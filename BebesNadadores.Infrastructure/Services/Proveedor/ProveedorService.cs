using BebesNadadores.Application.Proveedores;
using BebesNadadores.Application.Proveedores.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ProveedorEntidad =   BebesNadadores.Domain.Entities.Proveedor;
namespace BebesNadadores.Infrastructure.Services.Proveedores;

public sealed class ProveedorService(
    IDbContextFactory<AppDbContext> factory)
    : IProveedorService
{
   public async Task<IReadOnlyList<ProveedorDto>>
    ListarAsync(
        bool incluirInactivos = false,
        CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    var query =
        db.Proveedores
            .AsNoTracking()
            .AsQueryable();


    if (!incluirInactivos)
    {
        query =
            query.Where(x =>
                x.Activo);
    }


    return await query
        .OrderBy(x =>
            x.RazonSocial)

        .Select(x =>
            new ProveedorDto(
                x.Id,
                x.RazonSocial,
                x.NombreFantasia,
                x.Cuit,
                x.Telefono,
                x.Email,
                x.Direccion,
                x.Observaciones,
                x.Activo,

                // TOTAL PAGADO
                x.Pagos
                    .Sum(p =>
                        (decimal?)p.Importe)
                ?? 0m,

                // ÚLTIMO PAGO
                x.Pagos
                    .Max(p =>
                        (DateTimeOffset?)p.FechaUtc)
            ))

        .ToListAsync(ct);
}


   public async Task<ProveedorDto?>
    ObtenerAsync(
        Guid id,
        CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    return await db.Proveedores
        .AsNoTracking()

        .Where(x =>
            x.Id == id)

        .Select(x =>
            new ProveedorDto(
                x.Id,
                x.RazonSocial,
                x.NombreFantasia,
                x.Cuit,
                x.Telefono,
                x.Email,
                x.Direccion,
                x.Observaciones,
                x.Activo,

                // TOTAL PAGADO
                x.Pagos
                    .Sum(p =>
                        (decimal?)p.Importe)
                ?? 0m,

                // ÚLTIMO PAGO
                x.Pagos
                    .Max(p =>
                        (DateTimeOffset?)p.FechaUtc)
            ))

        .FirstOrDefaultAsync(ct);
}


    public async Task<Guid>
        CrearAsync(
            GuardarProveedorDto dto,
            CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var cuit =
            Normalizar(dto.Cuit);


        if (cuit is not null)
        {
            var existe =
                await db.Proveedores
                    .AnyAsync(
                        x =>
                            x.Cuit == cuit,
                        ct);


            if (existe)
            {
                throw new InvalidOperationException(
                    "Ya existe un proveedor con ese CUIT.");
            }
        }


        var proveedor =
            new ProveedorEntidad
            {
                Id =
                    Guid.NewGuid(),

                RazonSocial =
                    dto.RazonSocial.Trim(),

                NombreFantasia =
                    Normalizar(dto.NombreFantasia),

                Cuit =
                    cuit,

                Telefono =
                    Normalizar(dto.Telefono),

                Email =
                    Normalizar(dto.Email),

                Direccion =
                    Normalizar(dto.Direccion),

                Observaciones =
                    Normalizar(dto.Observaciones),

                Activo =
                    dto.Activo
            };


        db.Proveedores.Add(
            proveedor);


        await db.SaveChangesAsync(ct);


        return proveedor.Id;
    }


    public async Task ActualizarAsync(
        Guid id,
        GuardarProveedorDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var proveedor =
            await db.Proveedores
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id,
                    ct)

            ?? throw new InvalidOperationException(
                "El proveedor indicado no existe.");


        var cuit =
            Normalizar(dto.Cuit);


        if (cuit is not null)
        {
            var existe =
                await db.Proveedores
                    .AnyAsync(
                        x =>
                            x.Id != id
                            &&
                            x.Cuit == cuit,
                        ct);


            if (existe)
            {
                throw new InvalidOperationException(
                    "Ya existe otro proveedor con ese CUIT.");
            }
        }


        proveedor.RazonSocial =
            dto.RazonSocial.Trim();

        proveedor.NombreFantasia =
            Normalizar(dto.NombreFantasia);

        proveedor.Cuit =
            cuit;

        proveedor.Telefono =
            Normalizar(dto.Telefono);

        proveedor.Email =
            Normalizar(dto.Email);

        proveedor.Direccion =
            Normalizar(dto.Direccion);

        proveedor.Observaciones =
            Normalizar(dto.Observaciones);

        proveedor.Activo =
            dto.Activo;


        await db.SaveChangesAsync(ct);
    }


    public async Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var proveedor =
            await db.Proveedores
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == id,
                    ct)

            ?? throw new InvalidOperationException(
                "El proveedor indicado no existe.");


        proveedor.Activo =
            activo;


        await db.SaveChangesAsync(ct);
    }


    private static void Validar(
        GuardarProveedorDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.RazonSocial))
        {
            throw new InvalidOperationException(
                "Debe indicar la razón social.");
        }


        if (
            !string.IsNullOrWhiteSpace(dto.Email)
            &&
            !dto.Email.Contains('@'))
        {
            throw new InvalidOperationException(
                "El email ingresado no es válido.");
        }
    }


    private static string? Normalizar(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }
}