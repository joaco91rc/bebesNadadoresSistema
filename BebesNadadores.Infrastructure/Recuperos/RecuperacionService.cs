using System.Data;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Application.Recuperos.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Turnera;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Application.Turnera;

namespace BebesNadadores.Infrastructure.Services.Recuperos;

public sealed class RecuperosService(
    IDbContextFactory<AppDbContext> factory,
    ITurneraService turneraService)
    : IRecuperosService
    {
    // =========================================================
    // PANEL
    // =========================================================

    public async Task<RecuperosPanelDto>
    ObtenerPanelAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
{
    if (hasta < desde)
    {
        throw new InvalidOperationException(
            "La fecha Hasta no puede ser anterior a Desde.");
    }


    // =====================================================
    // ASEGURAR SESIONES DEL PERÍODO
    // =====================================================

    await turneraService
        .ObtenerAgendaAsync(
            desde,
            hasta,
            ct);


    await using var db =
        await factory.CreateDbContextAsync(ct);


    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    await ActualizarVencidosAsync(
        db,
        hoy,
        ct);

    
    // =====================================================
    // RECUPEROS ACTIVOS
    // =====================================================

    var recuperaciones =
        await db.Recuperaciones
            .AsNoTracking()

            .Include(x =>
                x.Bebe)

            .Include(x =>
                x.SesionOrigen)
                .ThenInclude(x =>
                    x.Nivel)

            .Include(x =>
                x.SesionOrigen)
                .ThenInclude(x =>
                    x.Turno)

            .Include(x =>
                x.SesionUso)

            .Where(x =>
                x.Estado ==
                    EstadoRecuperacion.Disponible
                ||
                x.Estado ==
                    EstadoRecuperacion.Reservada)

            .OrderBy(x =>
                x.FechaVencimiento == null)

            .ThenBy(x =>
                x.FechaVencimiento)

            .ThenBy(x =>
                x.Bebe.Apellido)

            .ThenBy(x =>
                x.Bebe.Nombre)

            .ToListAsync(ct);


    var recuperosDto =
        recuperaciones
            .Select(x =>
                new RecuperacionActivaDto(
                    x.Id,

                    x.BebeId,

                    $"{x.Bebe.Nombre} {x.Bebe.Apellido}"
                        .Trim(),

                    x.SesionOrigen.NivelId,

                    x.SesionOrigen.Nivel.Nombre,

                    x.Tipo,

                    x.Estado,

                    x.FechaGeneracion,

                    x.FechaVencimiento,

                    x.SinVencimiento,

                    x.SesionOrigen.Fecha,

                    x.SesionOrigen.HoraInicio,

                    x.SesionOrigen.Turno.Nombre,

                    x.SesionUsoId,

                    x.SesionUso?.Fecha,

                    x.SesionUso?.HoraInicio))
            .ToList();


    // =====================================================
    // SESIONES DEL PERÍODO
    // =====================================================

    var sesiones =
        await db.SesionesClase
            .AsNoTracking()

            .Include(x =>
                x.Nivel)

            .Include(x =>
                x.Turno)
                .ThenInclude(x =>
                    x.Profesores)
                        .ThenInclude(x =>
                            x.Profesor)

            .Where(x =>
                x.Fecha >= desde
                &&
                x.Fecha <= hasta
                &&
                x.Estado ==
                    EstadoSesionClase.Programada)

            .OrderBy(x =>
                x.Fecha)

            .ThenBy(x =>
                x.HoraInicio)

            .ToListAsync(ct);


    // =====================================================
    // SI NO HAY SESIONES
    // =====================================================

    if (sesiones.Count == 0)
    {
        var resumenSinSesiones =
            new RecuperosResumenDto(

                recuperosDto.Count(x =>
                    x.Estado ==
                        EstadoRecuperacion.Disponible),

                recuperosDto.Count(x =>
                    x.Estado ==
                        EstadoRecuperacion.Disponible
                    &&
                    x.FechaVencimiento.HasValue
                    &&
                    x.FechaVencimiento.Value >= hoy
                    &&
                    x.FechaVencimiento.Value <=
                        hoy.AddDays(7)),

                recuperosDto.Count(x =>
                    x.Estado ==
                        EstadoRecuperacion.Reservada),

                0);


        return new RecuperosPanelDto(
            resumenSinSesiones,
            recuperosDto,
            []);
    }


    var turnoIds =
        sesiones
            .Select(x =>
                x.TurnoId)
            .Distinct()
            .ToList();


    var sesionIds =
        sesiones
            .Select(x =>
                x.Id)
            .ToList();


    // =====================================================
    // OCUPACIÓN DE CADA CLASE
    //
    // Misma cuenta que la Turnera y el alta de bebé
    // (CalculadoraCupoSesion): regulares + clase inicial +
    // recuperos, menos los que avisaron que faltan.
    // =====================================================

    var ocupaciones =
        await CalculadoraCupoSesion.CalcularAsync(
            db,
            sesiones,
            ct);


    // =====================================================
    // ARMAR LUGARES PARA RECUPERO
    // =====================================================

    var vacantes =
        new List<VacanteRecuperoDto>();


    foreach (var sesion in sesiones)
    {
        var ocupacion =
            ocupaciones[sesion.Id];

        if (ocupacion.Libres == 0)
        {
            continue;
        }


        // -------------------------------------------------
        // PROFESORES
        // -------------------------------------------------

        var profesores =
            sesion
                .Turno
                .Profesores

                .OrderByDescending(x =>
                    x.EsPrincipal)

                .ThenBy(x =>
                    x.Profesor.Apellido)

                .ThenBy(x =>
                    x.Profesor.Nombre)

                .Select(x =>
                    $"{x.Profesor.Nombre} " +
                    $"{x.Profesor.Apellido}")

                .Select(x =>
                    x.Trim())

                .ToList();


       vacantes.Add(
    new VacanteRecuperoDto(
        sesion.Id,
        sesion.TurnoId,
        sesion.Fecha,
        sesion.HoraInicio,
        sesion.HoraFin,
        sesion.NivelId,
        sesion.Nivel.Nombre,
        sesion.Turno.Nombre,
        sesion.Cupo,
        ocupacion.Regulares,
        ocupacion.AusentesConAviso,
        ocupacion.Recuperos,
        ocupacion.VacantesFijas,
        ocupacion.AusentesConAviso,
        ocupacion.Libres,
        profesores,
        ocupacion.ClaseInicial));
    }


    // =====================================================
    // RESUMEN
    // =====================================================

    var disponibles =
        recuperosDto.Count(x =>
            x.Estado ==
                EstadoRecuperacion.Disponible);


    var reservadosTotal =
        recuperosDto.Count(x =>
            x.Estado ==
                EstadoRecuperacion.Reservada);


    var vencenProximos =
        recuperosDto.Count(x =>
            x.Estado ==
                EstadoRecuperacion.Disponible
            &&
            x.FechaVencimiento.HasValue
            &&
            x.FechaVencimiento.Value >= hoy
            &&
            x.FechaVencimiento.Value <=
                hoy.AddDays(7));


    /*
     * Ahora este número representa exclusivamente
     * lugares temporales todavía disponibles.
     */
    var lugaresSemana =
        vacantes.Sum(x =>
            x.VacantesParaRecupero);


    var resumen =
        new RecuperosResumenDto(
            disponibles,
            vencenProximos,
            reservadosTotal,
            lugaresSemana);


    return new RecuperosPanelDto(
        resumen,
        recuperosDto,
        vacantes);
}

// =========================================================
// CLASES COMPATIBLES PARA UN RECUPERO
// =========================================================

public async Task<IReadOnlyList<VacanteRecuperoDto>>
    ListarClasesCompatiblesAsync(
        Guid recuperacionId,
        DateOnly desde,
        DateOnly hasta,
        bool respetarDiaPreferido = true,
        CancellationToken ct = default)
{
    if (hasta < desde)
    {
        throw new InvalidOperationException(
            "La fecha Hasta no puede ser anterior a Desde.");
    }


    await using var db =
        await factory.CreateDbContextAsync(ct);


    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    await ActualizarVencidosAsync(
        db,
        hoy,
        ct);


    // =====================================================
    // RECUPERO
    // =====================================================

    var recuperacion =
        await db.Recuperaciones
            .AsNoTracking()

            .Include(x =>
                x.Bebe)

            .Include(x =>
                x.SesionOrigen)

            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        recuperacionId,
                ct)

        ?? throw new InvalidOperationException(
            "El recupero indicado no existe.");


    if (recuperacion.Estado !=
        EstadoRecuperacion.Disponible)
    {
        return [];
    }


    // =====================================================
    // PREFERENCIA EN LISTA DE ESPERA
    // =====================================================

    var solicitud =
        await db.ListaEspera
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.RecuperacionId ==
                        recuperacionId

                    &&

                    (
                        x.Estado ==
                            EstadoListaEspera.Activa

                        ||

                        x.Estado ==
                            EstadoListaEspera.Notificada
                    ),
                ct);


    var diaPreferido =
        solicitud?.DiaPreferido;


    var franja =
        solicitud?.FranjaHoraria
        ?? FranjaHoraria.Indistinta;


    // =====================================================
    // RANGO REAL
    // =====================================================

    var fechaDesde =
        desde < hoy
            ? hoy
            : desde;


    /*
     * Nunca permitimos usar el recupero antes
     * de la clase que lo originó.
     */
    if (fechaDesde <
        recuperacion.FechaGeneracion)
    {
        fechaDesde =
            recuperacion.FechaGeneracion;
    }


    var fechaHasta =
        hasta;


    if (recuperacion.FechaVencimiento.HasValue &&
        recuperacion.FechaVencimiento.Value <
        fechaHasta)
    {
        fechaHasta =
            recuperacion.FechaVencimiento.Value;
    }


    if (fechaHasta < fechaDesde)
    {
        return [];
    }


    // =====================================================
    // SESIONES
    // =====================================================

    var query =
        db.SesionesClase
            .AsNoTracking()

            .Include(x =>
                x.Nivel)

            .Include(x =>
                x.Turno)
                .ThenInclude(x =>
                    x.Profesores)
                    .ThenInclude(x =>
                        x.Profesor)

            .Where(x =>
                x.Estado ==
                    EstadoSesionClase.Programada

                &&

                x.Fecha >=
                    fechaDesde

                &&

                x.Fecha <=
                    fechaHasta

                &&

                x.NivelId ==
                    recuperacion.SesionOrigen.NivelId

                &&

                x.Id !=
                    recuperacion.SesionOrigenId);


    var sesiones =
        await query
            .OrderBy(x =>
                x.Fecha)
            .ThenBy(x =>
                x.HoraInicio)
            .ToListAsync(ct);


    if (sesiones.Count == 0)
    {
        return [];
    }


    // =====================================================
    // TURNOS DONDE EL BEBÉ YA ES REGULAR
    // =====================================================

    var turnoIds =
        sesiones
            .Select(x =>
                x.TurnoId)
            .Distinct()
            .ToList();


    var inscripcionesBebe =
        await db.InscripcionesTurnos
            .AsNoTracking()

            .Include(x =>
                x.Inscripcion)

            .Where(x =>
                turnoIds.Contains(
                    x.TurnoId)

                &&

                x.Inscripcion.BebeId ==
                    recuperacion.BebeId)

            .ToListAsync(ct);


    // =====================================================
    // ARMAR RESULTADO
    // =====================================================

    var resultado =
        new List<VacanteRecuperoDto>();


    foreach (var sesion in sesiones)
    {
        /*
         * No puede recuperar en un turno al que
         * ya pertenece regularmente.
         */
        var yaEsRegular =
            inscripcionesBebe.Any(x =>
                x.TurnoId ==
                    sesion.TurnoId

                &&

                InscripcionValidaEnFecha(
                    x.Inscripcion,
                    sesion.Fecha));


        if (yaEsRegular)
        {
            continue;
        }


        // -------------------------------------------------
        // DÍA PREFERIDO
        // -------------------------------------------------

        if (respetarDiaPreferido &&
            diaPreferido.HasValue)
        {
            var diaSesion =
                ObtenerDiaSemana(
                    sesion.Fecha);


            if (diaSesion !=
                diaPreferido.Value)
            {
                continue;
            }
        }


        // -------------------------------------------------
        // FRANJA HORARIA
        // -------------------------------------------------
        //
        // Seguimos respetando la franja aunque la
        // administrativa habilite "otros días".
        //

        if (!CoincideFranjaListaEspera(
            franja,
            sesion.HoraInicio))
        {
            continue;
        }


        // -------------------------------------------------
        // DISPONIBILIDAD
        // -------------------------------------------------

        var ocupacion =
            await CalculadoraCupoSesion.CalcularAsync(
                db,
                sesion,
                ct);


        if (ocupacion.Libres <= 0)
        {
            continue;
        }


        var profesores =
            sesion
                .Turno
                .Profesores

                .OrderByDescending(x =>
                    x.EsPrincipal)

                .ThenBy(x =>
                    x.Profesor.Apellido)

                .ThenBy(x =>
                    x.Profesor.Nombre)

                .Select(x =>
                    $"{x.Profesor.Nombre} {x.Profesor.Apellido}"
                        .Trim())

                .ToList();


        resultado.Add(
            new VacanteRecuperoDto(
                sesion.Id,
                sesion.TurnoId,
                sesion.Fecha,
                sesion.HoraInicio,
                sesion.HoraFin,
                sesion.NivelId,
                sesion.Nivel.Nombre,
                sesion.Turno.Nombre,
                sesion.Cupo,
                ocupacion.Regulares,
                ocupacion.AusentesConAviso,
                ocupacion.Recuperos,
                ocupacion.VacantesFijas,
                ocupacion.AusentesConAviso,
                ocupacion.Libres,
                profesores,
                ocupacion.ClaseInicial));
    }


    return resultado;
}

    // =========================================================
    // CANDIDATOS PARA UNA SESIÓN
    // =========================================================

   public async Task<IReadOnlyList<CandidatoRecuperoDto>>
    ListarCandidatosAsync(
        Guid sesionClaseId,
        CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    await ActualizarVencidosAsync(
        db,
        hoy,
        ct);


    // =========================================================
    // SESIÓN
    // =========================================================

    var sesion =
        await db.SesionesClase
            .AsNoTracking()

            .Include(x =>
                x.Nivel)

            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        sesionClaseId,
                ct)

        ?? throw new InvalidOperationException(
            "La clase seleccionada no existe.");


    if (sesion.Estado !=
        EstadoSesionClase.Programada)
    {
        throw new InvalidOperationException(
            "Sólo puede asignar recuperos a una clase programada.");
    }


    if (sesion.Fecha < hoy)
    {
        throw new InvalidOperationException(
            "No puede asignar un recupero a una clase pasada.");
    }


    // =========================================================
    // ALUMNOS REGULARES DE ESE TURNO
    // =========================================================

    var relaciones =
        await db.InscripcionesTurnos
            .AsNoTracking()

            .Include(x =>
                x.Inscripcion)

            .Where(x =>
                x.TurnoId ==
                    sesion.TurnoId)

            .ToListAsync(ct);


    var bebesRegulares =
        relaciones
            .Where(x =>
                InscripcionValidaEnFecha(
                    x.Inscripcion,
                    sesion.Fecha))

            .Select(x =>
                x.Inscripcion.BebeId)

            .ToHashSet();


    // =========================================================
    // DÍA DE LA SESIÓN
    // =========================================================

    var diaSesion =
        ObtenerDiaSemana(
            sesion.Fecha);


    // =========================================================
    // LISTA DE ESPERA
    //
    // MUY IMPORTANTE:
    //
    // Tener recupero disponible NO significa querer usarlo.
    //
    // Sólo tomamos bebés que la administrativa haya agregado
    // explícitamente a Lista de espera.
    // =========================================================

    var solicitudes =
        await db.ListaEspera
            .AsNoTracking()

            .Include(x =>
                x.Bebe)

            .Include(x =>
                x.Recuperacion)
                    .ThenInclude(x =>
                        x!.SesionOrigen)

            .Where(x =>
                (
                    x.Estado ==
                        EstadoListaEspera.Activa

                    ||

                    x.Estado ==
                        EstadoListaEspera.Notificada
                )

                &&
                x.NivelId ==
                    sesion.NivelId

                &&
                x.RecuperacionId.HasValue

                &&
                x.Recuperacion != null

                &&
                x.Recuperacion.Estado ==
                    EstadoRecuperacion.Disponible

                &&
                !x.Recuperacion.SesionUsoId.HasValue

                &&
                x.Recuperacion.FechaGeneracion <=
                    sesion.Fecha

                &&
                (
                    !x.Recuperacion.FechaVencimiento.HasValue
                    ||
                    x.Recuperacion.FechaVencimiento.Value >=
                        sesion.Fecha
                )

                &&
                (
                    !x.DiaPreferido.HasValue
                    ||
                    x.DiaPreferido.Value ==
                        diaSesion
                ))

            .ToListAsync(ct);


    // =========================================================
    // FILTRAR FRANJA Y OTRAS REGLAS
    // =========================================================

    var candidatos =
        solicitudes

            /*
             * Si ya es alumno habitual de este mismo turno,
             * no tiene sentido utilizar el recupero acá.
             */
            .Where(x =>
                !bebesRegulares.Contains(
                    x.BebeId))

            /*
             * No usamos como recuperación la misma sesión
             * que originó el recupero.
             */
            .Where(x =>
                x.Recuperacion!.SesionOrigenId !=
                    sesion.Id)

            /*
             * Respetamos la franja solicitada por la familia.
             */
            .Where(x =>
                CoincideFranjaListaEspera(
                    x.FranjaHoraria,
                    sesion.HoraInicio))

            .Select(x =>
            {
                var recuperacion =
                    x.Recuperacion!;


                var diasRestantes =
                    recuperacion
                        .FechaVencimiento
                        .HasValue

                        ? recuperacion
                              .FechaVencimiento
                              .Value
                              .DayNumber
                          -
                          hoy.DayNumber

                        : (int?)null;


                var tienePreferencias =
                    x.DiaPreferido.HasValue
                    ||
                    x.FranjaHoraria !=
                        FranjaHoraria.Indistinta;


                return new CandidatoRecuperoDto(
                    recuperacion.Id,

                    x.BebeId,

                    $"{x.Bebe.Nombre} {x.Bebe.Apellido}"
                        .Trim(),

                    recuperacion.Tipo,

                    recuperacion.SesionOrigen.Fecha,

                    recuperacion.FechaVencimiento,

                    diasRestantes,

                    tienePreferencias,

                    tienePreferencias,

                    ObtenerPreferenciaListaEsperaTexto(
                        x.DiaPreferido,
                        x.FranjaHoraria));
            })

            /*
             * Primero los que vencen antes.
             */
            .OrderBy(x =>
                x.FechaVencimiento
                ??
                DateOnly.MaxValue)

            /*
             * Luego los que tenían una preferencia concreta
             * y esta clase coincide con ella.
             */
            .ThenByDescending(x =>
                x.CoincidePreferencia)

            .ThenBy(x =>
                x.Bebe)

            .ToList();


    return candidatos;
}
private static bool CoincideFranjaListaEspera(
    FranjaHoraria franja,
    TimeOnly hora)
{
    return franja switch
    {
        FranjaHoraria.Indistinta =>
            true,

        FranjaHoraria.Manana =>
            hora <
                new TimeOnly(12, 0),

        FranjaHoraria.Tarde =>
            hora >=
                new TimeOnly(12, 0)
            &&
            hora <
                new TimeOnly(18, 0),

        FranjaHoraria.Noche =>
            hora >=
                new TimeOnly(18, 0),

        _ =>
            true
    };
}


