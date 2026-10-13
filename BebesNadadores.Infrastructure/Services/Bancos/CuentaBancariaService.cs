using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bancos;

public sealed class CuentaBancariaService(
    IDbContextFactory<AppDbContext> factory)
    : ICuentaBancariaService
{
    public async Task<IReadOnlyList<CuentaBancariaDto>>
        ListarAsync(
            bool incluirInactivas = false,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var query =
            db.CuentasBancarias
                .AsNoTracking()
                .Include(x =>
                    x.Banco)
                .AsQueryable();


        if (!incluirInactivas)
        {
            query =
                query.Where(x =>
                    x.Activa);
        }


        return await query
            .OrderBy(x =>
                x.Banco.Nombre)
            .ThenBy(x =>
                x.Nombre)
            .Select(x =>
                new CuentaBancariaDto(
                    x.Id,
                    x.BancoId,
                    x.Banco.Nombre,
                    x.Nombre,
                    x.Tipo,
                    x.NumeroCuenta,
                    x.CbuCvu,
                    x.Alias,
                    x.Titular,
                    x.Moneda,
                    x.SaldoInicial,
                    x.Activa))
            .ToListAsync(ct);
    }


    public async Task<CuentaBancariaDto?>
        ObtenerAsync(
            Guid id,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        return await db.CuentasBancarias
            .AsNoTracking()
            .Where(x =>
                x.Id == id)
            .Select(x =>
                new CuentaBancariaDto(
                    x.Id,
                    x.BancoId,
                    x.Banco.Nombre,
                    x.Nombre,
                    x.Tipo,
                    x.NumeroCuenta,
                    x.CbuCvu,
                    x.Alias,
                    x.Titular,
                    x.Moneda,
                    x.SaldoInicial,
                    x.Activa))
            .FirstOrDefaultAsync(ct);
    }


    public async Task<Guid> CrearAsync(
        GuardarCuentaBancariaDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        await ValidarBancoAsync(
            db,
            dto.BancoId,
            ct);


        await ValidarDuplicadosAsync(
            db,
            dto,
            null,
            ct);


        var cuenta =
            new CuentaBancaria
            {
                Id =
                    Guid.NewGuid(),

                BancoId =
                    dto.BancoId,

                Nombre =
                    dto.Nombre.Trim(),

                Tipo =
                    dto.Tipo,

                NumeroCuenta =
                    Normalizar(dto.NumeroCuenta),

                CbuCvu =
                    Normalizar(dto.CbuCvu),

                Alias =
                    Normalizar(dto.Alias),

                Titular =
                    dto.Titular.Trim(),

                Moneda =
                    string.IsNullOrWhiteSpace(
                        dto.Moneda)
                        ? "ARS"
                        : dto.Moneda
                            .Trim()
                            .ToUpperInvariant(),

                SaldoInicial =
                    dto.SaldoInicial,

                Activa =
                    dto.Activa
            };


        db.CuentasBancarias.Add(
            cuenta);


        await db.SaveChangesAsync(
            ct);


        return cuenta.Id;
    }


    public async Task ActualizarAsync(
        Guid id,
        GuardarCuentaBancariaDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var cuenta =
            await db.CuentasBancarias
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "La cuenta bancaria indicada no existe.");


        await ValidarBancoAsync(
            db,
            dto.BancoId,
            ct);


        await ValidarDuplicadosAsync(
            db,
            dto,
            id,
            ct);


        cuenta.BancoId =
            dto.BancoId;

        cuenta.Nombre =
            dto.Nombre.Trim();

        cuenta.Tipo =
            dto.Tipo;

        cuenta.NumeroCuenta =
            Normalizar(
                dto.NumeroCuenta);

        cuenta.CbuCvu =
            Normalizar(
                dto.CbuCvu);

        cuenta.Alias =
            Normalizar(
                dto.Alias);

        cuenta.Titular =
            dto.Titular.Trim();

        cuenta.Moneda =
            string.IsNullOrWhiteSpace(
                dto.Moneda)
                ? "ARS"
                : dto.Moneda
                    .Trim()
                    .ToUpperInvariant();

        cuenta.SaldoInicial =
            dto.SaldoInicial;

        cuenta.Activa =
            dto.Activa;


        await db.SaveChangesAsync(
            ct);
    }


    public async Task CambiarEstadoAsync(
        Guid id,
        bool activa,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var cuenta =
            await db.CuentasBancarias
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "La cuenta bancaria indicada no existe.");


        cuenta.Activa =
            activa;


        await db.SaveChangesAsync(
            ct);
    }


    private static void Validar(
        GuardarCuentaBancariaDto dto)
    {
        if (dto.BancoId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "Debe seleccionar un banco.");
        }


        if (string.IsNullOrWhiteSpace(
            dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe indicar un nombre para la cuenta.");
        }


        if (string.IsNullOrWhiteSpace(
            dto.Titular))
        {
            throw new InvalidOperationException(
                "Debe indicar el titular de la cuenta.");
        }


        if (dto.SaldoInicial < 0)
        {
            throw new InvalidOperationException(
                "El saldo inicial no puede ser negativo.");
        }
    }


    private static async Task ValidarBancoAsync(
        AppDbContext db,
        Guid bancoId,
        CancellationToken ct)
    {
        var existe =
            await db.Bancos
                .AnyAsync(
                    x =>
                        x.Id == bancoId
                        &&
                        x.Activo,
                    ct);


        if (!existe)
        {
            throw new InvalidOperationException(
                "El banco seleccionado no existe o está inactivo.");
        }
    }


    private static async Task ValidarDuplicadosAsync(
        AppDbContext db,
        GuardarCuentaBancariaDto dto,
        Guid? excluirId,
        CancellationToken ct)
    {
        var cbu =
            Normalizar(
                dto.CbuCvu);

        var alias =
            Normalizar(
                dto.Alias);


        if (cbu is not null)
        {
            var existe =
                await db.CuentasBancarias
                    .AnyAsync(
                        x =>
                            x.Id != excluirId
                            &&
                            x.CbuCvu == cbu,
                        ct);


            if (existe)
            {
                throw new InvalidOperationException(
                    "Ya existe una cuenta con ese CBU/CVU.");
            }
        }


        if (alias is not null)
        {
            var existe =
                await db.CuentasBancarias
                    .AnyAsync(
                        x =>
                            x.Id != excluirId
                            &&
                            x.Alias != null
                            &&
                            x.Alias.ToLower() ==
                                alias.ToLower(),
                        ct);


            if (existe)
            {
                throw new InvalidOperationException(
                    "Ya existe una cuenta con ese alias.");
            }
        }
    }


    private static string? Normalizar(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(
            valor)
            ? null
            : valor.Trim();
    }
}