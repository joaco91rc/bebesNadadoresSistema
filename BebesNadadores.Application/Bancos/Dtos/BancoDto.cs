namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record BancoDto(
    Guid Id,
    string Nombre,
    bool Activo,
    int CantidadCuentas);