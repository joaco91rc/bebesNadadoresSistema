using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class MovimientoCaja : EntidadAuditable
{
    public Guid CajaId { get; set; }

    public Caja Caja { get; set; } =
        null!;


    public DateTimeOffset FechaUtc { get; set; }
        = DateTimeOffset.UtcNow;


    public TipoMovimientoCaja Tipo { get; set; }


    public Guid? ConceptoCajaId { get; set; }

    public ConceptoCaja? ConceptoCaja { get; set; }


    /*
     * Snapshot histórico.
     * Si mañana renombramos el ConceptoCaja,
     * el movimiento conserva cómo se llamaba.
     */
    public string Concepto { get; set; } =
        string.Empty;


    public string? Detalle { get; set; }


    public decimal Importe { get; set; }


    public string? Referencia { get; set; }


    /*
     * Si proviene de un cobro de cuota.
     */
    public Guid? PagoId { get; set; }

    public Pago? Pago { get; set; }

    public Guid? PagoProveedorId { get; set; }

public PagoProveedor? PagoProveedor { get; set; }
}