using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bancos.Dtos;

public sealed record PagoProveedorBancarioDetalleDto(
    Guid PagoProveedorId,
    Guid ProveedorId,
    string Proveedor,
    DateTimeOffset FechaUtc,
    decimal Importe,
    MedioPago MedioPago,
    string Concepto,
    string? NumeroComprobante,
    string? ReferenciaExterna,
    string? Observaciones);