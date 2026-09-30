using BebesNadadores.Application.Niveles;
using BebesNadadores.Application.Niveles.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Niveles;

public sealed class NivelService(
    IDbContextFactory<AppDbContext> factory)
    : INivelService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<NivelDto>> ListarAsync(
        bool incluirInactivos = true,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var query =
            db.Niveles
                .AsNoTracking()
                .AsQueryable();

        if (!incluirInactivos)
        {
            query =
                query.Where(x => x.Activo);
        }

        return await query
            .OrderBy(x => x.Orden)
            .ThenBy(x => x.Nombre)
            .Select(x =>
                new NivelDto(
                    x.Id,
                    x.Nombre,
                    x.Descripcion,
                    x.Orden,
                    x.EdadMinMeses,
                    x.EdadMaxMeses,
                    x.DuracionMinutos,
                    x.CupoPredeterminado,
                    x.ProfesoresMinimos,
                    x.ProfesoresMaximos,
                    x.Activo,

                    x.Turnos.Count(t =>
                        t.Estado ==
                            EstadoTurno.Activo)))
            .ToListAsync(ct);
    }


    // =========================================================
    // OBTENER
    // =========================================================

    public async Task<NivelDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        return await db.Niveles
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x =>
                new NivelDto(
                    x.Id,
                    x.Nombre,
                    x.Descripcion,
                    x.Orden,
                    x.EdadMinMeses,
                    x.EdadMaxMeses,
                    x.DuracionMinutos,
                    x.CupoPredeterminado,
                    x.ProfesoresMinimos,
                    x.ProfesoresMaximos,
                    x.Activo,

                    x.Turnos.Count(t =>
                        t.Estado ==
                            EstadoTurno.Activo)))
            .FirstOrDefaultAsync(ct);
    }


    // =========================================================
    // CREAR
    // =========================================================

    public async Task<Guid> CrearAsync(
        GuardarNivelDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        await ValidarNombreDuplicadoAsync(
            db,
            dto.Nombre,
            null,
            ct);

        var nivel =
            new Nivel
            {
                Id =
                    Guid.NewGuid(),

                Nombre =
                    dto.Nombre.Trim(),

                Descripcion =
                    Normalizar(dto.Descripcion),

                Orden =
                    dto.Orden,

                EdadMinMeses =
                    dto.EdadMinMeses,

                EdadMaxMeses =
                    dto.EdadMaxMeses,

                DuracionMinutos =
                    dto.DuracionMinutos,

                CupoPredeterminado =
                    dto.CupoPredeterminado,

                ProfesoresMinimos =
                    dto.ProfesoresMinimos,

                ProfesoresMaximos =
                    dto.ProfesoresMaximos,

                Activo =
                    dto.Activo
            };

        db.Niveles.Add(nivel);

        await db.SaveChangesAsync(ct);

        return nivel.Id;
    }


    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public async Task ActualizarAsync(
        Guid id,
        GuardarNivelDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var nivel =
            await db.Niveles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El nivel indicado no existe.");

        await ValidarNombreDuplicadoAsync(
            db,
            dto.Nombre,
            id,
            ct);

        if (!dto.Activo &&
            nivel.Activo)
        {
            await ValidarPuedeDesactivarseAsync(
                db,
                nivel.Id,
                ct);
        }

        nivel.Nombre =
            dto.Nombre.Trim();

        nivel.Descripcion =
            Normalizar(dto.Descripcion);

        nivel.Orden =
            dto.Orden;

        nivel.EdadMinMeses =
            dto.EdadMinMeses;

        nivel.EdadMaxMeses =
            dto.EdadMaxMeses;

        nivel.DuracionMinutos =
            dto.DuracionMinutos;

        nivel.CupoPredeterminado =
            dto.CupoPredeterminado;

        nivel.ProfesoresMinimos =
            dto.ProfesoresMinimos;

        nivel.ProfesoresMaximos =
            dto.ProfesoresMaximos;

        nivel.Activo =
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

        var nivel =
            await db.Niveles
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El nivel indicado no existe.");

        if (!activo)
        {
            await ValidarPuedeDesactivarseAsync(
                db,
                id,
                ct);
        }

        nivel.Activo =
            activo;

        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void Validar(
        GuardarNivelDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe ingresar el nombre del nivel.");
        }

        if (dto.Nombre.Trim().Length > 150)
        {
            throw new InvalidOperationException(
                "El nombre no puede superar los 150 caracteres.");
        }

        if (dto.Descripcion?.Length > 500)
        {
            throw new InvalidOperationException(
                "La descripción no puede superar los 500 caracteres.");
        }

        if (dto.Orden < 0)
        {
            throw new InvalidOperationException(
                "El orden no puede ser negativo.");
        }

        if (dto.EdadMinMeses < 0)
        {
            throw new InvalidOperationException(
                "La edad mínima no puede ser negativa.");
        }

        if (dto.EdadMaxMeses <
            dto.EdadMinMeses)
        {
            throw new InvalidOperationException(
                "La edad máxima debe ser mayor o igual a la edad mínima.");
        }

        /*
         * Bebés Nadadores admite hasta 7 años.
         */
        if (dto.EdadMaxMeses > 84)
        {
            throw new InvalidOperationException(
                "La edad máxima no puede superar los 84 meses (7 años).");
        }

        if (dto.DuracionMinutos <= 0 ||
            dto.DuracionMinutos > 120)
        {
            throw new InvalidOperationException(
                "La duración debe estar entre 1 y 120 minutos.");
        }

        if (dto.CupoPredeterminado <= 0)
        {
            throw new InvalidOperationException(
                "El cupo debe ser mayor a cero.");
        }

        if (dto.ProfesoresMinimos <= 0)
        {
            throw new InvalidOperationException(
                "Debe haber al menos un profesor.");
        }

        if (dto.ProfesoresMaximos <
            dto.ProfesoresMinimos)
        {
            throw new InvalidOperationException(
                "La cantidad máxima de profesores no puede ser menor que la mínima.");
        }
    }


    private static async Task
        ValidarNombreDuplicadoAsync(
            AppDbContext db,
            string nombre,
            Guid? excluirId,
            CancellationToken ct)
    {
        var normalizado =
            nombre.Trim().ToLower();

        var existe =
            await db.Niveles
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        (!excluirId.HasValue ||
                         x.Id != excluirId.Value)
                        &&
                        x.Nombre.ToLower() ==
                            normalizado,
                    ct);

        if (existe)
        {
            throw new InvalidOperationException(
                "Ya existe un nivel con ese nombre.");
        }
    }


    private static async Task
        ValidarPuedeDesactivarseAsync(
            AppDbContext db,
            Guid nivelId,
            CancellationToken ct)
    {
        var tieneTurnosActivos =
            await db.Turnos
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.NivelId == nivelId
                        &&
                        x.Estado ==
                            EstadoTurno.Activo,
                    ct);

        if (tieneTurnosActivos)
        {
            throw new InvalidOperationException(
                "No puede desactivar este nivel porque tiene turnos activos asociados. Primero debe dar de baja esos turnos.");
        }
    }


    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(texto)
            ? null
            : texto.Trim();
    }
}