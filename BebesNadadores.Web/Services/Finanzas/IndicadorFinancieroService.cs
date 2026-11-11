using System.Globalization;
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
/// Indicadores solo informativos. El archivo de conciliación V4.8 se encuentra
/// fuera de wwwroot y debe reemplazarse por el del último dump antes de producción.
/// Si falta un alumno o el reporte no está disponible, NO se muestra "Al día".
/// </summary>
public sealed class IndicadorFinancieroService(
    IDbContextFactory<AppDbContext> factory,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILogger<IndicadorFinancieroService> logger) : IIndicadorFinancieroService
{
    private sealed record Snapshot(DateOnly Corte, int MesesVencidosInciertos);

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
        var snapshots = LeerSnapshot();

        await using var db = await factory.CreateDbContextAsync(ct);
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
            bool requiereRevision = snapshot.MesesVencidosInciertos > 0;

            // Luego del corte también pueden vencer meses sin cuota registrada.
            // Solo se revisan meses posteriores al corte, sin inventar saldos.
            var periodo = new DateOnly(snapshot.Corte.Year, snapshot.Corte.Month, 1);
            while (periodo <= new DateOnly(hoy.Year, hoy.Month, 1))
            {
                var vence = new DateOnly(periodo.Year, periodo.Month, Math.Min(15, DateTime.DaysInMonth(periodo.Year, periodo.Month)));
                if (vence > snapshot.Corte && vence < hoy &&
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

    private Dictionary<Guid, Snapshot> LeerSnapshot()
    {
        var relativo = configuration["IndicadoresFinancieros:ArchivoConciliacion"]
            ?? "Conciliacion/finanzas_446_estado_indicador_v48.csv";
        var ruta = Path.IsPathRooted(relativo)
            ? relativo
            : Path.Combine(environment.ContentRootPath, relativo);
        var resultado = new Dictionary<Guid, Snapshot>();

        if (!File.Exists(ruta))
        {
            logger.LogWarning("Falta el reporte de conciliación financiera en {Ruta}. Todos los alumnos mostrarán Revisar.", ruta);
            return resultado;
        }
        var lines = File.ReadLines(ruta).ToArray();
        if (lines.Length < 2) return resultado;
        var encabezados = lines[0].TrimStart('\uFEFF').Split(';');
        int idxId = Array.IndexOf(encabezados, "BEBE_GUID");
        int idxCorte = Array.IndexOf(encabezados, "FECHA_CORTE");
        int idxPend = Array.IndexOf(encabezados, "MESES_VENCIDOS_SIN_CONCILIAR");
        if (idxId < 0 || idxCorte < 0 || idxPend < 0)
        {
            logger.LogWarning("El CSV de indicadores no tiene las columnas V4.8 esperadas.");
            return resultado;
        }
        foreach (var line in lines.Skip(1))
        {
            var c = line.Split(';');
            if (c.Length <= Math.Max(idxId, Math.Max(idxCorte, idxPend))) continue;
            if (Guid.TryParse(c[idxId], out var id) &&
                DateOnly.TryParseExact(c[idxCorte], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var corte) &&
                int.TryParse(c[idxPend], NumberStyles.Integer, CultureInfo.InvariantCulture, out int pendientes))
            {
                resultado[id] = new Snapshot(corte, pendientes);
            }
        }
        return resultado;
    }
}
