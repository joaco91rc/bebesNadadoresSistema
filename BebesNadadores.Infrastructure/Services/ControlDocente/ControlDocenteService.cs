using BebesNadadores.Application.ControlDocente;
using BebesNadadores.Application.ControlDocente.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.ControlDocente;

public sealed class ControlDocenteService(
    IDbContextFactory<AppDbContext> factory)
    : IControlDocenteService
{
    // =========================================================
    // OBTENER DÍA
    // =========================================================

    public async Task<ControlDocenteDiaDto>
        ObtenerDiaAsync(
            DateOnly fecha,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var sesiones =
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

                .Where(x =>
                    x.Fecha == fecha)

                .OrderBy(x =>
                    x.HoraInicio)

                .ToListAsync(ct);


        var clases =
            sesiones
                .Select(sesion =>
                    new ClaseControlDocenteDto(
                        sesion.Id,
                        sesion.TurnoId,
                        sesion.Turno.Nombre,
                        sesion.Nivel.Nombre,
                        sesion.Fecha,
                        sesion.HoraInicio,
                        sesion.HoraFin,
                        sesion.Estado,

                        sesion.Profesores
                            .OrderByDescending(x =>
                                x.EsPrincipal)

                            .ThenBy(x =>
                                x.Profesor.Apellido)

                            .ThenBy(x =>
                                x.Profesor.Nombre)

                            .Select(x =>
                                new ProfesorControlDocenteDto(
                                    x.ProfesorId,

                                    $"{x.Profesor.Nombre} {x.Profesor.Apellido}"
                                        .Trim(),

                                    x.EsPrincipal,
                                    x.EsReemplazo,
                                    x.ControlConfirmado,
                                    x.DictoClase))

                            .ToList()))

                .ToList();


        /*
         * No contamos clases canceladas para el control docente.
         */
        var profesores =
            clases
                .Where(x =>
                    x.Estado !=
                    EstadoSesionClase.Cancelada)

                .SelectMany(x =>
                    x.Profesores)

                .ToList();


        var confirmados =
            profesores.Count(x =>
                x.ControlConfirmado);


        return new ControlDocenteDiaDto(
            fecha,
            clases.Count,
            profesores.Count,
            confirmados,
            profesores.Count -
                confirmados,
            clases);
    }


    // =========================================================
    // CONFIRMAR PROFESOR
    // =========================================================

    public async Task ConfirmarProfesorAsync(
        ConfirmarProfesorClaseDto dto,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var asignacion =
            await db.SesionesClaseProfesores

                .Include(x =>
                    x.SesionClase)

                .FirstOrDefaultAsync(
                    x =>
                        x.SesionClaseId ==
                            dto.SesionClaseId
                        &&
                        x.ProfesorId ==
                            dto.ProfesorId,
                    ct)

            ?? throw new InvalidOperationException(
                "El profesor no está asignado a la clase indicada.");


        if (asignacion.SesionClase.Estado ==
            EstadoSesionClase.Cancelada)
        {
            throw new InvalidOperationException(
                "No se puede registrar control docente sobre una clase cancelada.");
        }


        ValidarFecha(
            asignacion.SesionClase.Fecha);


        asignacion.ControlConfirmado =
            true;

        asignacion.DictoClase =
            dto.DictoClase;

        asignacion.ConfirmadoUtc =
            DateTimeOffset.UtcNow;

        asignacion.ConfirmadoPorUsuarioId =
            dto.UsuarioId;


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // CONFIRMAR CLASE COMPLETA
    // =========================================================

    public async Task ConfirmarClaseCompletaAsync(
        Guid sesionClaseId,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var sesion =
            await db.SesionesClase

                .Include(x =>
                    x.Profesores)

                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                        sesionClaseId,
                    ct)

            ?? throw new InvalidOperationException(
                "La clase indicada no existe.");


        if (sesion.Estado ==
            EstadoSesionClase.Cancelada)
        {
            throw new InvalidOperationException(
                "No se puede confirmar una clase cancelada.");
        }


        ValidarFecha(
            sesion.Fecha);


        if (sesion.Profesores.Count == 0)
        {
            throw new InvalidOperationException(
                "La clase no tiene profesores asignados.");
        }


        var ahora =
            DateTimeOffset.UtcNow;


        foreach (var profesor in
            sesion.Profesores)
        {
            profesor.ControlConfirmado =
                true;

            profesor.DictoClase =
                true;

            profesor.ConfirmadoUtc =
                ahora;

            profesor.ConfirmadoPorUsuarioId =
                usuarioId;
        }


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // CONFIRMAR TODO EL DÍA
    // =========================================================

    public async Task ConfirmarDiaCompletoAsync(
        DateOnly fecha,
        Guid? usuarioId,
        CancellationToken ct = default)
    {
        ValidarFecha(
            fecha);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var sesiones =
            await db.SesionesClase

                .Include(x =>
                    x.Profesores)

                .Where(x =>
                    x.Fecha == fecha
                    &&
                    x.Estado !=
                        EstadoSesionClase.Cancelada)

                .ToListAsync(ct);


        if (sesiones.Count == 0)
        {
            throw new InvalidOperationException(
                "No existen clases para confirmar en la fecha seleccionada.");
        }


        var ahora =
            DateTimeOffset.UtcNow;


        foreach (var sesion in sesiones)
        {
            foreach (var profesor in
                sesion.Profesores)
            {
                profesor.ControlConfirmado =
                    true;

                profesor.DictoClase =
                    true;

                profesor.ConfirmadoUtc =
                    ahora;

                profesor.ConfirmadoPorUsuarioId =
                    usuarioId;
            }
        }


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // VALIDACIÓN FECHA
    // =========================================================

    private static void ValidarFecha(
        DateOnly fecha)
    {
        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        if (fecha > hoy)
        {
            throw new InvalidOperationException(
                "No se puede confirmar asistencia docente de una fecha futura.");
        }
    }

    public async Task RegistrarReemplazoAsync(
    RegistrarReemplazoDocenteDto dto,
    CancellationToken ct = default)
{
    if (dto.ProfesorAusenteId ==
        dto.ProfesorQueDictoId)
    {
        throw new InvalidOperationException(
            "El profesor que reemplaza no puede ser el mismo que no dictó la clase.");
    }


    await using var db =
        await factory.CreateDbContextAsync(ct);


    await using var transaction =
        await db.Database.BeginTransactionAsync(ct);


    var sesion =
        await db.SesionesClase

            .Include(x =>
                x.Profesores)

            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                    dto.SesionClaseId,
                ct)

        ?? throw new InvalidOperationException(
            "La clase indicada no existe.");


    if (sesion.Estado ==
        EstadoSesionClase.Cancelada)
    {
        throw new InvalidOperationException(
            "No se puede registrar un reemplazo en una clase cancelada.");
    }


    ValidarFecha(
        sesion.Fecha);


    // =========================================================
    // PROFESOR QUE NO DICTÓ
    // =========================================================

    var profesorAusente =
        sesion.Profesores
            .FirstOrDefault(x =>
                x.ProfesorId ==
                dto.ProfesorAusenteId)

        ?? throw new InvalidOperationException(
            "El profesor indicado no estaba asignado a esta clase.");


    // =========================================================
    // VALIDAR PROFESOR REEMPLAZANTE
    // =========================================================

    var reemplazanteExiste =
        await db.Profesores
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id ==
                        dto.ProfesorQueDictoId
                    &&
                    x.Activo,
                ct);


    if (!reemplazanteExiste)
    {
        throw new InvalidOperationException(
            "El profesor seleccionado no existe o está inactivo.");
    }


    // =========================================================
    // SUPERPOSICIÓN DEL REEMPLAZANTE
    // =========================================================

    await ValidarSuperposicionProfesoresSesionAsync(
        db,
        sesion.Id,
        sesion.Fecha,
        sesion.HoraInicio,
        sesion.DuracionMinutos,
        new[]
        {
            dto.ProfesorQueDictoId
        },
        ct);


    var ahora =
        DateTimeOffset.UtcNow;


    // =========================================================
    // MARCAR AL ORIGINAL COMO "NO DICTÓ"
    // =========================================================

    profesorAusente.ControlConfirmado =
        true;

    profesorAusente.DictoClase =
        false;

    profesorAusente.ConfirmadoUtc =
        ahora;

    profesorAusente.ConfirmadoPorUsuarioId =
        dto.UsuarioId;


    // =========================================================
    // ¿EL QUE DICTÓ YA ESTABA ASIGNADO?
    // =========================================================

/*
 * Puede existir un reemplazo previamente cargado
 * para este profesor.
 *
 * Si ahora eligieron otro, quitamos el anterior.
 */
var reemplazosAnteriores =
    sesion.Profesores

        .Where(x =>
            x.EsReemplazo
            &&
            x.ReemplazaAProfesorId ==
                dto.ProfesorAusenteId
            &&
            x.ProfesorId !=
                dto.ProfesorQueDictoId)

        .ToList();


foreach (var reemplazoAnterior in
    reemplazosAnteriores)
{
    db.SesionesClaseProfesores
        .Remove(
            reemplazoAnterior);

    sesion.Profesores
        .Remove(
            reemplazoAnterior);
}


    var profesorQueDicto =
        sesion.Profesores
            .FirstOrDefault(x =>
                x.ProfesorId ==
                dto.ProfesorQueDictoId);


   if (profesorQueDicto is not null)
{
    profesorQueDicto.ControlConfirmado =
        true;

    profesorQueDicto.DictoClase =
        true;

    profesorQueDicto.ConfirmadoUtc =
        ahora;

    profesorQueDicto.ConfirmadoPorUsuarioId =
        dto.UsuarioId;


    /*
     * Si ya era un reemplazo cargado previamente,
     * aseguramos a quién reemplaza.
     */
    if (profesorQueDicto.EsReemplazo)
    {
        profesorQueDicto.ReemplazaAProfesorId =
            dto.ProfesorAusenteId;
    }
}
    else
    {
        /*
         * Profesor que no estaba originalmente
         * asignado a esta sesión.
         */

        sesion.Profesores.Add(
            new SesionClaseProfesor
            {
                SesionClaseId =
                    sesion.Id,

                ProfesorId =
                    dto.ProfesorQueDictoId,

                EsPrincipal =
                    false,

                EsReemplazo =
                    true,

                ReemplazaAProfesorId =
                    dto.ProfesorAusenteId,

                ControlConfirmado =
                    true,

                DictoClase =
                    true,

                ConfirmadoUtc =
                    ahora,

                ConfirmadoPorUsuarioId =
                    dto.UsuarioId
            });
    }


    /*
     * Esta clase ya tiene una particularidad respecto
     * de la grilla recurrente.
     */
    sesion.EsModificadaManualmente =
        true;


    await db.SaveChangesAsync(ct);

    await transaction.CommitAsync(ct);
}

// =========================================================
// VALIDAR SUPERPOSICIÓN DE PROFESORES EN UNA SESIÓN
// =========================================================

private static async Task
    ValidarSuperposicionProfesoresSesionAsync(
        AppDbContext db,
        Guid sesionIdExcluir,
        DateOnly fecha,
        TimeOnly horaInicio,
        int duracionMinutos,
        IReadOnlyCollection<Guid> profesorIds,
        CancellationToken ct)
{
    if (profesorIds.Count == 0)
    {
        return;
    }


    /*
     * Buscamos las demás clases de ese mismo día
     * donde alguno de los profesores seleccionados
     * ya se encuentre asignado.
     *
     * Excluimos la sesión que estamos modificando.
     */
    var sesiones =
        await db.SesionesClase
            .AsNoTracking()

            .Include(x =>
                x.Profesores)

            .Where(x =>
                x.Id != sesionIdExcluir
                &&
                x.Fecha == fecha
                &&
                x.Estado !=
                    EstadoSesionClase.Cancelada
                &&
                x.Profesores.Any(p =>
                    profesorIds.Contains(
                        p.ProfesorId)))

            .ToListAsync(ct);


    var horaFin =
        horaInicio.AddMinutes(
            duracionMinutos);


    foreach (var otraSesion in sesiones)
    {
        if (!HorariosSeSuperponen(
            horaInicio,
            horaFin,
            otraSesion.HoraInicio,
            otraSesion.HoraFin))
        {
            continue;
        }


        /*
         * Averiguamos qué profesor genera el conflicto.
         */
        var profesorConflictoId =
            otraSesion.Profesores

                .Where(x =>
                    profesorIds.Contains(
                        x.ProfesorId))

                .Select(x =>
                    x.ProfesorId)

                .FirstOrDefault();


        string mensaje;


        if (profesorConflictoId != Guid.Empty)
        {
            var nombreProfesor =
                await db.Profesores
                    .AsNoTracking()

                    .Where(x =>
                        x.Id ==
                            profesorConflictoId)

                    .Select(x =>
                        (x.Nombre + " " + x.Apellido)
                            .Trim())

                    .FirstOrDefaultAsync(ct);


            mensaje =
                string.IsNullOrWhiteSpace(
                    nombreProfesor)

                    ? "El profesor seleccionado ya está asignado a otra clase en ese horario."

                    : $"{nombreProfesor} ya está asignado a otra clase en ese horario.";
        }
        else
        {
            mensaje =
                "El profesor seleccionado ya está asignado a otra clase en ese horario.";
        }


        throw new InvalidOperationException(
            mensaje);
    }
}


// =========================================================
// SUPERPOSICIÓN HORARIA
// =========================================================

private static bool HorariosSeSuperponen(
    TimeOnly inicio1,
    TimeOnly fin1,
    TimeOnly inicio2,
    TimeOnly fin2)
{
    return
        inicio1 < fin2
        &&
        inicio2 < fin1;
}

public async Task CorregirProfesorComoDictoAsync(
    Guid sesionClaseId,
    Guid profesorId,
    Guid? usuarioId,
    CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    var sesion =
        await db.SesionesClase

            .Include(x =>
                x.Profesores)

            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                    sesionClaseId,
                ct)

        ?? throw new InvalidOperationException(
            "La clase indicada no existe.");


    if (sesion.Estado ==
        EstadoSesionClase.Cancelada)
    {
        throw new InvalidOperationException(
            "No se puede modificar el control de una clase cancelada.");
    }


    ValidarFecha(
        sesion.Fecha);


    var profesor =
        sesion.Profesores
            .FirstOrDefault(x =>
                x.ProfesorId ==
                    profesorId
                &&
                !x.EsReemplazo)

        ?? throw new InvalidOperationException(
            "El profesor indicado no pertenece a esta clase.");


    /*
     * Si antes habíamos cargado un reemplazo externo
     * para este profesor, lo eliminamos.
     */
    var reemplazos =
        sesion.Profesores

            .Where(x =>
                x.EsReemplazo
                &&
                x.ReemplazaAProfesorId ==
                    profesorId)

            .ToList();


    if (reemplazos.Count > 0)
    {
        db.SesionesClaseProfesores
            .RemoveRange(
                reemplazos);
    }


    profesor.ControlConfirmado =
        true;

    profesor.DictoClase =
        true;

    profesor.ConfirmadoUtc =
        DateTimeOffset.UtcNow;

    profesor.ConfirmadoPorUsuarioId =
        usuarioId;


    await db.SaveChangesAsync(ct);
}
}