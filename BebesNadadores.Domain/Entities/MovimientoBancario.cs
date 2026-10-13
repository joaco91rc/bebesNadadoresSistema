using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class MovimientoBancario : EntidadAuditable
{
    public Guid CuentaBancariaId { get; set; }

    public CuentaBancaria CuentaBancaria { get; set; }
        = null!;


    public DateTimeOffset FechaUtc { get; set; }
        = DateTimeOffset.UtcNow;


    public TipoMovimientoBancario Tipo { get; set; }

    public OrigenMovimientoBancario Origen { get; set; }




    /*
     * Siempre positivo.
     *
     * Tipo define si suma o resta.
     */
    public decimal Importe { get; set; }


    public string Concepto { get; set; }
        = string.Empty;


    /*
     * Cuando proviene de un pago de una familia.
     */
    public Guid? PagoId { get; set; }

    public Pago? Pago { get; set; }

public Guid? PagoProveedorId { get; set; }

public PagoProveedor? PagoProveedor { get; set; }

    public string? ReferenciaExterna { get; set; }

    public string? Observaciones { get; set; }
}