using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Web.Services.Finanzas;

public enum IndicadorFinancieroTipo
{
    Revisar,
    AlDia,
    DebeUna,
    DebeVarias
}

public sealed record IndicadorFinancieroDto(
    IndicadorFinancieroTipo Tipo,
    int CuotasVencidas,
    decimal SaldoVencido,
    string Motivo);

public interface IIndicadorFinancieroService
{
    Task<IReadOnlyDictionary<Guid, IndicadorFinancieroDto>> ObtenerAsync(
        IReadOnlyCollection<Guid> bebeIds,
        CancellationToken ct = default);
}

/// <summary>
/// Indicadores informativos basados en deuda actual de PostgreSQL y la evidencia
/// de incertidumbre histórica persistida al finalizar la migración.
/// Si falta la conciliación validada, NO se muestra "Al día".
/// </summary>
public sealed class IndicadorFinancieroService(
    IDbContextFactory<AppDbContext> factory) : IIndicadorFinancieroService
{
    private static DateOnly HoyArgentina()
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        }
    }

    public async Task<IReadOnlyDictionary<Guid, IndicadorFinancieroDto>> ObtenerAsync(
        IReadOnlyCollection<Guid> bebeIds,
        CancellationToken ct = default)
    {
        var ids = bebeIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, IndicadorFinancieroDto>();

        var hoy = HoyArgentina();
        await using var db = await factory.CreateDbContextAsync(ct);
        var snapshots = await db.ConciliacionesFinancierasBebes
            .AsNoTracking()
            .Where(x => ids.Contains(x.BebeId))
            .ToDictionaryAsync(x => x.BebeId, ct);
        var cuotas = await db.Cuotas.AsNoTracking()
            .Where(c => ids.Contains(c.Inscripcion.BebeId) && c.Estado != EstadoCuota.Anulada)
            .Select(c => new
            {
                c.Inscripcion.BebeId,
                c.Periodo,
                c.FechaVencimiento,
                c.Importe,
                Pagado = c.Imputaciones.Sum(i => (decimal?)i.Importe) ?? 0m
            })
            .ToListAsync(ct);

        var porBebe = cuotas.GroupBy(x => x.BebeId).ToDictionary(x => x.Key, x => x.ToArray());
        var resultado = new Dictionary<Guid, IndicadorFinancieroDto>(ids.Length);

        foreach (var id in ids)
        {
            if (!snapshots.TryGetValue(id, out var snapshot))
            {
                resultado[id] = new(IndicadorFinancieroTipo.Revisar, 0, 0m,
                    "Sin conciliación histórica validada para este alumno.");
                continue;
            }

            porBebe.TryGetValue(id, out var filas);
            filas ??= [];
            var vencidas = filas
                .Where(x => x.FechaVencimiento < hoy && x.Importe - x.Pagado > 0.005m)
                .GroupBy(x => x.Periodo)
                .ToArray();
            var cantidad = vencidas.Length;
            var saldo = vencidas.Sum(g => g.Sum(x => Math.Max(0m, x.Importe - x.Pagado)));

            // La conciliación V4.8 no incorpora períodos sin evidencia. Si alguno
            // ya era incierto al corte, nunca deducimos "Al día" de las cuotas faltantes.
            bool requiereRevision = snapshot.MesesVencidosSinConciliar > 0;

            // Luego del corte también pueden vencer meses sin cuota registrada.
            // Solo se revisan meses posteriores al corte, sin inventar saldos.
            var periodo = new DateOnly(snapshot.FechaCorte.Year, snapshot.FechaCorte.Month, 1);
            while (periodo <= new DateOnly(hoy.Year, hoy.Month, 1))
            {
                var vence = new DateOnly(periodo.Year, periodo.Month, Math.Min(15, DateTime.DaysInMonth(periodo.Year, periodo.Month)));
                if (vence > snapshot.FechaCorte && vence < hoy &&
                    !filas.Any(x => x.Periodo == periodo))
                {
                    requiereRevision = true;
                }
                periodo = periodo.AddMonths(1);
            }

            if (requiereRevision)
            {
                resultado[id] = new(IndicadorFinancieroTipo.Revisar, cantidad, saldo,
                    cantidad == 0
                        ? "Existen períodos sin conciliar. Verificar la cuenta corriente."
                        : $"Hay {cantidad} cuota(s) vencida(s) y períodos adicionales sin conciliar.");
            }
            else if (cantidad > 1)
            {
                resultado[id] = new(IndicadorFinancieroTipo.DebeVarias, cantidad, saldo,
                    $"{cantidad} cuotas vencidas con saldo pendiente.");
            }
            else if (cantidad == 1)
            {
                resultado[id] = new(IndicadorFinancieroTipo.DebeUna, 1, saldo,
                    "Una cuota vencida con saldo pendiente.");
            }
            else
            {
                resultado[id] = new(IndicadorFinancieroTipo.AlDia, 0, 0m,
                    $"Sin deuda vencida documentada al {hoy:dd/MM/yyyy}.");
            }
        }
        return resultado;
    }

}
