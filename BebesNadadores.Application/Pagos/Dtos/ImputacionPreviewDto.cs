namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record ImputacionPreviewDto(
    Guid CuotaId,
    DateOnly Periodo,
    decimal SaldoActual,
    decimal ImporteAImputar,
    decimal SaldoPosterior);