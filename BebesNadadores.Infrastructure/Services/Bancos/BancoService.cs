using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bancos;

public sealed class BancoService(
    IDbContextFactory<AppDbContext> factory)
    : IBancoService
{
    public async Task<IReadOnlyList<BancoDto>>
        ListarAsync(
            bool incluirInactivos = false,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var query =
            db.Bancos
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
                x.Nombre)
            .Select(x =>
                new BancoDto(
                    x.Id,
                    x.Nombre,
                    x.Activo,
                    x.Cuentas.Count))
            .ToListAsync(ct);
    }


    public async Task<Guid> CrearAsync(
        GuardarBancoDto dto,
        CancellationToken ct = default)
    {
        var nombre =
            NormalizarRequerido(
                dto.Nombre);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var existe =
            await db.Bancos
                .AnyAsync(
                    x =>
                        x.Nombre.ToLower() ==
                            nombre.ToLower(),
                    ct);


        if (existe)
        {
            throw new InvalidOperationException(
                "Ya existe un banco con ese nombre.");
        }


        var banco =
            new Banco
            {
                Id =
                    Guid.NewGuid(),

                Nombre =
                    nombre,

                Activo =
                    dto.Activo
            };


        db.Bancos.Add(
            banco);


        await db.SaveChangesAsync(
            ct);


        return banco.Id;
    }


    public async Task ActualizarAsync(
        Guid id,
        GuardarBancoDto dto,
        CancellationToken ct = default)
    {
        var nombre =
            NormalizarRequerido(
                dto.Nombre);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var banco =
            await db.Bancos
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El banco indicado no existe.");


        var duplicado =
            await db.Bancos
                .AnyAsync(
                    x =>
                        x.Id != id
                        &&
                        x.Nombre.ToLower() ==
                            nombre.ToLower(),
                    ct);


        if (duplicado)
        {
            throw new InvalidOperationException(
                "Ya existe otro banco con ese nombre.");
        }


        banco.Nombre =
            nombre;

        banco.Activo =
            dto.Activo;


        await db.SaveChangesAsync(
            ct);
    }


    public async Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var banco =
            await db.Bancos
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El banco indicado no existe.");


        banco.Activo =
            activo;


        await db.SaveChangesAsync(
            ct);
    }


    private static string NormalizarRequerido(
        string? valor)
    {
        if (string.IsNullOrWhiteSpace(
            valor))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre.");
        }


        return valor.Trim();
    }
}