private static string ObtenerPreferenciaListaEsperaTexto(
    DiaSemana? dia,
    FranjaHoraria franja)
{
    var diaTexto =
        dia.HasValue
            ? ObtenerDiaSemanaTexto(
                dia.Value)
            : "Cualquier día";


    var franjaTexto =
        ObtenerFranjaTexto(
            franja);


    return
        $"{diaTexto} · {franjaTexto}";
}

    // =========================================================
    // RESERVAR
    // =========================================================

    public Task ReservarAsync(
        Guid recuperacionId,
        Guid sesionClaseId,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => ReservarInternoAsync(
                recuperacionId,
                sesionClaseId,
                ct),
            ct);
    }


    private async Task ReservarInternoAsync(
        Guid recuperacionId,
        Guid sesionClaseId,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database
                .BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        var recuperacion =
            await db.Recuperaciones

                .Include(x =>
                    x.Bebe)

                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            recuperacionId,
                    ct)
            ?? throw new InvalidOperationException(
                "El recupero indicado no existe.");


        if (recuperacion.Estado !=
            EstadoRecuperacion.Disponible)
        {
            throw new InvalidOperationException(
                "El recupero ya no se encuentra disponible.");
        }


        if (
            recuperacion.FechaVencimiento.HasValue
            &&
            recuperacion.FechaVencimiento.Value <
                hoy)
        {
            recuperacion.Estado =
                EstadoRecuperacion.Vencida;


            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);


            throw new InvalidOperationException(
                "El recupero se encuentra vencido.");
        }


        var sesion =
            await db.SesionesClase

                .Include(x =>
                    x.Nivel)

                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            sesionClaseId,
                    ct)
            ?? throw new InvalidOperationException(
                "La clase seleccionada no existe.");


        if (sesion.Estado !=
            EstadoSesionClase.Programada)
        {
            throw new InvalidOperationException(
                "La clase seleccionada no se encuentra programada.");
        }


        if (sesion.Fecha < hoy)
        {
            throw new InvalidOperationException(
                "No puede reservar un recupero en una clase pasada.");
        }


        if (
            recuperacion.FechaVencimiento.HasValue
            &&
            sesion.Fecha >
                recuperacion.FechaVencimiento.Value)
        {
            throw new InvalidOperationException(
                "La clase seleccionada ocurre después del vencimiento del recupero.");
        }


        if (recuperacion.FechaGeneracion >
            sesion.Fecha)
        {
            throw new InvalidOperationException(
                "No puede utilizar un recupero antes de la clase que lo originó.");
        }


        // =====================================================
        // NIVEL
        // =====================================================

        if (
            !recuperacion.Bebe.NivelActualId.HasValue
            ||
            recuperacion.Bebe.NivelActualId.Value !=
                sesion.NivelId)
        {
            throw new InvalidOperationException(
                "El bebé no pertenece al nivel de la clase seleccionada.");
        }


        // =====================================================
        // NO PUEDE SER ALUMNO REGULAR DEL MISMO TURNO
        // =====================================================

        var relacionesRegulares =
            await db.InscripcionesTurnos
                .AsNoTracking()

                .Include(x =>
                    x.Inscripcion)

                .Where(x =>
                    x.TurnoId ==
                        sesion.TurnoId
                    &&
                    x.Inscripcion.BebeId ==
                        recuperacion.BebeId)

                .ToListAsync(ct);


        if (
            relacionesRegulares.Any(x =>
                InscripcionValidaEnFecha(
                    x.Inscripcion,
                    sesion.Fecha)))
        {
            throw new InvalidOperationException(
                "El bebé ya asiste regularmente a ese turno.");
        }


        // =====================================================
        // VALIDAR CUPO OPERATIVO
        // =====================================================

        var ocupacion =
            await CalculadoraCupoSesion.CalcularAsync(
                db,
                sesion,
                ct);


        if (ocupacion.Libres <= 0)
        {
            throw new InvalidOperationException(
                "La clase ya no tiene lugares disponibles para recupero.");
        }


        // =====================================================
        // RESERVAR
        // =====================================================

        recuperacion.Estado =
            EstadoRecuperacion.Reservada;

        recuperacion.SesionUsoId =
            sesion.Id;

        recuperacion.UtilizadaUtc =
            null;

            // =========================================================
