using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Turnera;

/// <summary>
/// El turno semanal sigue reservado, pero el alumno NO aparece en sus sesiones
/// anteriores (ni en la misma fecha) a una primera clase excepcional.
/// Después de esa primera clase, comienza a asistir a su horario habitual.
/// No modifica inscripciones históricas ni genera ausencias/recuperos falsos.
/// </summary>
public static class PrimeraClaseExcepcional
{
    public static async Task<IReadOnlyDictionary<Guid, DateOnly>> ObtenerFechasAsync(
        AppDbContext db,
        IEnumerable<Guid> inscripcionesIds,
        DateOnly fechaDesde,
        CancellationToken ct = default)
    {
        var ids = inscripcionesIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateOnly>();

        var reservas = await db.ReservasSesionesClase.AsNoTracking()
            .Where(x => ids.Contains(x.InscripcionId)
                && x.Tipo == TipoReservaSesion.InscripcionInicial
                && x.Estado == EstadoReservaSesion.Activa
                && x.SesionClase.Fecha >= fechaDesde)
            .Select(x => new { x.InscripcionId, x.SesionClase.Fecha })
            .ToListAsync(ct);

        return reservas.GroupBy(x => x.InscripcionId)
            .ToDictionary(x => x.Key, x => x.Min(y => y.Fecha));
    }

    public static async Task<HashSet<Guid>> ObtenerInscripcionesConReservaAnteriorAsync(
        AppDbContext db, IEnumerable<Guid> inscripcionesIds, DateOnly fechaSesion,
        CancellationToken ct = default)
    {
        var ids = inscripcionesIds.Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();
        var anteriores = await db.ReservasSesionesClase.AsNoTracking()
            .Where(x => ids.Contains(x.InscripcionId)
                && x.Tipo == TipoReservaSesion.InscripcionInicial
                && x.Estado == EstadoReservaSesion.Activa
                && x.SesionClase.Fecha < fechaSesion)
            .Select(x => x.InscripcionId).Distinct().ToListAsync(ct);
        return anteriores.ToHashSet();
    }

    public static bool TodaviaNoComenzo(
        IReadOnlyDictionary<Guid, DateOnly> fechas,
        Guid inscripcionId,
        DateOnly fechaSesion)
        => fechas.TryGetValue(inscripcionId, out var primeraClase)
           && fechaSesion <= primeraClase;
}
