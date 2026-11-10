using BebesNadadores.Application.Asistencia;
using BebesNadadores.Application.Asistencia.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Infrastructure.Services.Pagos;

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


        var esperados =
            new Dictionary<
                Guid,
                AlumnoEsperado>();


        foreach (var relacion in relaciones)
        {
            var inscripcion =
                relacion.Inscripcion;


            if (!InscripcionValidaEnFecha(
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
                    false);
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

            false);
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
                    x => x.First());


        // =====================================================
        // CUOTAS VENCIDAS POR ALUMNO (MISMA REGLA QUE TURNERA)
        // =====================================================
        // Consultamos todas las cuotas de los alumnos de la sesión de una vez.
        // No cuenta una cuota futura, anulada ni totalmente cancelada.
        // Una cuota parcialmente abonada sigue contando como 1 deuda.
        // El estado se calcula al día de hoy en Argentina, no a la fecha
        // de la sesión, que puede ser anterior o posterior.
        var bebeIds = esperados.Keys.ToList();
        var cuotasVencidasPorBebe = new Dictionary<Guid, int>();

        if (bebeIds.Count > 0)
        {
            var hoy = RegistradorCobros.ObtenerFechaArgentina();
            var cuotas = await db.Cuotas
                .AsNoTracking()
                .Where(c =>
                    bebeIds.Contains(c.Inscripcion.BebeId)
                    && c.Estado != EstadoCuota.Anulada
                    && c.FechaVencimiento < hoy)
                .Select(c => new
                {
                    BebeId = c.Inscripcion.BebeId,
                    c.Importe,
                    ImportePagado = c.Imputaciones.Sum(i => (decimal?)i.Importe) ?? 0m
                })
                .ToListAsync(ct);

            cuotasVencidasPorBebe = cuotas
                .Where(c => c.Importe > c.ImportePagado)
                .GroupBy(c => c.BebeId)
                .ToDictionary(g => g.Key, g => g.Count());
        }

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
                    cuotasVencidasPorBebe.GetValueOrDefault(esperado.BebeId)));
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


        var estadoAnterior =
            asistencia?.Estado
            ?? EstadoAsistencia.Pendiente;


        // =====================================================
        // SI ESTABA AUSENTE CON AVISO Y LO CAMBIAN
        // =====================================================

        if (
            estadoAnterior ==
                EstadoAsistencia.AusenteConAviso
            &&
            estado !=
                EstadoAsistencia.AusenteConAviso
        )
        {
            await AnularRecuperoPorCambioAsistenciaAsync(
                db,
                sesion,
                bebeId,
                ct);
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
        if (recuperacion.Estado ==
            EstadoRecuperacion.Utilizada)
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
        var recuperacion =
            await db.Recuperaciones
                .FirstOrDefaultAsync(
                    x =>
                        x.BebeId ==
                            bebeId
                        &&
                        x.SesionOrigenId ==
                            sesion.Id
                        &&
                        x.Tipo ==
                            TipoRecuperacion
                                .AusenciaConAviso,
                    ct);


        if (recuperacion is null)
            return;


        /*
         * Si ya usó el recupero, no permitimos
         * modificar el hecho que le dio origen.
         */
        if (recuperacion.Estado ==
            EstadoRecuperacion.Utilizada)
        {
            throw new InvalidOperationException(
                "No puede modificar esta ausencia porque el recupero generado ya fue utilizado.");
        }


        recuperacion.Estado =
            EstadoRecuperacion.Anulada;

        recuperacion.SesionUsoId =
            null;

        recuperacion.UtilizadaUtc =
            null;
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


        var regular =
            relaciones
                .FirstOrDefault(x =>
                    InscripcionValidaEnFecha(
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


    await using (
        var db =
            await factory.CreateDbContextAsync(ct))
    {
        sesionesIds =
            await db.SesionesClase
                .AsNoTracking()
                .Where(x =>
                    x.Fecha == fecha)
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
        bool EsRecupero);
}