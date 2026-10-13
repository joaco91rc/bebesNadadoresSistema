namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record ResumenMovimientosBancariosDto(
    decimal TotalIngresos,
    decimal TotalEgresos,
    decimal NetoPeriodo,
    int CantidadMovimientos,
    IReadOnlyList<MovimientoBancarioDto> Movimientos);  