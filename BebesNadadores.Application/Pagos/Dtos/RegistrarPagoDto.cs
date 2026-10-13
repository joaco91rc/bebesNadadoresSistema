using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Pagos.Dtos;

public sealed class RegistrarPagoDto
{
    public Guid BebeId { get; set; }

    public Guid? ResponsableId { get; set; }

    public decimal Importe { get; set; }

    public MedioPago MedioPago { get; set; }
        = MedioPago.Transferencia;

    public Guid? CuentaBancariaId { get; set; }

    public string? ReferenciaExterna { get; set; }

    public string? Observaciones { get; set; }
}