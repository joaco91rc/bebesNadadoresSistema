using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Turnera;

/*
 * =============================================================
 * CALCULADORA DE CUPO DE UNA CLASE (SesionClase)
 * =============================================================
 *
 * Único lugar donde se calcula cuánta gente va a estar en una
 * clase y cuántos lugares quedan. La usan:
 *
 *   - Turnera / Dashboard   (ocupación que se muestra)
 *   - Recuperos             (vacantes y reserva de un recupero)
 *   - Alta de bebé          (reserva de la clase inicial)
 *
 * Antes cada pantalla lo calculaba a su manera y Recuperos no
 * contaba las reservas de clase inicial: ofrecía como vacante un
 * lugar ya reservado (overbooking).
 *
 * Quiénes ocupan un lugar (un bebé cuenta UNA sola vez):
 *
 *   1. Regulares:        inscriptos al turno, vigentes ese día.
 *   2. Clase inicial:    reservas activas de primera clase.
 *   3. Recuperos:        recuperos reservados / utilizados ahí.
 *
 * Una ausencia con aviso de un regular o de una clase inicial
 * libera su lugar para ESE día. La de alguien que venía a
 * recuperar no libera nada (su lugar ya era temporal).
 *
 *   Esperados = Regulares + ClaseInicial + Recuperos
 *   Presentes = Esperados - AusentesConAviso
 *   Libres    = Cupo - Presentes
 */
public static class CalculadoraCupoSesion
{
    public static async Task<OcupacionSesion> CalcularAsync(
        AppDbContext db,
        SesionClase sesion,
        CancellationToken ct)
    {
        var resultado =
            await CalcularAsync(
                db,
                [sesion],
                ct);

        return resultado[sesion.Id];
    }


    public static async Task<IReadOnlyDictionary<Guid, OcupacionSesion>> CalcularAsync(
        AppDbContext db,
        IReadOnlyCollection<SesionClase> sesiones,
        CancellationToken ct)
    {
        if (sesiones.Count == 0)
        {
            return new Dictionary<Guid, OcupacionSesion>();
        }

        var turnoIds =
            sesiones
                .Select(x => x.TurnoId)
                .Distinct()
                .ToList();

        var sesionIds =
            sesiones
                .Select(x => x.Id)
                .ToList();


        // Una consulta por tipo de ocupante para todas las sesiones.

        var inscripciones =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Where(x => turnoIds.Contains(x.TurnoId))
                .Select(x => new
                {
                    x.TurnoId,
                    x.Inscripcion.BebeId,
                    x.Inscripcion.Estado,
                    x.Inscripcion.FechaInicio,
                    x.Inscripcion.FechaFin
                })
                .ToListAsync(ct);

        var reservasIniciales =
            await db.ReservasSesionesClase
                .AsNoTracking()
                .Where(x =>
                    sesionIds.Contains(x.SesionClaseId)
                    && x.Estado == EstadoReservaSesion.Activa)
                .Select(x => new { x.SesionClaseId, x.BebeId })
                .ToListAsync(ct);

        var recuperos =
            await db.Recuperaciones
                .AsNoTracking()
                .Where(x =>
                    x.SesionUsoId != null
                    && sesionIds.Contains(x.SesionUsoId.Value)
                    && (x.Estado == EstadoRecuperacion.Reservada
                        || x.Estado == EstadoRecuperacion.Utilizada))
                .Select(x => new { SesionId = x.SesionUsoId!.Value, x.BebeId })
                .ToListAsync(ct);

        var ausencias =
            await db.Asistencias
                .AsNoTracking()
                .Where(x =>
                    sesionIds.Contains(x.SesionClaseId)
                    && x.Estado == EstadoAsistencia.AusenteConAviso)
                .Select(x => new { x.SesionClaseId, x.BebeId })
                .ToListAsync(ct);


        var resultado =
            new Dictionary<Guid, OcupacionSesion>();

        foreach (var sesion in sesiones)
        {
            var regulares =
                inscripciones
                    .Where(x =>
                        x.TurnoId == sesion.TurnoId
                        && InscripcionValidaEnFecha(
                            x.Estado,
                            x.FechaInicio,
                            x.FechaFin,
                            sesion.Fecha))
                    .Select(x => x.BebeId)
                    .ToHashSet();

            var claseInicial =
                reservasIniciales
                    .Where(x =>
                        x.SesionClaseId == sesion.Id
                        && !regulares.Contains(x.BebeId))
                    .Select(x => x.BebeId)
                    .ToHashSet();

            var deRecupero =
                recuperos
                    .Where(x =>
                        x.SesionId == sesion.Id
                        && !regulares.Contains(x.BebeId)
                        && !claseInicial.Contains(x.BebeId))
                    .Select(x => x.BebeId)
                    .ToHashSet();

            var ausentesConAviso =
                ausencias
                    .Where(x =>
                        x.SesionClaseId == sesion.Id
                        && (regulares.Contains(x.BebeId)
                            || claseInicial.Contains(x.BebeId)))
                    .Select(x => x.BebeId)
                    .Distinct()
                    .Count();

            resultado[sesion.Id] =
                new OcupacionSesion(
                    sesion.Id,
                    sesion.Cupo,
                    regulares.Count,
                    claseInicial.Count,
                    deRecupero.Count,
                    ausentesConAviso);
        }

        return resultado;
    }


    /*
     * Regla única de "la inscripción cuenta ese día".
     */
    public static bool InscripcionValidaEnFecha(
        Inscripcion inscripcion,
        DateOnly fecha)
    {
        return InscripcionValidaEnFecha(
            inscripcion.Estado,
            inscripcion.FechaInicio,
            inscripcion.FechaFin,
            fecha);
    }


    public static bool InscripcionValidaEnFecha(
        EstadoInscripcion estado,
        DateOnly fechaInicio,
        DateOnly? fechaFin,
        DateOnly fecha)
    {
        if (estado is EstadoInscripcion.Cancelada or EstadoInscripcion.Pausada)
            return false;

        if (fechaInicio > fecha)
            return false;

        if (fechaFin.HasValue && fechaFin.Value < fecha)
            return false;

        return true;
    }
}


/*
 * Foto de la ocupación de una clase.
 */
public sealed record OcupacionSesion(
    Guid SesionId,
    int Cupo,
    int Regulares,
    int ClaseInicial,
    int Recuperos,
    int AusentesConAviso)
{
    /*
     * Bebés que figuran en la clase (incluye a los que avisaron
     * que faltan). Es lo que se muestra como "inscriptos".
     */
    public int Esperados =>
        Regulares + ClaseInicial + Recuperos;


    /*
     * Lugares que nunca ocupa un alumno fijo (regular o clase
     * inicial). Son vacantes de inscripción.
     */
    public int VacantesFijas =>
        Math.Max(0, Cupo - Regulares - ClaseInicial);


    /*
     * Lugares libres ESE día: lo que se puede ofrecer para un
     * recupero o una clase inicial.
     */
    public int Libres =>
        Math.Max(0, Cupo - (Esperados - AusentesConAviso));
}
