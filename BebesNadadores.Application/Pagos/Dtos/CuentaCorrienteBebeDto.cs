namespace BebesNadadores.Application.Pagos.Dtos;

public sealed record CuentaCorrienteBebeDto(
    Guid BebeId,
    string NombreCompleto,
    decimal TotalFacturado,
    decimal TotalPagado,
    decimal Deuda,
    decimal SaldoAFavor,
    Guid? ResponsablePrincipalId,
    IReadOnlyList<ResponsablePagoDto> Responsables,
    IReadOnlyList<CuotaCuentaCorrienteDto> Cuotas,
    IReadOnlyList<PagoCuentaCorrienteDto> Pagos);