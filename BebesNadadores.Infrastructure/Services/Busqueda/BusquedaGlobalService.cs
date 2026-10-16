using System.Globalization;
using System.Text;
using BebesNadadores.Application.Busqueda;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Busqueda;

/*
 * =============================================================
 * BUSCADOR GLOBAL
 * =============================================================
 *
 * Bebés y responsables se filtran en memoria para poder ignorar
 * acentos sin depender de la extensión "unaccent" de PostgreSQL
 * (un natatorio tiene cientos de bebés: es una consulta liviana).
 * Los comprobantes se buscan en la base (ILIKE).
 */
public sealed class BusquedaGlobalService(
    IDbContextFactory<AppDbContext> factory)
    : IBusquedaGlobalService
{
    private const int LargoMinimo = 2;

    private static readonly CultureInfo Cultura =
        CultureInfo.GetCultureInfo("es-AR");


    public async Task<IReadOnlyList<ResultadoBusquedaDto>> BuscarAsync(
        string texto,
        int maximo = 8,
        CancellationToken ct = default)
    {
        var terminos =
            Normalizar(texto)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (terminos.Length == 0 || terminos.Sum(x => x.Length) < LargoMinimo)
            return [];

        await using var db =
            await factory.CreateDbContextAsync(ct);


        // -----------------------------------------------------
        // Bebés + responsables
        // -----------------------------------------------------

        var bebes =
            await db.Bebes
                .AsNoTracking()
                .Select(x => new
                {
                    x.Id,
                    x.Nombre,
                    x.Apellido,
                    x.Dni,
                    x.Estado,
                    Nivel = x.NivelActual != null ? x.NivelActual.Nombre : null,
                    Responsables = x.Responsables
                        .OrderByDescending(r => r.EsContactoPrincipal)
                        .Select(r => new
                        {
                            r.Responsable.Nombre,
                            r.Responsable.Apellido,
                            r.Responsable.Dni,
                            r.Responsable.Telefono,
                            r.Responsable.WhatsApp,
                            r.Responsable.Email
                        })
                        .ToList()
                })
                .ToListAsync(ct);

        var encontrados =
            new List<(int Puntaje, ResultadoBusquedaDto Resultado)>();

        foreach (var bebe in bebes)
        {
            var nombreBebe =
                $"{bebe.Nombre} {bebe.Apellido}";

            var textoBebe =
                Normalizar($"{nombreBebe} {bebe.Dni}");

            var principal =
                bebe.Responsables.FirstOrDefault();

            var detalleBase =
                string.Join(
                    " · ",
                    new[]
                    {
                        bebe.Nivel,
                        principal is null ? null : $"{principal.Nombre} {principal.Apellido}".Trim()
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));

            // 1. Coincide con el bebé
            if (terminos.All(t => textoBebe.Contains(t)))
            {
                var empieza =
                    Normalizar(nombreBebe).StartsWith(terminos[0]) ||
                    Normalizar($"{bebe.Apellido} {bebe.Nombre}").StartsWith(terminos[0]);

                encontrados.Add((
                    empieza ? 3 : 2,
                    new ResultadoBusquedaDto(
                        TipoResultadoBusqueda.Bebe,
                        bebe.Id,
                        nombreBebe,
                        string.IsNullOrWhiteSpace(bebe.Dni) ? detalleBase : $"DNI {bebe.Dni} · {detalleBase}".TrimEnd(' ', '·'),
                        bebe.Estado == EstadoBebe.Baja)));

                continue;
            }

            // 2. Coincide con un responsable
            foreach (var r in bebe.Responsables)
            {
                var textoResponsable =
                    Normalizar($"{r.Nombre} {r.Apellido} {r.Dni} {r.Email} {SoloDigitos(r.Telefono)} {SoloDigitos(r.WhatsApp)}");

                if (!terminos.All(t => textoResponsable.Contains(t) || textoBebe.Contains(t)))
                    continue;

                encontrados.Add((
                    1,
                    new ResultadoBusquedaDto(
                        TipoResultadoBusqueda.Responsable,
                        bebe.Id,
                        nombreBebe,
                        $"Responsable: {r.Nombre} {r.Apellido}".Trim() +
                            (bebe.Nivel is null ? "" : $" · {bebe.Nivel}"),
                        bebe.Estado == EstadoBebe.Baja)));

                break;
            }
        }


        // -----------------------------------------------------
        // Comprobantes (referencia del pago)
        // -----------------------------------------------------

        var textoLimpio =
            texto.Trim();

        if (textoLimpio.Length >= 3)
        {
            var patron =
                $"%{textoLimpio.Replace("%", "").Replace("_", "")}%";

            var pagos =
                await db.Pagos
                    .AsNoTracking()
                    .Where(x =>
                        x.ReferenciaExterna != null
                        && EF.Functions.ILike(x.ReferenciaExterna, patron))
                    .OrderByDescending(x => x.FechaUtc)
                    .Take(5)
                    .Select(x => new
                    {
                        x.BebeId,
                        x.ReferenciaExterna,
                        x.Importe,
                        x.FechaUtc,
                        x.AnuladoUtc,
                        x.Bebe.Nombre,
                        x.Bebe.Apellido,
                        x.Bebe.Estado
                    })
                    .ToListAsync(ct);

            foreach (var pago in pagos)
            {
                encontrados.Add((
                    4,
                    new ResultadoBusquedaDto(
                        TipoResultadoBusqueda.Comprobante,
                        pago.BebeId,
                        $"Comprobante {pago.ReferenciaExterna}",
                        $"{pago.Nombre} {pago.Apellido} · {pago.Importe.ToString("C0", Cultura)} · " +
                            $"{pago.FechaUtc.ToOffset(TimeSpan.FromHours(-3)):dd/MM/yyyy}" +
                            (pago.AnuladoUtc.HasValue ? " · ANULADO" : ""),
                        pago.Estado == EstadoBebe.Baja)));
            }
        }


        return encontrados
            .OrderBy(x => x.Resultado.DadoDeBaja)
            .ThenByDescending(x => x.Puntaje)
            .ThenBy(x => x.Resultado.Titulo, StringComparer.Create(Cultura, true))
            .Select(x => x.Resultado)
            .Take(maximo)
            .ToList();
    }


    /*
     * minúsculas, sin acentos y sin puntos:
     * "Pérez" -> "perez", "40.123.456" -> "40123456".
     */
    public static string Normalizar(
        string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return string.Empty;

        var descompuesto =
            texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);

        var sb =
            new StringBuilder(descompuesto.Length);

        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString()
            .Normalize(NormalizationForm.FormC)
            .Replace(".", "");
    }


    private static string SoloDigitos(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(texto)
            ? string.Empty
            : new string(texto.Where(char.IsDigit).ToArray());
    }
}
