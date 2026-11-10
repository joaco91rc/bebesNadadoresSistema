using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Pagos;
using BebesNadadores.Infrastructure.Services.Turnera;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Recuperos;

/*
 * =============================================================
 * RECUPEROS POR CLASES NO DICTADAS
 * =============================================================
 *
 * Único lugar donde se generan. Antes había dos copias (Turnera
 * y Días no laborables) con reglas distintas, y solo corrían al
 * guardar un feriado o al generar sesiones: un feriado cargado
 * por adelantado nunca generaba recuperos.
 *
 * Lo llaman:
 *   - TurneraService          (al generar sesiones / cancelar una clase)
 *   - DiaNoLaborableService   (al guardar o activar un cierre)
 *   - RecuperosClasesNoDictadasBackgroundService (todos los días)
 *
 * Una clase genera recupero si está Cancelada y:
 *   - la canceló un cierre activo con "Genera recupero", o
 *   - se canceló a mano marcando "Genera recupero".
 *
 * Reciben recupero: los regulares vigentes ese día y los que
 * tenían reservada su clase inicial ahí. Nunca dos veces para el
 * mismo bebé y la misma clase, nunca a un bebé dado de baja y
 * nunca uno que ya nacería vencido.
 */
public sealed class GeneradorRecuperosClasesNoDictadas(
    IDbContextFactory<AppDbContext> factory,
    IConfiguracionSistemaService configuracionService)
    : IRecuperosClasesNoDictadasService
{
    /*
     * El job mira hacia atrás esta cantidad de días. Cubre que el
     * servidor haya estado apagado y la primera corrida después
     * de instalar esta versión.
     */
    private const int DiasHaciaAtras = 60;


    public async Task<int> GenerarPendientesAsync(
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var hoy =
            RegistradorCobros.ObtenerFechaArgentina();

        return await GenerarAsync(
            db,
            hoy.AddDays(-DiasHaciaAtras),
            hoy,
            ct);
    }


    /*
     * Trabaja sobre el DbContext del llamador y guarda.
     * Devuelve la cantidad de recuperos creados.
     */
    public async Task<int> GenerarAsync(
        AppDbContext db,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct)
    {
        var hoy =
            RegistradorCobros.ObtenerFechaArgentina();

        // No se generan recuperos de clases que todavía no pasaron.
        var hastaReal =
            hasta > hoy ? hoy : hasta;

        if (hastaReal < desde)
            return 0;


        // -----------------------------------------------------
        // Clases canceladas que generan recupero
        // -----------------------------------------------------

        var sesiones =
            (await db.SesionesClase
                .AsNoTracking()
                .Include(x => x.DiaNoLaborable)
                .Where(x =>
                    x.Fecha >= desde
                    && x.Fecha <= hastaReal
                    && x.Estado == EstadoSesionClase.Cancelada)
                .ToListAsync(ct))
            .Where(GeneraRecupero)
            .ToList();

        if (sesiones.Count == 0)
            return 0;

        var configuracion =
            await configuracionService.ObtenerAsync(ct);

        var sesionIds =
            sesiones.Select(x => x.Id).ToList();

        var turnoIds =
            sesiones.Select(x => x.TurnoId).Distinct().ToList();


        // -----------------------------------------------------
        // Recuperos que ya existen (cualquier tipo, no anulados):
        // si el bebé avisó que faltaba y ya tiene su recupero,
        // no se le da otro por la misma clase.
        // -----------------------------------------------------

        var existentes =
            (await db.Recuperaciones
                .AsNoTracking()
                .Where(x =>
                    sesionIds.Contains(x.SesionOrigenId)
                    && x.Estado != EstadoRecuperacion.Anulada)
                .Select(x => new { x.BebeId, x.SesionOrigenId })
                .ToListAsync(ct))
            .Select(x => (x.BebeId, x.SesionOrigenId))
            .ToHashSet();


        // -----------------------------------------------------
        // Beneficiarios
        // -----------------------------------------------------

        var inscripciones =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Include(x => x.Inscripcion)
                .Where(x => turnoIds.Contains(x.TurnoId))
                .ToListAsync(ct);

        var reservasIniciales =
            await db.ReservasSesionesClase
                .AsNoTracking()
                .Where(x =>
                    sesionIds.Contains(x.SesionClaseId)
                    && x.Estado == EstadoReservaSesion.Activa)
                .ToListAsync(ct);

        var bebesDeBaja =
            (await db.Bebes
                .AsNoTracking()
                .Where(x => x.Estado == EstadoBebe.Baja)
                .Select(x => x.Id)
                .ToListAsync(ct))
            .ToHashSet();


        var creados = 0;

        foreach (var sesion in sesiones)
        {
            var sinVencimiento =
                sesion.DiaNoLaborable?.RecuperoSinVencimiento ?? false;

            DateOnly? vencimiento =
                sinVencimiento
                    ? null
                    : sesion.Fecha.AddDays(configuracion.DiasVigenciaRecupero);

            if (vencimiento.HasValue && vencimiento.Value < hoy)
                continue;

            var beneficiarios =
                inscripciones
                    .Where(x =>
                        x.TurnoId == sesion.TurnoId
                        && CalculadoraCupoSesion.InscripcionValidaEnFecha(
                            x.Inscripcion,
                            sesion.Fecha))
                    .Select(x => (x.Inscripcion.BebeId, x.InscripcionId))
                    .Concat(
                        reservasIniciales
                            .Where(x => x.SesionClaseId == sesion.Id)
                            .Select(x => (x.BebeId, x.InscripcionId)));

            foreach (var (bebeId, inscripcionId) in beneficiarios)
            {
                if (bebesDeBaja.Contains(bebeId))
                    continue;

                // Add devuelve false si ya estaba: cubre existentes
                // y que el bebé sea regular y tenga clase inicial.
                if (!existentes.Add((bebeId, sesion.Id)))
                    continue;

                db.Recuperaciones.Add(
                    new Recuperacion
                    {
                        Id = Guid.NewGuid(),
                        BebeId = bebeId,
                        InscripcionId = inscripcionId,
                        SesionOrigenId = sesion.Id,
                        AsistenciaOrigenId = null,
                        Tipo = TipoRecuperacion.ClaseNoDictada,
                        FechaGeneracion = sesion.Fecha,
                        FechaVencimiento = vencimiento,
                        Estado = EstadoRecuperacion.Disponible
                    });

                creados++;
            }
        }

        if (creados > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return creados;
    }


    /*
     * ¿Esta clase cancelada da derecho a recupero?
     */
    public static bool GeneraRecupero(
        SesionClase sesion)
    {
        if (sesion.Estado != EstadoSesionClase.Cancelada)
            return false;

        if (sesion.DiaNoLaborableId.HasValue)
        {
            return sesion.DiaNoLaborable is { Activo: true, GeneraRecupero: true };
        }

        return sesion.CancelacionGeneraRecupero;
    }


    /*
     * Cuando se cancela una clase, los recuperos que estaban
     * RESERVADOS para usarse en ella vuelven a quedar disponibles
     * (o vencidos, si ya pasó su vencimiento), sin vincularse
     * a la lista de espera de nuevas inscripciones.
     *
     * No guarda: queda dentro del SaveChanges del llamador.
     */
    public static async Task<int> LiberarReservasEnSesionesAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> sesionIds,
        CancellationToken ct)
    {
        if (sesionIds.Count == 0)
            return 0;

        var hoy =
            RegistradorCobros.ObtenerFechaArgentina();

        var reservados =
            await db.Recuperaciones
                .Where(x =>
                    x.SesionUsoId != null
                    && sesionIds.Contains(x.SesionUsoId.Value)
                    && x.Estado == EstadoRecuperacion.Reservada)
                .ToListAsync(ct);

        if (reservados.Count == 0)
            return 0;

        foreach (var recupero in reservados)
        {
            recupero.SesionUsoId = null;
            recupero.UtilizadaUtc = null;
            recupero.Estado =
                recupero.FechaVencimiento.HasValue && recupero.FechaVencimiento.Value < hoy
                    ? EstadoRecuperacion.Vencida
                    : EstadoRecuperacion.Disponible;
        }

        return reservados.Count;
    }
}
