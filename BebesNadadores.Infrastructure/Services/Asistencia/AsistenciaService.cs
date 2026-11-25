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
            db, relaciones.Select(x => x.InscripcionId), sesion.Fecha, ct, bloquearPendientesSinFecha: true);

        var primerasAnteriores = await PrimeraClaseExcepcional.ObtenerInscripcionesConReservaAnteriorAsync(
            db, inscripcionesIds, sesion.Fecha, ct);

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
                    !primerasAnteriores.Contains(inscripcion.Id)
                        && diasPorInscripcion.TryGetValue(inscripcion.Id, out var dias)
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

        if (sesion.Fecha >= FechaArgentina.Hoy)
        {
            var idsEsperados = esperados.Keys.ToArray();
            var activos = (await db.Bebes.AsNoTracking()
                .Where(b => idsEsperados.Contains(b.Id) && b.Estado == EstadoBebe.Activo)
                .Select(b => b.Id).ToListAsync(ct)).ToHashSet();
            foreach (var id in idsEsperados.Where(id => !activos.Contains(id)))
                esperados.Remove(id);
        }

        // Una inscripción con asistencia real en otra sesión no debe volver al
        // circuito de primera clase, aunque se hayan versionado sus turnos.
        var idsInscripcionesEsperadas = esperados.Values.Select(x => x.InscripcionId).Distinct().ToArray();
        var inscripcionesConOtraClaseRealizada = (await db.Asistencias.AsNoTracking()
            .Where(a => idsInscripcionesEsperadas.Contains(a.InscripcionId) && a.SesionClaseId != sesion.Id
                && a.Estado == EstadoAsistencia.Presente)
            .Select(a => a.InscripcionId).Distinct().ToListAsync(ct)).ToHashSet();

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
                    !esperado.EsRecupero && !inscripcionesConOtraClaseRealizada.Contains(esperado.InscripcionId) &&
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


    private static async Task<SesionClase> ObtenerOrigenPrimeraClaseAsync(AppDbContext db, Guid sesionId, CancellationToken ct)
    {
        return await db.SesionesClase.Include(x => x.Turno).FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("No se encuentra la clase original.");
    }

    private async Task<Guid> ValidarPrimeraClaseAsync(AppDbContext db, SesionClase origen, Guid bebeId, CancellationToken ct)
    {
        if (!await db.Bebes.AnyAsync(b => b.Id == bebeId && b.Estado == EstadoBebe.Activo, ct))
            throw new InvalidOperationException("El bebé no está activo.");

        // La reserva puntual es la fuente de verdad, incluso cuando el bebé
        // todavía no eligió horarios semanales o la sesión original fue cancelada.
        var id = await db.ReservasSesionesClase.AsNoTracking()
            .Where(r => r.BebeId == bebeId && r.SesionClaseId == origen.Id
                && r.Tipo == TipoReservaSesion.InscripcionInicial && r.Estado == EstadoReservaSesion.Activa
                && r.Inscripcion.Estado == EstadoInscripcion.Activa)
            .Select(r => (Guid?)r.InscripcionId).FirstOrDefaultAsync(ct);
        if (!id.HasValue)
        {
            var detalle = await ObtenerSesionAsync(origen.Id, ct);
            var alumno = detalle?.Alumnos.FirstOrDefault(x => x.BebeId == bebeId && x.EsPrimeraClase && !x.EsRecupero);
            id = alumno?.InscripcionId;
        }
        if (!id.HasValue)
            throw new InvalidOperationException("El alumno ya no tiene una primera clase en este horario.");
        var hoy = FechaArgentina.Hoy;
        if (!await db.Inscripciones.AnyAsync(i => i.Id == id.Value && i.BebeId == bebeId
            && i.Estado == EstadoInscripcion.Activa && (i.FechaFin == null || i.FechaFin >= hoy), ct))
            throw new InvalidOperationException("La inscripción original ya no está activa.");
        if (await db.Asistencias.AnyAsync(x => x.InscripcionId == id.Value
            && x.SesionClaseId != origen.Id && x.Estado == EstadoAsistencia.Presente, ct))
            throw new InvalidOperationException("El alumno ya registra una clase realizada en esta inscripción.");
        return id.Value;
    }

    private static TimeOnly HoraArgentina() =>
        TimeOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).DateTime);

    public async Task<IReadOnlyList<OpcionPrimeraClaseDto>> ListarReprogramacionAsync(Guid sesionId, Guid bebeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var origen = await ObtenerOrigenPrimeraClaseAsync(db, sesionId, ct);
        var inscripcionId = await ValidarPrimeraClaseAsync(db, origen, bebeId, ct);
        var inscripcion = await db.Inscripciones.AsNoTracking().SingleAsync(x => x.Id == inscripcionId, ct);
        var hoy = FechaArgentina.Hoy;
        var hora = HoraArgentina();
        var desde = PrimeraClaseAnticipada.FechaMinima(inscripcion.FechaInicio, hoy);
        var hasta = desde.AddDays(60);
        if (inscripcion.FechaFin is DateOnly fin && fin < hasta) hasta = fin;
        var sesiones = await db.SesionesClase.AsNoTracking().Include(x => x.Nivel)
            .Where(x => x.Id != sesionId && x.NivelId == origen.NivelId && x.Fecha >= desde && x.Fecha <= hasta
                && x.Estado == EstadoSesionClase.Programada && x.Turno.Estado == EstadoTurno.Activo
                && (x.Fecha > hoy || x.HoraInicio > hora))
            .OrderBy(x => x.Fecha).ThenBy(x => x.HoraInicio).ToListAsync(ct);
        var ocupaciones = await CalculadoraCupoSesion.CalcularAsync(db, sesiones, ct);
        var ocupadas = await db.Asistencias.AsNoTracking().Where(x => x.BebeId == bebeId && x.Estado != EstadoAsistencia.Pendiente)
            .Select(x => x.SesionClaseId).ToListAsync(ct);
        var reservas = await db.ReservasSesionesClase.AsNoTracking().Where(x => x.BebeId == bebeId && x.Estado == EstadoReservaSesion.Activa)
            .Select(x => x.SesionClaseId).ToListAsync(ct);
        var recuperos = await db.Recuperaciones.AsNoTracking().Where(x => x.BebeId == bebeId && x.SesionUsoId.HasValue
            && (x.Estado == EstadoRecuperacion.Reservada || x.Estado == EstadoRecuperacion.Utilizada))
            .Select(x => x.SesionUsoId!.Value).ToListAsync(ct);
        var turnosPropios = await db.InscripcionesTurnos.AsNoTracking().Where(t => t.InscripcionId == inscripcionId)
            .Select(t => t.TurnoId).ToListAsync(ct);
        var primeras = await PrimeraClaseExcepcional.ObtenerFechasAsync(db, new[] { inscripcionId }, desde, ct);
        bool LugarPropio(SesionClase x) => turnosPropios.Contains(x.TurnoId)
            && CalculadoraCupoSesion.InscripcionValidaEnFecha(inscripcion, x.Fecha)
            && !PrimeraClaseExcepcional.TodaviaNoComenzo(primeras, inscripcionId, x.Fecha);
        return sesiones.Where(x => !ocupadas.Contains(x.Id) && !reservas.Contains(x.Id) && !recuperos.Contains(x.Id))
            .Select(x => new OpcionPrimeraClaseDto(x.Id, x.Fecha, x.HoraInicio, x.Nivel.Nombre, ocupaciones[x.Id].Libres, LugarPropio(x))).ToList();
    }

    public Task ReprogramarPrimeraClaseAsync(Guid sesionId, Guid bebeId, Guid destinoId, string? observaciones, Guid? usuarioId, CancellationToken ct = default)
        => CambiarReservaPrimeraClaseAsync(sesionId, bebeId, destinoId, observaciones, usuarioId, registrarAusencia: true, ct: ct);

    public Task MoverReservaPrimeraClaseAsync(Guid sesionId, Guid bebeId, Guid destinoId, Guid? usuarioId, CancellationToken ct = default)
        => CambiarReservaPrimeraClaseAsync(sesionId, bebeId, destinoId, null, usuarioId, registrarAusencia: false, ct: ct);

    private async Task CambiarReservaPrimeraClaseAsync(Guid sesionId, Guid bebeId, Guid destinoId, string? observaciones, Guid? usuarioId, bool registrarAusencia, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var origen = await ObtenerOrigenPrimeraClaseAsync(db, sesionId, ct);
        var inscripcionId = await ValidarPrimeraClaseAsync(db, origen, bebeId, ct);
        var destino = await ObtenerSesionEditableAsync(db, destinoId, ct);
        var inscripcion = await db.Inscripciones.SingleAsync(x => x.Id == inscripcionId, ct);
        var hoy = FechaArgentina.Hoy;
        var desde = PrimeraClaseAnticipada.FechaMinima(inscripcion.FechaInicio, hoy);
        if (destino.Id == origen.Id || destino.NivelId != origen.NivelId || destino.Estado != EstadoSesionClase.Programada
            || destino.Turno.Estado != EstadoTurno.Activo || destino.Fecha < desde || destino.Fecha > desde.AddDays(60)
            || (destino.Fecha == hoy && destino.HoraInicio <= HoraArgentina())
            || (inscripcion.FechaFin.HasValue && inscripcion.FechaFin.Value < destino.Fecha))
            throw new InvalidOperationException("La nueva clase no está disponible para esta inscripción.");
        var primerasDestino = await PrimeraClaseExcepcional.ObtenerFechasAsync(db, new[] { inscripcionId }, destino.Fecha, ct);
        var lugarPropio = CalculadoraCupoSesion.InscripcionValidaEnFecha(inscripcion, destino.Fecha)
            && !PrimeraClaseExcepcional.TodaviaNoComenzo(primerasDestino, inscripcionId, destino.Fecha)
            && await db.InscripcionesTurnos.AnyAsync(t => t.InscripcionId == inscripcionId && t.TurnoId == destino.TurnoId, ct);

        if (await db.Asistencias.AnyAsync(x => x.SesionClaseId == destinoId && x.BebeId == bebeId && x.Estado != EstadoAsistencia.Pendiente, ct))
            throw new InvalidOperationException("El alumno ya registra asistencia en la clase elegida.");
        if (await db.Recuperaciones.AnyAsync(x => x.SesionUsoId == destinoId && x.BebeId == bebeId
            && (x.Estado == EstadoRecuperacion.Reservada || x.Estado == EstadoRecuperacion.Utilizada), ct))
            throw new InvalidOperationException("El alumno ya tiene un recupero en la clase elegida.");
        var reservas = await db.ReservasSesionesClase.Where(x => x.BebeId == bebeId
            && (x.SesionClaseId == destinoId || (x.InscripcionId == inscripcionId && x.Estado == EstadoReservaSesion.Activa))).ToListAsync(ct);
        if (reservas.Any(x => x.Estado == EstadoReservaSesion.Activa && x.SesionClaseId != sesionId))
            throw new InvalidOperationException("Ya existe otra reserva activa. Actualizá la pantalla.");
        var nueva = reservas.FirstOrDefault(x => x.SesionClaseId == destinoId);
        if (nueva is not null && (nueva.InscripcionId != inscripcionId || nueva.Tipo != TipoReservaSesion.InscripcionInicial))
            throw new InvalidOperationException("Existe otra reserva para este alumno en la clase elegida.");

        var asistencia = await db.Asistencias.FirstOrDefaultAsync(x => x.SesionClaseId == sesionId && x.BebeId == bebeId, ct);
        if (!registrarAusencia && asistencia?.Estado == EstadoAsistencia.Presente)
            throw new InvalidOperationException("La primera clase figura como realizada. Rectificá primero la asistencia si fue un error.");
        if (!registrarAusencia && asistencia?.Estado == EstadoAsistencia.Ausente)
            throw new InvalidOperationException("La clase figura como ausente sin aviso. Revisá la asistencia antes de reprogramar.");
        if (!registrarAusencia && asistencia?.Estado != EstadoAsistencia.AusenteConAviso
            && (origen.Fecha < hoy || (origen.Fecha == hoy && origen.HoraInicio <= HoraArgentina())))
            throw new InvalidOperationException("Registrá primero la ausencia con aviso en Asistencia.");

        // Solo registrar ausencia cuando fue solicitada desde Asistencia, no
        // cuando una reserva futura se cambia antes de que se dicte la clase.
        if (registrarAusencia || asistencia?.Estado == EstadoAsistencia.AusenteConAviso)
        {
            await AnularRecuperoPorCambioAsistenciaAsync(db, origen, bebeId, ct);
            if (asistencia is null)
            {
                asistencia = new Domain.Entities.Asistencia { Id = Guid.NewGuid(), SesionClaseId = sesionId, BebeId = bebeId };
                db.Asistencias.Add(asistencia);
            }
            var notas = string.IsNullOrWhiteSpace(observaciones) ? asistencia.Observaciones : observaciones;
            asistencia.InscripcionId = inscripcionId;
            asistencia.EsRecupero = false;
            asistencia.Estado = EstadoAsistencia.AusenteConAviso;
            asistencia.Observaciones = Normalizar($"{notas} | Primera clase reprogramada al {destino.Fecha:dd/MM/yyyy} {destino.HoraInicio:HH:mm}");
            if (registrarAusencia || !asistencia.RegistradaUtc.HasValue)
            {
                asistencia.RegistradaUtc = DateTimeOffset.UtcNow;
                asistencia.RegistradaPorUsuarioId = usuarioId;
            }
            asistencia.ModificadoUtc = DateTimeOffset.UtcNow;
            asistencia.ModificadoPorUsuarioId = usuarioId;
        }
        foreach (var reserva in reservas.Where(x => x.SesionClaseId == sesionId && x.InscripcionId == inscripcionId && x.Tipo == TipoReservaSesion.InscripcionInicial))
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


        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await MarcarInternoAsync(
            db,
            sesion,
            dto.BebeId,
            dto.Estado,
            dto.Observaciones,
            dto.UsuarioId,
            ct);


        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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
            await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);


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


        var esPrimeraAusente = estado == EstadoAsistencia.AusenteConAviso && !esperado.EsRecupero
            && (esperado.EsPrimeraClase || await db.ReservasSesionesClase.AnyAsync(x =>
                x.SesionClaseId == sesion.Id && x.BebeId == bebeId && x.InscripcionId == esperado.InscripcionId
                && x.Tipo == TipoReservaSesion.InscripcionInicial && x.Estado == EstadoReservaSesion.Activa, ct))
            && await db.Inscripciones.AnyAsync(i => i.Id == esperado.InscripcionId && i.Estado == EstadoInscripcion.Activa, ct)
            && !await db.Asistencias.AnyAsync(a => a.InscripcionId == esperado.InscripcionId
                && a.SesionClaseId != sesion.Id && a.Estado == EstadoAsistencia.Presente, ct);

        if (esPrimeraAusente)
        {
            var reserva = await db.ReservasSesionesClase.FirstOrDefaultAsync(r => r.BebeId == bebeId && r.SesionClaseId == sesion.Id, ct);
            if (reserva is not null && (reserva.InscripcionId != esperado.InscripcionId || reserva.Tipo != TipoReservaSesion.InscripcionInicial))
                throw new InvalidOperationException("La reserva de primera clase no corresponde a esta inscripción.");
            if (await db.ReservasSesionesClase.AnyAsync(r => r.InscripcionId == esperado.InscripcionId && r.SesionClaseId != sesion.Id
                && r.Tipo == TipoReservaSesion.InscripcionInicial && r.Estado == EstadoReservaSesion.Activa, ct))
                throw new InvalidOperationException("Ya existe otra primera clase reservada. Actualizá la pantalla.");
            if (reserva is null)
            {
                reserva = new ReservaSesionClase { Id = Guid.NewGuid(), BebeId = bebeId, InscripcionId = esperado.InscripcionId,
                    SesionClaseId = sesion.Id, Tipo = TipoReservaSesion.InscripcionInicial, CreadoPorUsuarioId = usuarioId };
                db.ReservasSesionesClase.Add(reserva);
            }
            reserva.Estado = EstadoReservaSesion.Activa;
            reserva.ModificadoUtc = DateTimeOffset.UtcNow;
            reserva.ModificadoPorUsuarioId = usuarioId;
        }

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
        if (estado != EstadoAsistencia.AusenteConAviso || esPrimeraAusente)
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
            !esperado.EsRecupero && !esPrimeraAusente)
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
                        (x.SesionOrigenId == sesion.Id || (x.SesionOrigenId == null && x.AsistenciaOrigenId == asistencia.Id))
                        && x.Tipo == TipoRecuperacion.AusenciaConAviso,
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
        var asistenciaIds = db.Asistencias.Where(a => a.BebeId == bebeId && a.SesionClaseId == sesion.Id)
            .Select(a => a.Id);
        var recuperaciones = await db.Recuperaciones
            .Where(x => x.BebeId == bebeId
                && (x.SesionOrigenId == sesion.Id || (x.SesionOrigenId == null && x.AsistenciaOrigenId.HasValue && asistenciaIds.Contains(x.AsistenciaOrigenId.Value)))
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
        if (sesion.Fecha >= FechaArgentina.Hoy && !await db.Bebes.AsNoTracking()
            .AnyAsync(b => b.Id == bebeId && b.Estado == EstadoBebe.Activo, ct))
            return null;

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
            db, relaciones.Select(x => x.InscripcionId), sesion.Fecha, ct, bloquearPendientesSinFecha: true);

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
            var dias = await db.InscripcionesTurnos.AsNoTracking()
                .Where(t => t.InscripcionId == regular.InscripcionId)
                .Select(t => (int)t.Turno.DiaSemana).ToListAsync(ct);
            var anteriores = await PrimeraClaseExcepcional.ObtenerInscripcionesConReservaAnteriorAsync(db, new[] { regular.InscripcionId }, sesion.Fecha, ct);
            var esPrimera = !anteriores.Contains(regular.InscripcionId)
                && EsPrimeraClaseEnHorarioFijo(regular.Inscripcion.FechaInicio, dias, sesion.Fecha);
            return new AlumnoEsperado(
                bebeId,
                regular.InscripcionId,

                $"{regular.Inscripcion.Bebe.Nombre} " +
                $"{regular.Inscripcion.Bebe.Apellido}",

                false, esPrimera);
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

        false, reservaInicial.Tipo == TipoReservaSesion.InscripcionInicial);
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