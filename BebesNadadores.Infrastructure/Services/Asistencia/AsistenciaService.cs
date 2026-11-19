using BebesNadadores.Application.Asistencia;
using BebesNadadores.Application.Asistencia.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Domain.Common;
using BebesNadadores.Infrastructure.Services.Turnera;

namespace BebesNadadores.Infrastructure.Services.Asistencias;

public sealed class AsistenciaService(
    IDbContextFactory<AppDbContext> factory,
    IConfiguracionSistemaService configuracionService)
    : IAsistenciaService
{
    // =========================================================
    // OBTENER ASISTENCIA DE UNA SESIÓN
    // =========================================================

    public async Task<AsistenciaSesionDto?>
        ObtenerSesionAsync(
            Guid sesionId,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


      var sesion =
    await db.SesionesClase
        .AsNoTracking()

        .Include(x =>
            x.Nivel)

        .Include(x =>
            x.Turno)

        .Include(x =>
            x.Profesores)
            .ThenInclude(x =>
                x.Profesor)

        .FirstOrDefaultAsync(
            x => x.Id == sesionId,
            ct);


        if (sesion is null)
            return null;


        // =====================================================
        // ALUMNOS REGULARES DEL TURNO
        // =====================================================

        var relaciones =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Include(x => x.Inscripcion)
                    .ThenInclude(x => x.Bebe)
                .Where(x =>
                    x.TurnoId == sesion.TurnoId)
                .ToListAsync(ct);

        // No incluir Inscripcion.Turnos desde InscripcionTurno:
        // esa navegación vuelve sobre la misma relación y en una consulta
        // AsNoTracking EF Core puede lanzar un error por ciclo de Include.
        // Solo necesitamos los días de la semana, no todo el grafo.
        var inscripcionesIds = relaciones
            .Select(x => x.InscripcionId)
            .Distinct()
            .ToArray();

        var horariosInscripciones = await db.InscripcionesTurnos
            .AsNoTracking()
            .Where(x => inscripcionesIds.Contains(x.InscripcionId))
            .Select(x => new { x.InscripcionId, x.Turno.DiaSemana })
            .ToListAsync(ct);

        var diasPorInscripcion = horariosInscripciones
            .GroupBy(x => x.InscripcionId)
            .ToDictionary(
                x => x.Key,
                x => x.Select(t => (int)t.DiaSemana).ToArray());


        // La primera clase puede realizarse en un horario excepcional.
        // Hasta entonces no se espera al alumno en su horario habitual.
        var primerasExcepcionales = await PrimeraClaseExcepcional.ObtenerFechasAsync(
            db, relaciones.Select(x => x.InscripcionId), sesion.Fecha, ct);

        var esperados =
            new Dictionary<
                Guid,
                AlumnoEsperado>();


        foreach (var relacion in relaciones)
        {
            var inscripcion =
                relacion.Inscripcion;


            if (PrimeraClaseExcepcional.TodaviaNoComenzo(
                    primerasExcepcionales, relacion.InscripcionId, sesion.Fecha)
                || !InscripcionValidaEnFecha(
                    inscripcion,
                    sesion.Fecha))
            {
                continue;
            }


            esperados[inscripcion.BebeId] =
                new AlumnoEsperado(
                    inscripcion.BebeId,
                    inscripcion.Id,
                    $"{inscripcion.Bebe.Nombre} {inscripcion.Bebe.Apellido}".Trim(),
                    false,
                    diasPorInscripcion.TryGetValue(inscripcion.Id, out var dias)
                        && EsPrimeraClaseEnHorarioFijo(inscripcion.FechaInicio, dias, sesion.Fecha));
        }


// =========================================================
// RESERVAS DE PRIMERA CLASE
// =========================================================

var reservasIniciales =
    await db.ReservasSesionesClase
        .AsNoTracking()

        .Include(x =>
            x.Bebe)

        .Where(x =>
            x.SesionClaseId ==
                sesion.Id
            &&
            x.Estado ==
                EstadoReservaSesion.Activa)

        .ToListAsync(ct);

// La primera clase reservada también puede coincidir con el turno habitual.
var primeraClaseReservadaIds = reservasIniciales
    .Where(x => x.Tipo == TipoReservaSesion.InscripcionInicial)
    .Select(x => x.BebeId)
    .ToHashSet();


foreach (var reserva in
    reservasIniciales)
{
    /*
     * Puede pasar que tenga horario habitual
     * y la reserva coincida accidentalmente
     * con la misma clase.
     *
     * Priorizamos siempre la inscripción regular.
     */
    if (esperados.ContainsKey(
        reserva.BebeId))
    {
        continue;
    }


    esperados[
        reserva.BebeId] =
        new AlumnoEsperado(
            reserva.BebeId,
            reserva.InscripcionId,

            $"{reserva.Bebe.Nombre} " +
            $"{reserva.Bebe.Apellido}",

            false,
            reserva.Tipo == TipoReservaSesion.InscripcionInicial);
}

        // =====================================================
        // ALUMNOS QUE VAN COMO RECUPERO
        // =====================================================

        var recuperosUso =
            await db.Recuperaciones
                .AsNoTracking()
                .Include(x => x.Bebe)
                .Where(x =>
                    x.SesionUsoId == sesion.Id &&
                    (
                        x.Estado ==
                            EstadoRecuperacion.Reservada
                        ||
                        x.Estado ==
                            EstadoRecuperacion.Utilizada
                    ))
                .ToListAsync(ct);


        foreach (var recupero in recuperosUso)
        {
            /*
             * Si por alguna razón ya es alumno regular
             * de ese turno, priorizamos la inscripción
             * normal.
             */
            if (esperados.ContainsKey(
                recupero.BebeId))
            {
                continue;
            }


            esperados[recupero.BebeId] =
                new AlumnoEsperado(
                    recupero.BebeId,
                    recupero.InscripcionId,
                    $"{recupero.Bebe.Nombre} {recupero.Bebe.Apellido}".Trim(),
                    true);
        }


        // =====================================================
        // ASISTENCIAS YA REGISTRADAS
        // =====================================================

        var asistencias =
            await db.Asistencias
                .AsNoTracking()
                .Where(x =>
                    x.SesionClaseId == sesion.Id)
                .ToDictionaryAsync(
                    x => x.BebeId,
                    ct);


        // =====================================================
        // RECUPEROS GENERADOS POR ESTA CLASE
        // =====================================================

        var recuperosGenerados =
            await db.Recuperaciones
                .AsNoTracking()
                .Where(x =>
                    x.SesionOrigenId == sesion.Id &&
                    x.Tipo ==
                        TipoRecuperacion.AusenciaConAviso)
                .ToListAsync(ct);


        var recuperosPorBebe =
            recuperosGenerados
                .GroupBy(x => x.BebeId)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(r => r.Estado == EstadoRecuperacion.Anulada).First());


        // =====================================================
        // ARMAR RESULTADO
        // =====================================================

        var alumnos =
            new List<AsistenciaAlumnoDto>();


        foreach (var esperado in esperados.Values)
        {
            asistencias.TryGetValue(
                esperado.BebeId,
                out var asistencia);


            recuperosPorBebe.TryGetValue(
                esperado.BebeId,
                out var recuperacion);


            var estado =
                asistencia?.Estado
                ?? EstadoAsistencia.Pendiente;


            var recuperoVigente =
                recuperacion is not null &&
                recuperacion.Estado !=
                    EstadoRecuperacion.Anulada;


            alumnos.Add(
                new AsistenciaAlumnoDto(
                    esperado.BebeId,
                    esperado.InscripcionId,
                    esperado.NombreCompleto,
                    estado,
                    asistencia?.EsRecupero
                        ?? esperado.EsRecupero,
                    asistencia?.Observaciones,
                    asistencia?.RegistradaUtc,
                    recuperoVigente,
                    recuperoVigente
                        ? recuperacion!.FechaVencimiento
                        : null,
                    !esperado.EsRecupero &&
                        (esperado.EsPrimeraClase || primeraClaseReservadaIds.Contains(esperado.BebeId))));
        }


        alumnos =
            alumnos
                .OrderBy(x =>
                    x.NombreCompleto)
                .ToList();

             var profesores =
    sesion.Profesores
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


        return new AsistenciaSesionDto(
    sesion.Id,
    sesion.TurnoId,
    sesion.Turno.Nombre,
    sesion.Fecha,
    sesion.HoraInicio,
    sesion.HoraFin,
    sesion.Estado,
    alumnos.Count,

    alumnos.Count(x =>
        x.Estado ==
            EstadoAsistencia.Presente),

    alumnos.Count(x =>
        x.Estado ==
            EstadoAsistencia.Ausente),

    alumnos.Count(x =>
        x.Estado ==
            EstadoAsistencia.AusenteConAviso),

    alumnos.Count(x =>
        x.Estado ==
            EstadoAsistencia.Pendiente),

    alumnos,

    sesion.Nivel.Nombre,

    profesores);
    }


    // =========================================================
    // MARCAR UN ALUMNO
    // =========================================================


    private async Task<Guid> ValidarPrimeraClaseAsync(AppDbContext db, SesionClase origen, Guid bebeId, CancellationToken ct)
    {
        var detalle = await ObtenerSesionAsync(origen.Id, ct);
        var alumno = detalle?.Alumnos.FirstOrDefault(x => x.BebeId == bebeId && x.EsPrimeraClase && !x.EsRecupero);
        if (alumno is null)
            throw new InvalidOperationException("El alumno ya no tiene una primera clase en este horario.");
        if (alumno.Estado == EstadoAsistencia.Presente)
            throw new InvalidOperationException("La primera clase ya figura como presente. Corregí primero ese estado.");
        if (await db.Asistencias.AnyAsync(x => x.InscripcionId == alumno.InscripcionId && x.Estado == EstadoAsistencia.Presente, ct))
            throw new InvalidOperationException("El alumno ya registra una clase realizada en esta inscripción.");
        return alumno.InscripcionId;
    }

    public async Task<IReadOnlyList<OpcionPrimeraClaseDto>> ListarReprogramacionAsync(Guid sesionId, Guid bebeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var origen = await ObtenerSesionEditableAsync(db, sesionId, ct);
        var inscripcionId = await ValidarPrimeraClaseAsync(db, origen, bebeId, ct);
        var inscripcion = await db.Inscripciones.AsNoTracking().SingleAsync(x => x.Id == inscripcionId, ct);
        var desde = FechaArgentina.Hoy > origen.Fecha ? FechaArgentina.Hoy : origen.Fecha;
        var hasta = desde.AddDays(60);
        if (inscripcion.FechaFin is DateOnly fin && fin < hasta) hasta = fin;
        var sesiones = await db.SesionesClase.AsNoTracking().Include(x => x.Nivel)
            .Where(x => x.Id != sesionId && x.NivelId == origen.NivelId && x.Fecha >= desde && x.Fecha <= hasta
                && x.Estado == EstadoSesionClase.Programada && x.Turno.Estado == EstadoTurno.Activo
                && (x.Fecha > origen.Fecha || x.HoraInicio > origen.HoraInicio))
            .OrderBy(x => x.Fecha).ThenBy(x => x.HoraInicio).ToListAsync(ct);
        var ocupaciones = await CalculadoraCupoSesion.CalcularAsync(db, sesiones, ct);
        var ocupadas = await db.Asistencias.AsNoTracking().Where(x => x.BebeId == bebeId && x.Estado != EstadoAsistencia.Pendiente)
            .Select(x => x.SesionClaseId).ToListAsync(ct);
        var reservas = await db.ReservasSesionesClase.AsNoTracking().Where(x => x.BebeId == bebeId && x.Estado == EstadoReservaSesion.Activa)
            .Select(x => x.SesionClaseId).ToListAsync(ct);
        return sesiones.Where(x => ocupaciones[x.Id].Libres > 0 && !ocupadas.Contains(x.Id) && !reservas.Contains(x.Id))
            .Select(x => new OpcionPrimeraClaseDto(x.Id, x.Fecha, x.HoraInicio, x.Nivel.Nombre, ocupaciones[x.Id].Libres)).ToList();
    }

    public async Task ReprogramarPrimeraClaseAsync(Guid sesionId, Guid bebeId, Guid destinoId, string? observaciones, Guid? usuarioId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var origen = await ObtenerSesionEditableAsync(db, sesionId, ct);
        var inscripcionId = await ValidarPrimeraClaseAsync(db, origen, bebeId, ct);
        var destino = await ObtenerSesionEditableAsync(db, destinoId, ct);
        var inscripcion = await db.Inscripciones.SingleAsync(x => x.Id == inscripcionId, ct);
        if (destino.Id == origen.Id || destino.NivelId != origen.NivelId || destino.Estado != EstadoSesionClase.Programada
            || destino.Turno.Estado != EstadoTurno.Activo || destino.Fecha < FechaArgentina.Hoy
            || destino.Fecha < origen.Fecha || (destino.Fecha == origen.Fecha && destino.HoraInicio <= origen.HoraInicio)
            || !InscripcionValidaEnFecha(inscripcion, destino.Fecha))
            throw new InvalidOperationException("La nueva clase no está disponible para esta inscripción.");
        if ((await CalculadoraCupoSesion.CalcularAsync(db, destino, ct)).Libres <= 0)
            throw new InvalidOperationException("La clase elegida ya no tiene cupos disponibles.");
        if (await db.Asistencias.AnyAsync(x => x.SesionClaseId == destinoId && x.BebeId == bebeId && x.Estado != EstadoAsistencia.Pendiente, ct))
            throw new InvalidOperationException("El alumno ya registra asistencia en la clase elegida.");
        var reservas = await db.ReservasSesionesClase.Where(x => x.BebeId == bebeId && (x.SesionClaseId == destinoId || (x.InscripcionId == inscripcionId && x.Estado == EstadoReservaSesion.Activa))).ToListAsync(ct);
        if (reservas.Any(x => x.Estado == EstadoReservaSesion.Activa && x.SesionClaseId != sesionId))
            throw new InvalidOperationException("Ya existe otra primera clase activa. Actualizá la pantalla.");
        var nueva = reservas.FirstOrDefault(x => x.SesionClaseId == destinoId);
        if (nueva is not null && nueva.InscripcionId != inscripcionId)
            throw new InvalidOperationException("Existe otra reserva para este alumno en la clase elegida.");
        await AnularRecuperoPorCambioAsistenciaAsync(db, origen, bebeId, ct);
        var asistencia = await db.Asistencias.FirstOrDefaultAsync(x => x.SesionClaseId == sesionId && x.BebeId == bebeId, ct);
        if (asistencia is null)
        {
            asistencia = new Domain.Entities.Asistencia { Id = Guid.NewGuid(), SesionClaseId = sesionId, BebeId = bebeId };
            db.Asistencias.Add(asistencia);
        }
        asistencia.InscripcionId = inscripcionId;
        asistencia.EsRecupero = false;
        asistencia.Estado = EstadoAsistencia.AusenteConAviso;
        asistencia.Observaciones = Normalizar($"{observaciones} | Primera clase reprogramada al {destino.Fecha:dd/MM/yyyy} {destino.HoraInicio:HH:mm}");
        asistencia.RegistradaUtc = DateTimeOffset.UtcNow;
        asistencia.RegistradaPorUsuarioId = usuarioId;
        foreach (var reserva in reservas.Where(x => x.SesionClaseId == sesionId && x.InscripcionId == inscripcionId))
        {
            reserva.Estado = EstadoReservaSesion.Cancelada;
            reserva.ModificadoUtc = DateTimeOffset.UtcNow;
            reserva.ModificadoPorUsuarioId = usuarioId;
        }
        if (nueva is null)
        {
            nueva = new ReservaSesionClase { Id = Guid.NewGuid(), SesionClaseId = destinoId, BebeId = bebeId, InscripcionId = inscripcionId, Tipo = TipoReservaSesion.InscripcionInicial, CreadoPorUsuarioId = usuarioId };
            db.ReservasSesionesClase.Add(nueva);
        }
        nueva.Estado = EstadoReservaSesion.Activa;
        nueva.ModificadoUtc = DateTimeOffset.UtcNow;
        nueva.ModificadoPorUsuarioId = usuarioId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task MarcarAsync(
        RegistrarAsistenciaDto dto,
        CancellationToken ct = default)
    {
        ValidarEstado(
            dto.Estado);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var sesion =
            await ObtenerSesionEditableAsync(
                db,
                dto.SesionClaseId,
                ct);


        await MarcarInternoAsync(
            db,
            sesion,
            dto.BebeId,
            dto.Estado,
            dto.Observaciones,
            dto.UsuarioId,
            ct);


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // MARCACIÓN MASIVA
    // =========================================================

    public async Task MarcarMasivoAsync(
        RegistrarAsistenciaMasivaDto dto,
        CancellationToken ct = default)
    {
        if (dto.Alumnos.Count == 0)
            return;


        var duplicados =
            dto.Alumnos
                .GroupBy(x => x.BebeId)
                .Any(x => x.Count() > 1);


        if (duplicados)
        {
            throw new InvalidOperationException(
                "Hay bebés repetidos en la marcación de asistencia.");
        }


        foreach (var alumno in dto.Alumnos)
        {
            ValidarEstado(
                alumno.Estado);
        }


        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database.BeginTransactionAsync(ct);


        var sesion =
            await ObtenerSesionEditableAsync(
                db,
                dto.SesionClaseId,
                ct);


        foreach (var alumno in dto.Alumnos)
        {
            await MarcarInternoAsync(
                db,
                sesion,
                alumno.BebeId,
                alumno.Estado,
                alumno.Observaciones,
                dto.UsuarioId,
                ct);
        }


        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }


    // =========================================================
    // LÓGICA CENTRAL DE MARCACIÓN
    // =========================================================

    private  async Task MarcarInternoAsync(
        AppDbContext db,
        SesionClase sesion,
        Guid bebeId,
        EstadoAsistencia estado,
        string? observaciones,
        Guid? usuarioId,
        CancellationToken ct)
    {
        var esperado =
            await ObtenerAlumnoEsperadoAsync(
                db,
                sesion,
                bebeId,
                ct);


        if (esperado is null)
        {
            throw new InvalidOperationException(
    "El bebé no pertenece a esta clase, no posee una reserva inicial ni tiene un recupero reservado.");
        }


        if (estado == EstadoAsistencia.AusenteConAviso && !esperado.EsRecupero &&
            await db.ReservasSesionesClase.AnyAsync(x => x.SesionClaseId == sesion.Id && x.BebeId == bebeId
                && x.Tipo == TipoReservaSesion.InscripcionInicial && x.Estado == EstadoReservaSesion.Activa, ct))
            throw new InvalidOperationException("Para una primera clase con aviso, elegí la nueva fecha desde Asistencia.");

        var asistencia =
            await db.Asistencias
                .FirstOrDefaultAsync(
                    x =>
                        x.SesionClaseId ==
                            sesion.Id
                        &&
                        x.BebeId ==
                            bebeId,
                    ct);


        // Reconciliar incluso al guardar nuevamente una asistencia ya corregida.
        if (estado != EstadoAsistencia.AusenteConAviso)
        {
            await AnularRecuperoPorCambioAsistenciaAsync(db, sesion, bebeId, ct);
        }


        // =====================================================
        // CREAR ASISTENCIA SI NO EXISTE
        // =====================================================

        if (asistencia is null)
        {
            asistencia =
                new Domain.Entities.Asistencia
                {
                    Id =
                        Guid.NewGuid(),

                    SesionClaseId =
                        sesion.Id,

                    BebeId =
                        bebeId,

                    InscripcionId =
                        esperado.InscripcionId,

                    Estado =
                        estado,

                    EsRecupero =
                        esperado.EsRecupero,

                    Observaciones =
                        Normalizar(observaciones),

                    RegistradaUtc =
                        estado ==
                            EstadoAsistencia.Pendiente
                            ? null
                            : DateTimeOffset.UtcNow,

                    RegistradaPorUsuarioId =
                        estado ==
                            EstadoAsistencia.Pendiente
                            ? null
                            : usuarioId
                };


            db.Asistencias.Add(
                asistencia);
        }
        else
        {
            asistencia.InscripcionId =
                esperado.InscripcionId;

            asistencia.EsRecupero =
                esperado.EsRecupero;

            asistencia.Estado =
                estado;

            asistencia.Observaciones =
                Normalizar(observaciones);


            if (estado ==
                EstadoAsistencia.Pendiente)
            {
                asistencia.RegistradaUtc =
                    null;

                asistencia.RegistradaPorUsuarioId =
                    null;
            }
            else
            {
                asistencia.RegistradaUtc =
                    DateTimeOffset.UtcNow;

                asistencia.RegistradaPorUsuarioId =
                    usuarioId;
            }
        }


        // =====================================================
        // AUSENTE CON AVISO
        // =====================================================

        if (
            estado ==
                EstadoAsistencia.AusenteConAviso
            &&
            !esperado.EsRecupero)
        {
            await GenerarRecuperoAusenciaConAvisoAsync(
                db,
                sesion,
                asistencia,
                esperado,
                ct);
        }


        /*
         * IMPORTANTE:
         *
         * Si el alumno estaba asistiendo como recupero
         * y falta a esa clase, NO generamos otro recupero.
         *
         * Evitamos así una cadena infinita:
         *
         * recupero -> falta -> recupero -> falta...
         */
    }


    // =========================================================
    // GENERAR RECUPERO POR AUSENCIA CON AVISO
    // =========================================================

    private async Task GenerarRecuperoAusenciaConAvisoAsync(
            AppDbContext db,
            SesionClase sesion,
            Domain.Entities.Asistencia asistencia,
            AlumnoEsperado esperado,
            CancellationToken ct)
    {
        var recuperacion =
            await db.Recuperaciones
                .FirstOrDefaultAsync(
                    x =>
                        x.BebeId ==
                            esperado.BebeId
                        &&
                        x.SesionOrigenId ==
                            sesion.Id
                        &&
                        x.Tipo ==
                            TipoRecuperacion
                                .AusenciaConAviso,
                    ct);

var configuracion =
    await configuracionService.ObtenerAsync(ct);
       var vencimiento =
    sesion.Fecha.AddDays(
        configuracion.DiasVigenciaRecupero);


        if (recuperacion is null)
        {
            recuperacion =
                new Recuperacion
                {
                    Id =
                        Guid.NewGuid(),

                    BebeId =
                        esperado.BebeId,

                    InscripcionId =
                        esperado.InscripcionId,

                    SesionOrigenId =
                        sesion.Id,

                    AsistenciaOrigenId =
                        asistencia.Id,

                    Tipo =
                        TipoRecuperacion
                            .AusenciaConAviso,

                    FechaGeneracion =
                        sesion.Fecha,

                    FechaVencimiento =
                        vencimiento,

                    Estado =
                        EstadoRecuperacion.Disponible
                };


            db.Recuperaciones.Add(
                recuperacion);

            return;
        }


        /*
         * Si ya fue utilizado, no hacemos nada:
         * el beneficio ya fue consumido.
         */
        if (recuperacion.Estado is EstadoRecuperacion.Utilizada
            or EstadoRecuperacion.Reservada
            or EstadoRecuperacion.Disponible
            or EstadoRecuperacion.Vencida)
        {
            return;
        }


        /*
         * Puede haber sido anulado porque alguien
         * corrigió la asistencia y ahora volvió a
         * colocar AusenteConAviso.
         */
        recuperacion.InscripcionId =
            esperado.InscripcionId;

        recuperacion.AsistenciaOrigenId =
            asistencia.Id;

        recuperacion.FechaGeneracion =
            sesion.Fecha;

        recuperacion.FechaVencimiento =
            vencimiento;

        recuperacion.SesionUsoId =
            null;

        recuperacion.UtilizadaUtc =
            null;

        recuperacion.Estado =
            EstadoRecuperacion.Disponible;
    }


    // =========================================================
    // ANULAR RECUPERO SI CORRIGEN LA ASISTENCIA
    // =========================================================

    private static async Task
        AnularRecuperoPorCambioAsistenciaAsync(
            AppDbContext db,
            SesionClase sesion,
            Guid bebeId,
            CancellationToken ct)
    {
        var recuperaciones = await db.Recuperaciones
            .Where(x => x.BebeId == bebeId
                && x.SesionOrigenId == sesion.Id
                && x.Tipo == TipoRecuperacion.AusenciaConAviso
                && x.Estado != EstadoRecuperacion.Anulada)
            .ToListAsync(ct);

        if (recuperaciones.Any(x => x.Estado == EstadoRecuperacion.Utilizada))
            throw new InvalidOperationException(
                "No puede modificar esta ausencia porque el recupero generado ya fue utilizado.");

        foreach (var recuperacion in recuperaciones)
        {
            if (recuperacion.SesionUsoId is Guid sesionUsoId &&
                await db.Asistencias.AnyAsync(x => x.SesionClaseId == sesionUsoId
                    && x.BebeId == bebeId && x.EsRecupero
                    && x.Estado != EstadoAsistencia.Pendiente, ct))
            {
                throw new InvalidOperationException(
                    "El recupero tiene una asistencia registrada. Revisá esa clase antes de corregir la ausencia original.");
            }

            var solicitudes = await db.ListaEspera
                .Where(x => x.RecuperacionId == recuperacion.Id
                    && (x.Estado == EstadoListaEspera.Activa
                        || x.Estado == EstadoListaEspera.Notificada
                        || x.Estado == EstadoListaEspera.Asignada))
                .ToListAsync(ct);

            foreach (var solicitud in solicitudes)
                solicitud.Estado = EstadoListaEspera.Cancelada;

            recuperacion.Estado = EstadoRecuperacion.Anulada;
            recuperacion.SesionUsoId = null;
            recuperacion.UtilizadaUtc = null;
        }
    }


    // =========================================================
    // OBTENER ALUMNO ESPERADO
    // =========================================================

    private static async Task<AlumnoEsperado?>
        ObtenerAlumnoEsperadoAsync(
            AppDbContext db,
            SesionClase sesion,
            Guid bebeId,
            CancellationToken ct)
    {
        // -----------------------------------------------------
        // Primero buscamos inscripción regular.
        // -----------------------------------------------------

        var relaciones =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Include(x => x.Inscripcion)
                    .ThenInclude(x => x.Bebe)
                .Where(x =>
                    x.TurnoId ==
                        sesion.TurnoId
                    &&
                    x.Inscripcion.BebeId ==
                        bebeId)
                .ToListAsync(ct);


        var primeraExcepcional = await PrimeraClaseExcepcional.ObtenerFechasAsync(
            db, relaciones.Select(x => x.InscripcionId), sesion.Fecha, ct);

        var regular =
            relaciones
                .FirstOrDefault(x =>
                    !PrimeraClaseExcepcional.TodaviaNoComenzo(
                        primeraExcepcional, x.InscripcionId, sesion.Fecha)
                    && InscripcionValidaEnFecha(
                        x.Inscripcion,
                        sesion.Fecha));


        if (regular is not null)
        {
            return new AlumnoEsperado(
                bebeId,
                regular.InscripcionId,

                $"{regular.Inscripcion.Bebe.Nombre} " +
                $"{regular.Inscripcion.Bebe.Apellido}",

                false);
        }

// -----------------------------------------------------
// Si no es alumno regular, verificamos si posee una
// reserva puntual de inscripción inicial.
// -----------------------------------------------------

var reservaInicial =
    await db.ReservasSesionesClase
        .AsNoTracking()

        .Include(x =>
            x.Bebe)

        .FirstOrDefaultAsync(
            x =>
                x.BebeId ==
                    bebeId
                &&
                x.SesionClaseId ==
                    sesion.Id
                &&
                x.Estado ==
                    EstadoReservaSesion.Activa,
            ct);


if (reservaInicial is not null)
{
    return new AlumnoEsperado(
        reservaInicial.BebeId,
        reservaInicial.InscripcionId,

        $"{reservaInicial.Bebe.Nombre} " +
        $"{reservaInicial.Bebe.Apellido}",

        false);
}


        // -----------------------------------------------------
        // Si no pertenece al turno, verificamos recupero.
        // -----------------------------------------------------

        var recuperacion =
            await db.Recuperaciones
                .AsNoTracking()
                .Include(x => x.Bebe)
                .FirstOrDefaultAsync(
                    x =>
                        x.BebeId ==
                            bebeId
                        &&
                        x.SesionUsoId ==
                            sesion.Id
                        &&
                        (
                            x.Estado ==
                                EstadoRecuperacion.Reservada
                            ||
                            x.Estado ==
                                EstadoRecuperacion.Utilizada
                        ),
                    ct);


        if (recuperacion is null)
            return null;


        return new AlumnoEsperado(
            recuperacion.BebeId,
            recuperacion.InscripcionId,

            $"{recuperacion.Bebe.Nombre} " +
            $"{recuperacion.Bebe.Apellido}",

            true);
    }


    // =========================================================
    // SESIÓN EDITABLE
    // =========================================================

    private static async Task<SesionClase>
        ObtenerSesionEditableAsync(
            AppDbContext db,
            Guid sesionId,
            CancellationToken ct)
    {
        var sesion =
            await db.SesionesClase
                .Include(x => x.Turno)
                .FirstOrDefaultAsync(
                    x => x.Id == sesionId,
                    ct)
            ?? throw new InvalidOperationException(
                "La clase indicada no existe.");


        if (sesion.Estado ==
            EstadoSesionClase.Cancelada)
        {
            throw new InvalidOperationException(
                "No puede registrar asistencia sobre una clase cancelada.");
        }


        return sesion;
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void ValidarEstado(
        EstadoAsistencia estado)
    {
        if (estado is not
            (
                EstadoAsistencia.Pendiente
                or
                EstadoAsistencia.Presente
                or
                EstadoAsistencia.Ausente
                or
                EstadoAsistencia.AusenteConAviso
            ))
        {
            throw new InvalidOperationException(
                "El estado de asistencia no es válido.");
        }
    }


    // Primera asistencia real del alumno: la fecha del primer turno semanal
    // configurado a partir del inicio, salvo una primera clase puntual.
    private static bool EsPrimeraClaseEnHorarioFijo(
        DateOnly fechaInicio, IReadOnlyCollection<int> diasSemana, DateOnly fecha)
    {
        if (diasSemana.Count == 0 || fecha < fechaInicio)
            return false;

        var inicioSemana = (int)fechaInicio.DayOfWeek;
        if (inicioSemana == 0) inicioSemana = 7;

        var primeraFecha = diasSemana
            .Select(dia => fechaInicio.AddDays((dia - inicioSemana + 7) % 7))
            .Min();

        return fecha == primeraFecha;
    }

    private static bool InscripcionValidaEnFecha(
        Inscripcion inscripcion,
        DateOnly fecha)
    {
        if (inscripcion.Estado is
            EstadoInscripcion.Cancelada or
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
// LISTAR CLASES DE UN DÍA
// =========================================================

public async Task<IReadOnlyList<AsistenciaClaseDiaDto>>
    ListarSesionesDiaAsync(
        DateOnly fecha,
        CancellationToken ct = default)
{
    List<Guid> sesionesIds;
    var hoy = FechaArgentina.Hoy;

    await using (
        var db =
            await factory.CreateDbContextAsync(ct))
    {
        sesionesIds =
            await db.SesionesClase
                .AsNoTracking()
                // Conservar clases historicas; no mostrar sesiones de turnos
                // dados de baja para hoy o fechas futuras.
                .Where(x =>
                    x.Fecha == fecha
                    && (fecha < hoy || x.Turno.Estado != EstadoTurno.Baja))
                .OrderBy(x =>
                    x.HoraInicio)
                .Select(x =>
                    x.Id)
                .ToListAsync(ct);
    }


    var resultado =
        new List<AsistenciaClaseDiaDto>();


    foreach (var sesionId in sesionesIds)
    {
        var sesion =
            await ObtenerSesionAsync(
                sesionId,
                ct);


        if (sesion is null)
            continue;


        resultado.Add(
            new AsistenciaClaseDiaDto(
                sesion.SesionClaseId,
                sesion.TurnoId,
                sesion.Turno,
                sesion.Nivel,
                sesion.Fecha,
                sesion.HoraInicio,
                sesion.HoraFin,
                sesion.EstadoSesion,
                sesion.TotalEsperados,
                sesion.Presentes,
                sesion.Ausentes,
                sesion.AusentesConAviso,
                sesion.Pendientes,
                sesion.Profesores
                    ?? Array.Empty<string>()));
    }


    return resultado;
}

    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(texto)
            ? null
            : texto.Trim();
    }


    private sealed record AlumnoEsperado(
        Guid BebeId,
        Guid InscripcionId,
        string NombreCompleto,
        bool EsRecupero,
        bool EsPrimeraClase = false);
}