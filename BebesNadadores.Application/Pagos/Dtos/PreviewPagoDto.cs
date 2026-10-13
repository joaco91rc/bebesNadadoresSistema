namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record PreviewPagoDto(
    decimal ImportePago,
    decimal TotalImputado,
    decimal SaldoAFavor,
    IReadOnlyList<ImputacionPreviewDto> Imputaciones);