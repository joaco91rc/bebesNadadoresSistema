using BebesNadadores.Application.Avisos;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Dashboard;
using BebesNadadores.Infrastructure.Services.Pagos;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Avisos;

/*
 * =============================================================
 * AVISOS (campana de la barra superior)
 * =============================================================
 *
 * Caja (Admin, Administrativa):
 *   - Caja de un día anterior que quedó abierta.        (urgente)
 *   - La caja de hoy todavía no se abrió.               (info)
 *   - Familias con cuotas vencidas.                     (advertencia)
 *
 * Administrativos (Admin, Administrativa):
 *   - Recuperos que vencen en los próximos 7 días.      (info)
 *   - Cumpleaños de hoy.                                (info)
 *   - Bebés que llegan a la edad máxima en 30 días.     (info)
 *
 * Médicos (Admin, Médica):
 *   - Aptos vencidos / por vencer / sin apto.           (advertencia / info)
 *
 * Administración (Admin):
 *   - Planes con inscriptos y sin tarifa vigente: el
 *     generador no les puede crear la cuota.            (urgente)
 */
public sealed class AvisosService(
    IDbContextFactory<AppDbContext> factory,
    IConfiguracionSistemaService configuracionService)
    : IAvisosService
{
    private const int DiasRecuperosPorVencer = 7;

    private const int DiasAlertaEdadMaxima = 30;


    public async Task<IReadOnlyList<AvisoDto>> ObtenerAsync(
        AlcanceAvisos alcance,
        CancellationToken ct = default)
    {
        var avisos =
            new List<AvisoDto>();

        if (!alcance.Caja && !alcance.Administrativos && !alcance.Medicos && !alcance.Administracion)
            return avisos;

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var hoy =
            RegistradorCobros.ObtenerFechaArgentina();

        var configuracion =
            await configuracionService.ObtenerAsync(ct);

        if (alcance.Caja)
            await AgregarAvisosCajaAsync(db, hoy, avisos, ct);

        if (alcance.Administrativos)
            await AgregarAvisosAdministrativosAsync(db, hoy, configuracion.EdadMaximaMeses, avisos, ct);

        if (alcance.Medicos)
            await AgregarAvisosMedicosAsync(db, hoy, configuracion.DiasAlertaApto, avisos, ct);

        if (alcance.Administracion)
            await AgregarAvisosAdministracionAsync(db, hoy, avisos, ct);

        return avisos
            .OrderByDescending(x => x.Nivel)
            .ToList();
    }


    // =========================================================
    // CAJA
    // =========================================================

    private static async Task AgregarAvisosCajaAsync(
        AppDbContext db,
        DateOnly hoy,
        List<AvisoDto> avisos,
        CancellationToken ct)
    {
        var abiertasAnteriores =
            await db.Cajas
                .AsNoTracking()
                .Where(x =>
                    x.Estado == EstadoCaja.Abierta
                    && x.FechaOperativa < hoy)
                .OrderBy(x => x.FechaOperativa)
                .Select(x => x.FechaOperativa)
                .ToListAsync(ct);

        foreach (var fecha in abiertasAnteriores)
        {
            avisos.Add(
                new AvisoDto(
                    $"caja-abierta-{fecha:yyyyMMdd}",
                    NivelAviso.Urgente,
                    $"Quedó abierta la caja del {fecha:dd/MM}",
                    "Hay que cerrarla para que el saldo del día quede registrado.",
                    "/caja"));
        }

        var hayCajaHoy =
            await db.Cajas
                .AsNoTracking()
                .AnyAsync(
                    x => x.FechaOperativa == hoy,
                    ct);

        var cerradoTodoElDia =
            await db.DiasNoLaborables
                .AsNoTracking()
                .AnyAsync(
                    x => x.Activo && x.Fecha == hoy && x.TodoElDia,
                    ct);

        if (!hayCajaHoy &&
            !cerradoTodoElDia &&
            hoy.DayOfWeek != DayOfWeek.Sunday)
        {
            avisos.Add(
                new AvisoDto(
                    "caja-hoy",
                    NivelAviso.Info,
                    "La caja de hoy no está abierta",
                    "Hace falta para cobrar en efectivo.",
                    "/caja"));
        }

        /*
         * Familias (bebés activos) con al menos una cuota vencida
         * con saldo. Se calcula por fecha de vencimiento, no por el
         * estado guardado.
         */
        var conDeuda =
            await db.Cuotas
                .AsNoTracking()
                .Where(x =>
                    x.Estado != EstadoCuota.Anulada
                    && x.Estado != EstadoCuota.Pagada
                    && x.FechaVencimiento < hoy
                    && x.Inscripcion.Bebe.Estado == EstadoBebe.Activo
                    && x.Importe > (x.Imputaciones.Sum(i => (decimal?)i.Importe) ?? 0m))
                .Select(x => x.Inscripcion.BebeId)
                .Distinct()
                .CountAsync(ct);

        if (conDeuda > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "cuotas-vencidas",
                    NivelAviso.Advertencia,
                    conDeuda == 1
                        ? "1 familia con cuotas vencidas"
                        : $"{conDeuda} familias con cuotas vencidas",
                    "Están en Pagos, con la deuda de cada bebé.",
                    "/pagos"));
        }
    }


    // =========================================================
    // ADMINISTRATIVOS
    // =========================================================

    private static async Task AgregarAvisosAdministrativosAsync(
        AppDbContext db,
        DateOnly hoy,
        int edadMaximaMeses,
        List<AvisoDto> avisos,
        CancellationToken ct)
    {
        var limiteRecuperos =
            hoy.AddDays(DiasRecuperosPorVencer);

        var recuperosPorVencer =
            await db.Recuperaciones
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.Estado == EstadoRecuperacion.Disponible
                        && x.FechaVencimiento.HasValue
                        && x.FechaVencimiento.Value >= hoy
                        && x.FechaVencimiento.Value <= limiteRecuperos
                        && x.Bebe.Estado == EstadoBebe.Activo,
                    ct);

        if (recuperosPorVencer > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "recuperos-por-vencer",
                    NivelAviso.Info,
                    recuperosPorVencer == 1
                        ? "1 recupero vence esta semana"
                        : $"{recuperosPorVencer} recuperos vencen esta semana",
                    "Conviene avisarles a las familias para que reserven.",
                    "/recuperos"));
        }

        var activos =
            await db.Bebes
                .AsNoTracking()
                .Where(x => x.Estado == EstadoBebe.Activo)
                .Select(x => new { x.Nombre, x.FechaNacimiento })
                .ToListAsync(ct);

        var cumpleanieros =
            activos
                .Where(x =>
                    x.FechaNacimiento.Month == hoy.Month
                    && x.FechaNacimiento.Day == hoy.Day)
                .Select(x => x.Nombre)
                .ToList();

        if (cumpleanieros.Count > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "cumpleanios",
                    NivelAviso.Info,
                    cumpleanieros.Count == 1
                        ? $"Hoy cumple años {cumpleanieros[0]}"
                        : $"Hoy cumplen años {cumpleanieros.Count} bebés",
                    "Desde el inicio se les puede mandar el saludo por WhatsApp.",
                    "/"));
        }

        var limiteEdad =
            hoy.AddDays(DiasAlertaEdadMaxima);

        var llegandoEdadMaxima =
            activos.Count(x =>
            {
                var cumple =
                    x.FechaNacimiento.AddMonths(edadMaximaMeses);

                return cumple >= hoy && cumple <= limiteEdad;
            });

        if (llegandoEdadMaxima > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "edad-maxima",
                    NivelAviso.Info,
                    llegandoEdadMaxima == 1
                        ? "1 bebé llega a la edad máxima este mes"
                        : $"{llegandoEdadMaxima} bebés llegan a la edad máxima este mes",
                    "Después de esa fecha no se pueden reinscribir.",
                    "/clientes"));
        }
    }


    // =========================================================
    // MÉDICOS
    // =========================================================

    private static async Task AgregarAvisosMedicosAsync(
        AppDbContext db,
        DateOnly hoy,
        int diasAlertaApto,
        List<AvisoDto> avisos,
        CancellationToken ct)
    {
        var activos =
            await db.Bebes
                .AsNoTracking()
                .Where(x => x.Estado == EstadoBebe.Activo)
                .Select(x => x.Id)
                .ToListAsync(ct);

        // Misma cuenta que el Dashboard.
        var resumen =
            await DashboardService.ObtenerResumenMedicoAsync(
                db,
                activos,
                hoy,
                diasAlertaApto,
                ct);

        if (resumen.Vencidos > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "aptos-vencidos",
                    NivelAviso.Advertencia,
                    resumen.Vencidos == 1
                        ? "1 apto médico vencido"
                        : $"{resumen.Vencidos} aptos médicos vencidos",
                    "Bebés activos con el apto vencido.",
                    "/aptos-medicos"));
        }

        if (resumen.PorVencer > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "aptos-por-vencer",
                    NivelAviso.Info,
                    resumen.PorVencer == 1
                        ? "1 apto médico por vencer"
                        : $"{resumen.PorVencer} aptos médicos por vencer",
                    $"Vencen en los próximos {diasAlertaApto} días.",
                    "/aptos-medicos"));
        }

        if (resumen.SinApto > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "sin-apto",
                    NivelAviso.Info,
                    resumen.SinApto == 1
                        ? "1 bebé sin apto médico"
                        : $"{resumen.SinApto} bebés sin apto médico",
                    "Bebés activos que todavía no tienen apto cargado.",
                    "/aptos-medicos"));
        }
    }


    // =========================================================
    // ADMINISTRACIÓN
    // =========================================================

    private static async Task AgregarAvisosAdministracionAsync(
        AppDbContext db,
        DateOnly hoy,
        List<AvisoDto> avisos,
        CancellationToken ct)
    {
        var planesSinTarifa =
            await db.Inscripciones
                .AsNoTracking()
                .Where(x =>
                    x.Estado == EstadoInscripcion.Activa
                    && x.Bebe.Estado == EstadoBebe.Activo
                    && !db.TarifasPlan.Any(t =>
                        t.PlanId == x.PlanId
                        && t.VigenciaDesde <= hoy
                        && (!t.VigenciaHasta.HasValue || t.VigenciaHasta.Value >= hoy)))
                .Select(x => x.Plan.Nombre)
                .Distinct()
                .ToListAsync(ct);

        if (planesSinTarifa.Count > 0)
        {
            avisos.Add(
                new AvisoDto(
                    "planes-sin-tarifa",
                    NivelAviso.Urgente,
                    planesSinTarifa.Count == 1
                        ? $"El plan {planesSinTarifa[0]} no tiene tarifa vigente"
                        : $"{planesSinTarifa.Count} planes sin tarifa vigente",
                    "Tiene bebés inscriptos y no se les puede generar la cuota del mes.",
                    "/configuracion"));
        }
    }
}
