namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record CuentaBancariaMovimientosDto(
    Guid CuentaBancariaId,
    string Banco,
    string Cuenta,
    string Moneda,
    decimal TotalIngresos,
    decimal TotalEgresos,
    decimal NetoPeriodo,
    int CantidadMovimientos,
    IReadOnlyList<MovimientoBancarioDto> Movimientos);