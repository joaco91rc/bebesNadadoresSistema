using BebesNadadores.Application.Caja;
using BebesNadadores.Domain.Common;
using BebesNadadores.Application.Caja.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Caja;

public sealed class ConceptoCajaService(
    IDbContextFactory<AppDbContext> factory)
    : IConceptoCajaService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<ConceptoCajaDto>>
        ListarAsync(
            TipoMovimientoCaja? tipo = null,
            bool incluirInactivos = false,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var query =
            db.ConceptosCaja
                .AsNoTracking()
                .AsQueryable();


        if (tipo.HasValue)
        {
            query =
                query.Where(x =>
                    x.Tipo ==
                        tipo.Value);
        }


        if (!incluirInactivos)
        {
            query =
                query.Where(x =>
                    x.Activo);
        }


        return await query
            .OrderBy(x =>
                x.Tipo)

            .ThenBy(x =>
                x.Orden)

            .ThenBy(x =>
                x.Nombre)

            .Select(x =>
                new ConceptoCajaDto(
                    x.Id,
                    x.Nombre,
                    x.Tipo,
                    x.Activo,
                    x.Orden))

            .ToListAsync(ct);
    }


    // =========================================================
    // CREAR
    // =========================================================

    public async Task<Guid>
        CrearAsync(
            GuardarConceptoCajaDto dto,
            CancellationToken ct = default)
    {
        Validar(
            dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var nombre =
            dto.Nombre.Trim();


        var existe =
            await db.ConceptosCaja
                .AnyAsync(
                    x =>
                        x.Tipo ==
                            dto.Tipo
                        &&
                        x.Nombre.ToLower() ==
                            nombre.ToLower(),
                    ct);


        if (existe)
        {
            throw new InvalidOperationException(
                "Ya existe un concepto con ese nombre para el mismo tipo de movimiento.");
        }


        var concepto =
            new ConceptoCaja
            {
                Id =
                    Guid.NewGuid(),

                Nombre =
                    nombre,

                Tipo =
                    dto.Tipo,

                Activo =
                    dto.Activo,

                Orden =
                    dto.Orden
            };


        db.ConceptosCaja.Add(
            concepto);


        await db.SaveChangesAsync(
            ct);


        return concepto.Id;
    }


    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public async Task ActualizarAsync(
        Guid id,
        GuardarConceptoCajaDto dto,
        CancellationToken ct = default)
    {
        Validar(
            dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var concepto =
            await db.ConceptosCaja
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            id,
                    ct)

            ??
            throw new InvalidOperationException(
                "El concepto indicado no existe.");

                if (
    concepto.Id ==
        ConceptosCajaSistema
            .CuotaNatatorioMensualId)
{
    throw new InvalidOperationException(
        "El concepto 'Cuota natatorio mensual' es un concepto del sistema y no puede modificarse.");
}


        var nombre =
            dto.Nombre.Trim();


        var duplicado =
            await db.ConceptosCaja
                .AnyAsync(
                    x =>
                        x.Id !=
                            id
                        &&
                        x.Tipo ==
                            dto.Tipo
                        &&
                        x.Nombre.ToLower() ==
                            nombre.ToLower(),
                    ct);


        if (duplicado)
        {
            throw new InvalidOperationException(
                "Ya existe otro concepto con ese nombre para el mismo tipo.");
        }


        concepto.Nombre =
            nombre;

        concepto.Tipo =
            dto.Tipo;

        concepto.Activo =
            dto.Activo;

        concepto.Orden =
            dto.Orden;


        await db.SaveChangesAsync(
            ct);
    }


    // =========================================================
    // ACTIVAR / DESACTIVAR
    // =========================================================

    public async Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default)
    {

        if (
    id ==
        ConceptosCajaSistema
            .CuotaNatatorioMensualId
    &&
    !activo)
{
    throw new InvalidOperationException(
        "El concepto 'Cuota natatorio mensual' es utilizado automáticamente por el sistema y no puede desactivarse.");
}
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var concepto =
            await db.ConceptosCaja
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            id,
                    ct)

            ??
            throw new InvalidOperationException(
                "El concepto indicado no existe.");


        concepto.Activo =
            activo;


        await db.SaveChangesAsync(
            ct);
    }


    // =========================================================
    // VALIDACIÓN
    // =========================================================

    private static void Validar(
        GuardarConceptoCajaDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre del concepto.");
        }


        if (dto.Nombre.Trim().Length > 150)
        {
            throw new InvalidOperationException(
                "El nombre no puede superar los 150 caracteres.");
        }


        if (dto.Orden < 0)
        {
            throw new InvalidOperationException(
                "El orden no puede ser negativo.");
        }
    }

    public async Task AsegurarConceptosSistemaAsync(
    CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    var existe =
        await db.ConceptosCaja
            .AnyAsync(
                x =>
                    x.Id ==
                    ConceptosCajaSistema
                        .CuotaNatatorioMensualId,
                ct);


    if (existe)
        return;


    db.ConceptosCaja.Add(
        new ConceptoCaja
        {
            Id =
                ConceptosCajaSistema
                    .CuotaNatatorioMensualId,

            Nombre =
                ConceptosCajaSistema
                    .CuotaNatatorioMensual,

            Tipo =
                TipoMovimientoCaja.Ingreso,

            Activo =
                true,

            Orden =
                0
        });


    await db.SaveChangesAsync(
        ct);
}
}