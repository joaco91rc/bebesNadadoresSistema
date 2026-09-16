namespace BebesNadadores.Domain.Entities;

public class ImputacionPago
{
    public Guid PagoId { get; set; }
    public Pago Pago { get; set; } = null!;

    public Guid CuotaId { get; set; }
    public Cuota Cuota { get; set; } = null!;

    public decimal Importe { get; set; }
}
