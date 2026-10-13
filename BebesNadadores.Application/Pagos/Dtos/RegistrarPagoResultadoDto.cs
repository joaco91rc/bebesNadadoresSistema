namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record RegistrarPagoResultadoDto(
    Guid PagoId,
    decimal Importe,
    decimal ImporteImputado,
    decimal SaldoAFavor);