// MARCAR SOLICITUD DE LISTA DE ESPERA COMO ASIGNADA
// =========================================================

var solicitudesEspera =
    await db.ListaEspera
        .Where(x =>
            x.RecuperacionId ==
                recuperacion.Id

            &&
            (
                x.Estado ==
                    EstadoListaEspera.Activa

                ||

                x.Estado ==
                    EstadoListaEspera.Notificada
            ))

        .ToListAsync(ct);


foreach (var solicitud in solicitudesEspera)
{
    solicitud.Estado =
        EstadoListaEspera.Asignada;
}


        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }


    // =========================================================
    // LIBERAR RESERVA
    // =========================================================

    public async Task LiberarReservaAsync(
        Guid recuperacionId,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var recuperacion =
            await db.Recuperaciones
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            recuperacionId,
                    ct)
            ?? throw new InvalidOperationException(
                "El recupero indicado no existe.");


        if (recuperacion.Estado !=
            EstadoRecuperacion.Reservada)
        {
            throw new InvalidOperationException(
                "El recupero no posee una reserva activa.");
        }


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        if (
            recuperacion.FechaVencimiento.HasValue
            &&
            recuperacion.FechaVencimiento.Value <
                hoy)
        {
            recuperacion.Estado =
                EstadoRecuperacion.Vencida;


            await db.SaveChangesAsync(ct);


            throw new InvalidOperationException(
                "La reserva fue liberada, pero el recupero ya se encuentra vencido.");
        }


        recuperacion.Estado =
            EstadoRecuperacion.Disponible;

        recuperacion.SesionUsoId =
            null;

        recuperacion.UtilizadaUtc =
            null;

            var solicitudes =
    await db.ListaEspera
        .Where(x =>
            x.RecuperacionId ==
                recuperacion.Id

            &&
            x.Estado ==
                EstadoListaEspera.Asignada)

        .ToListAsync(ct);


