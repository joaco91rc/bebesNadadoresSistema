using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Proveedores.Dtos;

public sealed record PagoProveedorDto(
    Guid Id,
    DateTimeOffset FechaUtc,
    decimal Importe,
    MedioPago MedioPago,
    string Concepto,
    string? NumeroComprobante,
    string? ReferenciaExterna,
    string? Observaciones,
    Guid? CajaId,
    Guid? CuentaBancariaId,
    string? CuentaBancaria,
    string? Banco,
    bool Anulado = false,
    string? MotivoAnulacion = null);