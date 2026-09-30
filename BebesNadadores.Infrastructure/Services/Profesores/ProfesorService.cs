using BebesNadadores.Application.Profesores;
using BebesNadadores.Application.Profesores.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Profesores;

public sealed class ProfesorService(
    IDbContextFactory<AppDbContext> factory)
    : IProfesorService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<ProfesorDto>> ListarAsync(
        bool incluirInactivos = true,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);

        var query =
            db.Profesores
                .AsNoTracking()
                .AsQueryable();


        if (!incluirInactivos)
        {
            query =
                query.Where(x => x.Activo);
        }


        return await query
            .OrderBy(x => x.Apellido)
            .ThenBy(x => x.Nombre)
            .Select(x =>
                new ProfesorDto(
                    x.Id,
                    x.Nombre,
                    x.Apellido,
                    (x.Nombre + " " + x.Apellido).Trim(),
                    x.Telefono,
                    x.Email,
                    x.Observaciones,
                    x.UsuarioId,
                    x.Activo,

                    x.Turnos.Count(tp =>
                        tp.Turno.Estado ==
                            EstadoTurno.Activo
                        &&
                        (
                            !tp.Turno.VigenteHasta.HasValue
                            ||
                            tp.Turno.VigenteHasta.Value >= hoy
                        ))))
            .ToListAsync(ct);
    }


    // =========================================================
    // OBTENER
    // =========================================================

    public async Task<ProfesorDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);

        return await db.Profesores
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x =>
                new ProfesorDto(
                    x.Id,
                    x.Nombre,
                    x.Apellido,
                    (x.Nombre + " " + x.Apellido).Trim(),
                    x.Telefono,
                    x.Email,
                    x.Observaciones,
                    x.UsuarioId,
                    x.Activo,

                    x.Turnos.Count(tp =>
                        tp.Turno.Estado ==
                            EstadoTurno.Activo
                        &&
                        (
                            !tp.Turno.VigenteHasta.HasValue
                            ||
                            tp.Turno.VigenteHasta.Value >= hoy
                        ))))
            .FirstOrDefaultAsync(ct);
    }


    // =========================================================
    // CREAR
    // =========================================================

    public async Task<Guid> CrearAsync(
        GuardarProfesorDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);


        await ValidarDuplicadoAsync(
            db,
            dto.Nombre,
            dto.Apellido,
            null,
            ct);


        var profesor =
            new Profesor
            {
                Id =
                    Guid.NewGuid(),

                Nombre =
                    dto.Nombre.Trim(),

                Apellido =
                    dto.Apellido.Trim(),

                Telefono =
                    Normalizar(dto.Telefono),

                Email =
                    NormalizarEmail(dto.Email),

                Observaciones =
                    Normalizar(dto.Observaciones),

                Activo =
                    dto.Activo
            };


        db.Profesores.Add(
            profesor);

        await db.SaveChangesAsync(ct);

        return profesor.Id;
    }


    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public async Task ActualizarAsync(
        Guid id,
        GuardarProfesorDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);


        var profesor =
            await db.Profesores
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El profesor indicado no existe.");


        await ValidarDuplicadoAsync(
            db,
            dto.Nombre,
            dto.Apellido,
            id,
            ct);


        if (profesor.Activo &&
            !dto.Activo)
        {
            await ValidarPuedeDesactivarseAsync(
                db,
                profesor.Id,
                ct);
        }


        profesor.Nombre =
            dto.Nombre.Trim();

        profesor.Apellido =
            dto.Apellido.Trim();

        profesor.Telefono =
            Normalizar(dto.Telefono);

        profesor.Email =
            NormalizarEmail(dto.Email);

        profesor.Observaciones =
            Normalizar(dto.Observaciones);

        profesor.Activo =
            dto.Activo;


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // ACTIVAR / DESACTIVAR
    // =========================================================

    public async Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var profesor =
            await db.Profesores
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El profesor indicado no existe.");


        if (!activo)
        {
            await ValidarPuedeDesactivarseAsync(
                db,
                profesor.Id,
                ct);
        }


        profesor.Activo =
            activo;

        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void Validar(
        GuardarProfesorDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe ingresar el nombre.");
        }


        if (string.IsNullOrWhiteSpace(
            dto.Apellido))
        {
            throw new InvalidOperationException(
                "Debe ingresar el apellido.");
        }


        if (dto.Nombre.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El nombre no puede superar los 100 caracteres.");
        }


        if (dto.Apellido.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El apellido no puede superar los 100 caracteres.");
        }


        if (dto.Telefono?.Trim().Length > 50)
        {
            throw new InvalidOperationException(
                "El teléfono no puede superar los 50 caracteres.");
        }


        if (dto.Email?.Trim().Length > 200)
        {
            throw new InvalidOperationException(
                "El email no puede superar los 200 caracteres.");
        }


        if (!string.IsNullOrWhiteSpace(
                dto.Email)
            &&
            !EsEmailValido(dto.Email))
        {
            throw new InvalidOperationException(
                "El email ingresado no es válido.");
        }


        if (dto.Observaciones?.Length > 1000)
        {
            throw new InvalidOperationException(
                "Las observaciones no pueden superar los 1000 caracteres.");
        }
    }


    private static async Task
        ValidarDuplicadoAsync(
            AppDbContext db,
            string nombre,
            string apellido,
            Guid? excluirId,
            CancellationToken ct)
    {
        var nombreNormalizado =
            nombre.Trim().ToLower();

        var apellidoNormalizado =
            apellido.Trim().ToLower();


        var existe =
            await db.Profesores
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        (
                            !excluirId.HasValue
                            ||
                            x.Id != excluirId.Value
                        )
                        &&
                        x.Nombre.ToLower() ==
                            nombreNormalizado
                        &&
                        x.Apellido.ToLower() ==
                            apellidoNormalizado,
                    ct);


        if (existe)
        {
            throw new InvalidOperationException(
                "Ya existe un profesor con el mismo nombre y apellido.");
        }
    }


    private static async Task
        ValidarPuedeDesactivarseAsync(
            AppDbContext db,
            Guid profesorId,
            CancellationToken ct)
    {
        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        var tieneTurnos =
            await db.TurnosProfesores
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.ProfesorId ==
                            profesorId
                        &&
                        x.Turno.Estado ==
                            EstadoTurno.Activo
                        &&
                        (
                            !x.Turno.VigenteHasta.HasValue
                            ||
                            x.Turno.VigenteHasta.Value >= hoy
                        ),
                    ct);


        if (tieneTurnos)
        {
            throw new InvalidOperationException(
                "No puede desactivar este profesor porque tiene turnos activos asignados. Primero reasigne o dé de baja esos turnos.");
        }
    }


    private static bool EsEmailValido(
        string email)
    {
        try
        {
            _ =
                new System.Net.Mail.MailAddress(
                    email.Trim());

            return true;
        }
        catch
        {
            return false;
        }
    }


    private static string? Normalizar(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }


    private static string? NormalizarEmail(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim().ToLowerInvariant();
    }

    public async Task<IReadOnlyList<HorarioProfesorDto>>
    ConsultarHorariosAsync(
        Guid profesorId,
        Guid? nivelId = null,
        CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);

    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    var profesorExiste =
        await db.Profesores
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.Id == profesorId
                    &&
                    x.Activo,
                ct);


    if (!profesorExiste)
    {
        throw new InvalidOperationException(
            "El profesor seleccionado no existe o está inactivo.");
    }


    var query =
        db.TurnosProfesores
            .AsNoTracking()
            .Where(x =>
                x.ProfesorId == profesorId
                &&
                x.Turno.Estado ==
                    EstadoTurno.Activo
                &&
                x.Turno.VigenteDesde <= hoy
                &&
                (
                    !x.Turno.VigenteHasta.HasValue
                    ||
                    x.Turno.VigenteHasta.Value >= hoy
                ));


    if (nivelId.HasValue)
    {
        query =
            query.Where(x =>
                x.Turno.NivelId ==
                    nivelId.Value);
    }


    var datos =
        await query
            .OrderBy(x =>
                x.Turno.DiaSemana)
            .ThenBy(x =>
                x.Turno.HoraInicio)
            .Select(x =>
                new
                {
                    x.TurnoId,

                    x.ProfesorId,

                    Profesor =
                        x.Profesor.Nombre
                        + " "
                        + x.Profesor.Apellido,

                    x.Turno.NivelId,

                    Nivel =
                        x.Turno.Nivel.Nombre,

                    x.Turno.DiaSemana,

                    x.Turno.HoraInicio,

                    x.Turno.DuracionMinutos,

                    x.Turno.Sector,

                    x.EsPrincipal,

                    x.Turno.VigenteDesde,

                    x.Turno.VigenteHasta
                })
            .ToListAsync(ct);


    return datos
        .Select(x =>
            new HorarioProfesorDto(
                x.TurnoId,
                x.ProfesorId,
                x.Profesor.Trim(),
                x.NivelId,
                x.Nivel,
                x.DiaSemana,
                x.HoraInicio,
                x.HoraInicio.AddMinutes(
                    x.DuracionMinutos),
                x.DuracionMinutos,
                x.Sector,
                x.EsPrincipal,
                x.VigenteDesde,
                x.VigenteHasta))
        .ToList();
}
}