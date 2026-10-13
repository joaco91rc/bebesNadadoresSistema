namespace BebesNadadores.Application.Caja.Dtos;

public sealed record CerrarCajaResultadoDto(
    Guid CajaId,
    decimal SaldoEsperado,
    decimal SaldoFinalDeclarado,
    decimal Diferencia);