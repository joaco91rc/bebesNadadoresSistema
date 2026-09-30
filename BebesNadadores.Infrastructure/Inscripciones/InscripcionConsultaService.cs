using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Inscripciones.Dtos;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Inscripciones;

public sealed class InscripcionConsultaService(
    IDbContextFactory<AppDbContext> factory)
    : IInscripcionConsultaService
{
    public async Task<IReadOnlyList<PlanDisponibleDto>>
        ListarPlanesAsync(
            DateOnly fecha,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var planes =
            await db.Planes
                .AsNoTracking()
                .Where(x => x.Activo)
                .Include(x => x.Tarifas)
                .OrderBy(x => x.VecesPorSemana)
                .ToListAsync(ct);


        return planes
            .Select(plan =>
            {
                var tarifa =
                    plan.Tarifas
                        .Where(x =>
                            x.VigenciaDesde <= fecha
                            &&
                            (
                                !x.VigenciaHasta.HasValue
                                ||
                                x.VigenciaHasta.Value >= fecha
                            ))
                        .OrderByDescending(x =>
                            x.VigenciaDesde)
                        .FirstOrDefault();


                return new PlanDisponibleDto(
                    plan.Id,
                    plan.Nombre,
                    plan.VecesPorSemana,
                    tarifa?.PrecioMensual);
            })
            .ToList();
    }


    public async Task<IReadOnlyList<TurnoDisponibleDto>>
        ListarTurnosDisponiblesAsync(
            Guid nivelId,
            DateOnly fechaInicio,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        // =====================================================
        // TURNOS VIGENTES DEL NIVEL
        // =====================================================

        var turnos =
            await db.Turnos
                .AsNoTracking()
                .Include(x => x.Nivel)
                .Where(x =>
                    x.NivelId == nivelId
                    &&
                    x.Estado == EstadoTurno.Activo
                    &&
                    x.VigenteDesde <= fechaInicio
                    &&
                    (
                        !x.VigenteHasta.HasValue
                        ||
                        x.VigenteHasta.Value >= fechaInicio
                    ))
                .OrderBy(x => x.DiaSemana)
                .ThenBy(x => x.HoraInicio)
                .ToListAsync(ct);


        if (turnos.Count == 0)
        {
            return [];
        }


        var turnoIds =
            turnos
                .Select(x => x.Id)
                .ToList();


        // =====================================================
        // OCUPACIÓN PERMANENTE
        //
        // Contamos las inscripciones activas que ocupan
        // ese turno en la fecha elegida.
        // =====================================================

        var ocupacion =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Where(x =>
                    turnoIds.Contains(x.TurnoId)
                    &&
                    x.Inscripcion.Estado ==
                        EstadoInscripcion.Activa
                    &&
                    x.Inscripcion.FechaInicio <=
                        fechaInicio
                    &&
                    (
                        !x.Inscripcion.FechaFin.HasValue
                        ||
                        x.Inscripcion.FechaFin.Value >=
                            fechaInicio
                    ))
                .GroupBy(x => x.TurnoId)
                .Select(x =>
                    new
                    {
                        TurnoId =
                            x.Key,

                        Cantidad =
                            x.Count()
                    })
                .ToDictionaryAsync(
                    x => x.TurnoId,
                    x => x.Cantidad,
                    ct);


        return turnos
            .Select(turno =>
            {
                var ocupados =
                    ocupacion.TryGetValue(
                        turno.Id,
                        out var cantidad)
                        ? cantidad
                        : 0;


                var disponibles =
                    Math.Max(
                        0,
                        turno.Cupo - ocupados);


                return new TurnoDisponibleDto(
                    turno.Id,
                    turno.Nombre,
                    turno.DiaSemana,
                    turno.HoraInicio,
                    turno.HoraFin,
                    turno.Nivel.Nombre,
                    turno.Sector,
                    turno.Cupo,
                    ocupados,
                    disponibles);
            })
            .ToList();
    }
}