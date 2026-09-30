using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bebes;

public sealed class BebeLegajoService(
    IDbContextFactory<AppDbContext> factory)
    : IBebeLegajoService
{
    public async Task<BebeLegajoDto?> ObtenerAsync(
        Guid bebeId,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        // =====================================================
        // BEBÉ
        // =====================================================

        var bebe =
            await db.Bebes
                .AsNoTracking()
                .Include(x => x.NivelActual)
                .Include(x => x.Responsables)
                    .ThenInclude(x => x.Responsable)
                .FirstOrDefaultAsync(
                    x => x.Id == bebeId,
                    ct);


        if (bebe is null)
            return null;


        // =====================================================
        // RESPONSABLES
        // =====================================================

        var responsables =
            bebe.Responsables
                .OrderByDescending(x =>
                    x.EsContactoPrincipal)
                .ThenBy(x =>
                    x.Responsable.Apellido)
                .ThenBy(x =>
                    x.Responsable.Nombre)
                .Select(x =>
                    new ResponsableLegajoDto(
                        x.Responsable.Id,

                        $"{x.Responsable.Nombre} {x.Responsable.Apellido}"
                            .Trim(),

                        x.Responsable.Dni,
                        x.Responsable.Telefono,
                        x.Responsable.WhatsApp,
                        x.Responsable.Email,
                        x.Responsable.Domicilio,
                        x.Relacion,
                        x.EsContactoPrincipal,
                        x.AutorizaComunicaciones))
                .ToList();


        var responsablePrincipal =
            responsables
                .FirstOrDefault(x =>
                    x.EsContactoPrincipal)
            ??
            responsables.FirstOrDefault();


        // =====================================================
        // INSCRIPCIONES
        // =====================================================

        var inscripciones =
            await db.Inscripciones
                .AsNoTracking()

                .Include(x => x.Plan)
                    .ThenInclude(x => x.Tarifas)

                .Include(x => x.Turnos)
                    .ThenInclude(x => x.Turno)
                        .ThenInclude(x => x.Nivel)

                .Include(x => x.Turnos)
                    .ThenInclude(x => x.Turno)
                        .ThenInclude(x => x.Profesores)
                            .ThenInclude(x => x.Profesor)

                .Where(x =>
                    x.BebeId == bebeId)

                .OrderByDescending(x =>
                    x.FechaInicio)

                .ToListAsync(ct);


        var inscripcion =
            inscripciones
                .FirstOrDefault(x =>
                    x.Estado ==
                        EstadoInscripcion.Activa
                    &&
                    x.FechaInicio <= hoy
                    &&
                    (
                        !x.FechaFin.HasValue
                        ||
                        x.FechaFin.Value >= hoy
                    ));


        var inscripcionDto =
            inscripcion is null
                ? null
                : MapearInscripcion(
                    inscripcion,
                    hoy);


        // =====================================================
        // PRÓXIMA CLASE
        // =====================================================

        var proximaClase =
            inscripcion is null
                ? null
                : ObtenerProximaClase(
                    inscripcion,
                    hoy);


        // =====================================================
        // APTOS MÉDICOS
        // =====================================================

        var aptos =
            await db.AptosMedicos
                .AsNoTracking()
                .Where(x =>
                    x.BebeId == bebeId)
                .OrderByDescending(x =>
                    x.FechaRevision)
                .ToListAsync(ct);


        var aptosDto =
            aptos
                .Select(x =>
                    new AptoMedicoLegajoDto(
                        x.Id,
                        x.FechaRevision,
                        x.VigenciaHasta,
                        x.Resultado,
                        x.Observaciones,
                        x.MedicoUsuarioId,
                        x.DocumentoStorageKey,
                        x.DocumentoNombreOriginal,
                        x.EstaVigente(hoy)))
                .ToList();


        var aptoActual =
            aptosDto.FirstOrDefault();


        // =====================================================
        // ASISTENCIA - ÚLTIMOS 90 DÍAS
        // =====================================================

        var desde =
            hoy.AddDays(-90);


        var asistencias =
            await db.Asistencias
                .AsNoTracking()
                .Include(x => x.SesionClase)
                    .ThenInclude(x => x.Nivel)
                .Where(x =>
                    x.BebeId == bebeId
                    &&
                    x.SesionClase.Fecha >= desde)
                .OrderByDescending(x =>
                    x.SesionClase.Fecha)
                .ThenByDescending(x =>
                    x.SesionClase.HoraInicio)
                .ToListAsync(ct);


        var asistenciasDto =
            asistencias
                .Select(x =>
                    new AsistenciaLegajoDto(
                        x.Id,
                        x.SesionClaseId,
                        x.SesionClase.Fecha,
                        x.SesionClase.HoraInicio,
                        x.SesionClase.Nivel.Nombre,
                        x.Estado,
                        x.EsRecupero,
                        x.Observaciones))
                .ToList();


        var marcadas =
            asistencias.Count(x =>
                x.Estado !=
                    EstadoAsistencia.Pendiente);


        var presentes =
            asistencias.Count(x =>
                x.Estado ==
                    EstadoAsistencia.Presente);


        var ausentesConAviso =
            asistencias.Count(x =>
                x.Estado ==
                    EstadoAsistencia.AusenteConAviso);


        var ausentes =
            asistencias.Count(x =>
                x.Estado ==
                    EstadoAsistencia.Ausente);


        var pendientes =
            asistencias.Count(x =>
                x.Estado ==
                    EstadoAsistencia.Pendiente);


        var asistenciasRecupero =
            asistencias.Count(x =>
                x.EsRecupero);


        var porcentajeAsistencia =
            marcadas == 0
                ? 0
                : (int)Math.Round(
                    presentes * 100d /
                    marcadas);


        var asistenciaResumen =
            new AsistenciaResumenLegajoDto(
                asistencias.Count,
                presentes,
                ausentesConAviso,
                ausentes,
                pendientes,
                asistenciasRecupero,
                porcentajeAsistencia);


        // =====================================================
        // RECUPEROS
        // =====================================================

        var recuperaciones =
            await db.Recuperaciones
                .AsNoTracking()

                .Include(x => x.SesionOrigen)
                    .ThenInclude(x => x.Nivel)

                .Include(x => x.SesionUso)
                    .ThenInclude(x => x!.Nivel)

                .Where(x =>
                    x.BebeId == bebeId)

                .OrderByDescending(x =>
                    x.FechaGeneracion)

                .ToListAsync(ct);


        var recuperacionesDto =
            recuperaciones
                .Select(x =>
                {
                    var vencida =
                        x.EstaVencida(hoy);


                    var estadoEfectivo =
                        vencida
                            ? EstadoRecuperacion.Vencida
                            : x.Estado;


                    return new RecuperacionLegajoDto(
                        x.Id,
                        x.Tipo,
                        estadoEfectivo,
                        x.FechaGeneracion,
                        x.FechaVencimiento,
                        x.SinVencimiento,
                        vencida,
                        x.SesionOrigen.Fecha,
                        x.SesionOrigen.Nivel.Nombre,
                        x.SesionUsoId,
                        x.SesionUso?.Fecha,
                        x.SesionUso?.Nivel.Nombre);
                })
                .ToList();


        // =====================================================
        // PAGOS
        // =====================================================

        var pagos =
            await db.Pagos
                .AsNoTracking()
                .Include(x => x.Responsable)
                .Where(x =>
                    x.BebeId == bebeId)
                .OrderByDescending(x =>
                    x.FechaUtc)
                .Take(100)
                .ToListAsync(ct);


        var pagadoMesActual =
            pagos
                .Where(x =>
                    x.FechaUtc.Year == hoy.Year
                    &&
                    x.FechaUtc.Month == hoy.Month)
                .Sum(x =>
                    x.Importe);


        var pagosDto =
            pagos
                .Select(x =>
                    new PagoLegajoDto(
                        x.Id,
                        x.FechaUtc,
                        x.Importe,
                        x.MedioPago,
                        x.ReferenciaExterna,
                        x.Observaciones,

                        x.Responsable is null
                            ? null
                            : $"{x.Responsable.Nombre} {x.Responsable.Apellido}"
                                .Trim()))
                .ToList();


        // =====================================================
        // RESULTADO
        // =====================================================

        var edadMeses =
            bebe.EdadEnMeses(
                hoy);


        return new BebeLegajoDto(
            bebe.Id,
            bebe.Nombre,
            bebe.Apellido,
            $"{bebe.Nombre} {bebe.Apellido}".Trim(),
            bebe.FechaNacimiento,
            bebe.Sexo,
            bebe.Dni,
            bebe.FechaAlta,
            bebe.Estado,
            bebe.FechaBaja,
            bebe.MotivoBaja,
            bebe.NivelActualId,
            bebe.NivelActual?.Nombre,
            edadMeses,
            edadMeses / 12,
            bebe.AlcanzoEdadMaxima(hoy),

            responsablePrincipal,
            responsables,

            inscripcionDto,
            proximaClase,

            aptoActual,
            aptosDto,

            asistenciaResumen,
            asistenciasDto,

            recuperacionesDto,

            pagadoMesActual,
            pagosDto);
    }


    // =========================================================
    // INSCRIPCIÓN
    // =========================================================

    private static InscripcionLegajoDto MapearInscripcion(
    Inscripcion inscripcion,
    DateOnly hoy)
{
    var tarifa =
        inscripcion.Plan.Tarifas
            .Where(x =>
                x.VigenciaDesde <= hoy
                &&
                (
                    !x.VigenciaHasta.HasValue
                    ||
                    x.VigenciaHasta.Value >= hoy
                ))
            .OrderByDescending(x =>
                x.VigenciaDesde)
            .FirstOrDefault();


    var turnos =
        inscripcion.Turnos
            .Where(x =>
                x.Turno.Estado ==
                    EstadoTurno.Activo
                &&
                (
                    !x.Turno.VigenteHasta.HasValue
                    ||
                    x.Turno.VigenteHasta.Value >= hoy
                ))
            .OrderBy(x =>
                x.Turno.DiaSemana)
            .ThenBy(x =>
                x.Turno.HoraInicio)
            .Select(x =>
                new TurnoLegajoDto(
                    x.Turno.Id,
                    x.Turno.Nombre,
                    x.Turno.DiaSemana,
                    x.Turno.HoraInicio,
                    x.Turno.HoraFin,
                    x.Turno.Nivel.Nombre,
                    x.Turno.Sector,
                    x.EsPrincipal,

                    x.Turno.Profesores
                        .OrderByDescending(p =>
                            p.EsPrincipal)
                        .ThenBy(p =>
                            p.Profesor.Apellido)
                        .ThenBy(p =>
                            p.Profesor.Nombre)
                        .Select(p =>
                            $"{p.Profesor.Nombre} {p.Profesor.Apellido}"
                                .Trim())
                        .ToList()))
            .ToList();


    return new InscripcionLegajoDto(
        inscripcion.Id,

        inscripcion.PlanId,
        inscripcion.Plan.Nombre,
        inscripcion.Plan.VecesPorSemana,
        tarifa?.PrecioMensual,

        inscripcion.FechaInicio,
        inscripcion.FechaFin,

        inscripcion.Estado,
        inscripcion.MedioPagoPreferido,

        inscripcion.TipoDescuento,
        inscripcion.ValorDescuento,
        inscripcion.MotivoDescuento,

        inscripcion.Observaciones,

        turnos);
}


    // =========================================================
    // PRÓXIMA CLASE RECURRENTE
    // =========================================================

    private static ProximaClaseLegajoDto?
        ObtenerProximaClase(
            Inscripcion inscripcion,
            DateOnly hoy)
    {
        var ahora =
            TimeOnly.FromDateTime(
                DateTime.Now);


        ProximaClaseLegajoDto? mejor =
            null;


        DateTime? mejorFechaHora =
            null;


        foreach (var relacion in
            inscripcion.Turnos)
        {
            var turno =
                relacion.Turno;


            if (turno.Estado !=
                EstadoTurno.Activo)
            {
                continue;
            }


            for (var i = 0; i <= 8; i++)
            {
                var fecha =
                    hoy.AddDays(i);


                if (turno.VigenteDesde >
                    fecha)
                {
                    continue;
                }


                if (
                    turno.VigenteHasta.HasValue
                    &&
                    turno.VigenteHasta.Value <
                    fecha)
                {
                    continue;
                }


                if (ConvertirDiaSemana(
                        fecha.DayOfWeek)
                    != turno.DiaSemana)
                {
                    continue;
                }


                if (
                    i == 0
                    &&
                    turno.HoraFin < ahora)
                {
                    continue;
                }


                var fechaHora =
                    fecha.ToDateTime(
                        turno.HoraInicio);


                if (
                    mejorFechaHora.HasValue
                    &&
                    fechaHora >=
                    mejorFechaHora.Value)
                {
                    break;
                }


                mejorFechaHora =
                    fechaHora;


                mejor =
                    new ProximaClaseLegajoDto(
                        turno.Id,
                        fecha,
                        turno.DiaSemana,
                        turno.HoraInicio,
                        turno.HoraFin,
                        turno.Nivel.Nombre,
                        turno.Sector,

                        turno.Profesores
                            .OrderByDescending(x =>
                                x.EsPrincipal)
                            .ThenBy(x =>
                                x.Profesor.Apellido)
                            .ThenBy(x =>
                                x.Profesor.Nombre)
                            .Select(x =>
                                $"{x.Profesor.Nombre} {x.Profesor.Apellido}"
                                    .Trim())
                            .ToList());


                break;
            }
        }


        return mejor;
    }


    private static DiaSemana ConvertirDiaSemana(
        DayOfWeek dia)
    {
        return dia switch
        {
            DayOfWeek.Monday =>
                DiaSemana.Lunes,

            DayOfWeek.Tuesday =>
                DiaSemana.Martes,

            DayOfWeek.Wednesday =>
                DiaSemana.Miercoles,

            DayOfWeek.Thursday =>
                DiaSemana.Jueves,

            DayOfWeek.Friday =>
                DiaSemana.Viernes,

            DayOfWeek.Saturday =>
                DiaSemana.Sabado,

            DayOfWeek.Sunday =>
                DiaSemana.Domingo,

            _ =>
                throw new ArgumentOutOfRangeException(
                    nameof(dia))
        };
    }
}