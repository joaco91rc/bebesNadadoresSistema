using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record MovimientoBancarioDetalleDto(
    Guid Id,
    DateTimeOffset FechaUtc,
    TipoMovimientoBancario Tipo,
    OrigenMovimientoBancario Origen,
    decimal Importe,
    string Concepto,
    string? ReferenciaExterna,
    string? Observaciones,
    PagoBancarioDetalleDto? Pago,
    PagoProveedorBancarioDetalleDto? PagoProveedor);


public sealed record PagoBancarioDetalleDto(
    Guid PagoId,
    Guid BebeId,
    string Bebe,
    string? Responsable,
    DateTimeOffset FechaUtc,
    decimal Importe,
    MedioPago MedioPago,
    string? ReferenciaExterna,
    string? Observaciones,
    IReadOnlyList<ImputacionPagoBancarioDto> Imputaciones);