foreach (var solicitud in solicitudes)
{
    solicitud.Estado =
        EstadoListaEspera.Activa;
}


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // VENCIMIENTOS
    // =========================================================

    private static async Task ActualizarVencidosAsync(
        AppDbContext db,
        DateOnly hoy,
        CancellationToken ct)
    {
        var vencidos =
            await db.Recuperaciones

                .Where(x =>
                    (
                        x.Estado ==
                            EstadoRecuperacion.Disponible
                        ||
                        x.Estado ==
                            EstadoRecuperacion.Reservada
                    )
                    &&
                    x.FechaVencimiento.HasValue
                    &&
                    x.FechaVencimiento.Value <
                        hoy)

                .ToListAsync(ct);


        if (vencidos.Count == 0)
        {
            return;
        }


        foreach (var recuperacion in vencidos)
        {
            recuperacion.Estado =
                EstadoRecuperacion.Vencida;
        }


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // PREFERENCIAS
    // =========================================================

    private static bool CoincidePreferencia(
        PreferenciaRecuperacion preferencia,
        DiaSemana dia,
        TimeOnly hora)
    {
        if (!preferencia.Activa)
        {
            return false;
        }


        if (preferencia.DiaSemana !=
            dia)
        {
            return false;
        }


        if (
            preferencia.HoraDesde.HasValue
            &&
            hora <
                preferencia.HoraDesde.Value)
        {
            return false;
        }


        if (
            preferencia.HoraHasta.HasValue
            &&
            hora >
                preferencia.HoraHasta.Value)
        {
            return false;
        }


        return preferencia.FranjaHoraria switch
        {
            FranjaHoraria.Indistinta =>
                true,

            FranjaHoraria.Manana =>
                hora <
                    new TimeOnly(12, 0),

            FranjaHoraria.Tarde =>
                hora >=
                    new TimeOnly(12, 0)
                &&
                hora <
                    new TimeOnly(18, 0),

            FranjaHoraria.Noche =>
                hora >=
                    new TimeOnly(18, 0),

            _ =>
                true
        };
    }


    private static string? ObtenerPreferenciaTexto(
        IReadOnlyCollection<PreferenciaRecuperacion> preferencias,
        DiaSemana dia,
        TimeOnly hora)
    {
        if (preferencias.Count == 0)
        {
            return null;
        }


        var coincidencia =
            preferencias
                .FirstOrDefault(x =>
                    CoincidePreferencia(
                        x,
                        dia,
                        hora));


        if (coincidencia is null)
        {
            return "Tiene preferencias configuradas en otros horarios";
        }


        var texto =
            $"{ObtenerDiaSemanaTexto(coincidencia.DiaSemana)} · " +
            $"{ObtenerFranjaTexto(coincidencia.FranjaHoraria)}";


        if (
            coincidencia.HoraDesde.HasValue
            ||
            coincidencia.HoraHasta.HasValue)
        {
            texto +=
                " · " +
                $"{coincidencia.HoraDesde?.ToString("HH:mm") ?? "--:--"}" +
                " a " +
                $"{coincidencia.HoraHasta?.ToString("HH:mm") ?? "--:--"}";
        }


        return texto;
    }


    // =========================================================
    // INSCRIPCIÓN VÁLIDA EN FECHA
    // =========================================================

    private static bool InscripcionValidaEnFecha(
        Inscripcion inscripcion,
        DateOnly fecha)
    {
        if (inscripcion.Estado is
            EstadoInscripcion.Cancelada
            or
            EstadoInscripcion.Pausada)
        {
            return false;
        }


        if (inscripcion.FechaInicio >
            fecha)
        {
            return false;
        }


        if (
            inscripcion.FechaFin.HasValue
            &&
            inscripcion.FechaFin.Value <
                fecha)
        {
            return false;
        }


        return true;
    }


    // =========================================================
    // DÍA DE LA SEMANA
    // =========================================================

    private static DiaSemana ObtenerDiaSemana(
        DateOnly fecha)
    {
        return fecha.DayOfWeek switch
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
                throw new ArgumentOutOfRangeException()
        };
    }


    private static string ObtenerDiaSemanaTexto(
        DiaSemana dia)
    {
        return dia switch
        {
            DiaSemana.Lunes =>
                "Lunes",

            DiaSemana.Martes =>
                "Martes",

            DiaSemana.Miercoles =>
                "Miércoles",

            DiaSemana.Jueves =>
                "Jueves",

            DiaSemana.Viernes =>
                "Viernes",

            DiaSemana.Sabado =>
                "Sábado",

            DiaSemana.Domingo =>
                "Domingo",

            _ =>
                dia.ToString()
        };
    }


    private static string ObtenerFranjaTexto(
        FranjaHoraria franja)
    {
        return franja switch
        {
            FranjaHoraria.Indistinta =>
                "Horario indistinto",

            FranjaHoraria.Manana =>
                "Mañana",

            FranjaHoraria.Tarde =>
                "Tarde",

            FranjaHoraria.Noche =>
                "Noche",

            _ =>
                franja.ToString()
        };
    }
}