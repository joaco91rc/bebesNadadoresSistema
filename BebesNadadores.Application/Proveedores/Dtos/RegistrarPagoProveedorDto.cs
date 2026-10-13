using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Proveedores.Dtos;

public sealed class RegistrarPagoProveedorDto
{
    public Guid ProveedorId { get; set; }

    public decimal Importe { get; set; }

    public MedioPago MedioPago { get; set; } =
        MedioPago.Transferencia;

    public string Concepto { get; set; } =
        string.Empty;

    public string? NumeroComprobante { get; set; }

    public string? ReferenciaExterna { get; set; }

    public string? Observaciones { get; set; }

    public Guid? CuentaBancariaId { get; set; }
}