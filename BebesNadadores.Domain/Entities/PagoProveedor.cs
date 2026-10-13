using BebesNadadores.Domain.Enums;
using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class PagoProveedor : EntidadAuditable
{
    public Guid ProveedorId { get; set; }

    public Proveedor Proveedor { get; set; } =
        null!;


    public DateTimeOffset FechaUtc { get; set; } =
        DateTimeOffset.UtcNow;


    public decimal Importe { get; set; }


    public MedioPago MedioPago { get; set; }


    /*
     * Ejemplos:
     *
     * Cloro para pileta
     * Reparación bomba
     * Honorarios contador
     * Insumos de limpieza
     */
    public string Concepto { get; set; } =
        string.Empty;


    public string? NumeroComprobante { get; set; }

    public string? ReferenciaExterna { get; set; }

    public string? Observaciones { get; set; }


    /*
     * Usuario que cargó el pago.
     */
    public Guid UsuarioRegistroId { get; set; }


    /*
     * Si fue en efectivo.
     */
    public Guid? CajaId { get; set; }

    public Caja? Caja { get; set; }


    /*
     * Si salió de banco / billetera.
     */
    public Guid? CuentaBancariaId { get; set; }

    public CuentaBancaria? CuentaBancaria { get; set; }
}