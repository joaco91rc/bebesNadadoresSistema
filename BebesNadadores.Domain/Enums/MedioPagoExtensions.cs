namespace BebesNadadores.Domain.Enums;

/*
 * Regla única sobre los medios de pago.
 *
 * La usan los servicios (PagoService, BebeService a través de
 * RegistradorCobros) y la UI, para que no vuelvan a desincronizarse.
 */
public static class MedioPagoExtensions
{
    /*
     * Medios que ingresan dinero a una cuenta bancaria o billetera
     * y por lo tanto generan un MovimientoBancario.
     */
    public static bool RequiereCuentaBancaria(
        this MedioPago medio)
    {
        return medio is
            MedioPago.Transferencia
            or MedioPago.Tarjeta
            or MedioPago.MercadoPago;
    }


    /*
     * Medios que ingresan a la caja física del día
     * y por lo tanto generan un MovimientoCaja.
     */
    public static bool IngresaACaja(
        this MedioPago medio)
    {
        return medio ==
            MedioPago.Efectivo;
    }
}
