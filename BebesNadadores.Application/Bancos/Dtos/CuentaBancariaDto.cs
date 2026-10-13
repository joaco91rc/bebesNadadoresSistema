using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record CuentaBancariaDto(
    Guid Id,
    Guid BancoId,
    string Banco,
    string Nombre,
    TipoCuentaBancaria Tipo,
    string? NumeroCuenta,
    string? CbuCvu,
    string? Alias,
    string Titular,
    string Moneda,
    decimal SaldoInicial,
    bool Activa);