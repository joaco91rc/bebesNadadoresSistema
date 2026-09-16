using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class MovimientoCaja : EntidadAuditable
{
    public Guid CajaId { get; set; }
    public Caja Caja { get; set; } = null!;

    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;

    public TipoMovimientoCaja Tipo { get; set; }
    public CategoriaMovimientoCaja Categoria { get; set; }
    public MedioPago MedioPago { get; set; }

    public decimal Importe { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string? Referencia { get; set; }

    // Ej.: transferencia bancaria queda registrada, pero no modifica efectivo físico.
    public bool AfectaSaldoEfectivo { get; set; }

    public Guid? PagoId { get; set; }
    public Pago? Pago { get; set; }
}
