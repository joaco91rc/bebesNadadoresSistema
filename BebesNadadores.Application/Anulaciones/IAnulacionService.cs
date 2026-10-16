namespace BebesNadadores.Application.Anulaciones;

/*
 * Anulación de operaciones con dinero.
 *
 * Nada se borra: lo anulado queda marcado con usuario, fecha y
 * motivo, y el dinero se revierte con un contra-asiento (caja o
 * banco). Así una caja ya cerrada nunca cambia sus números.
 */
public interface IAnulacionService
{
    /*
     * Anula un pago de una familia:
     * - libera las cuotas que había cancelado (vuelven a deber),
     * - revierte el ingreso en la caja del día o en la cuenta bancaria,
     * - reaplica los saldos a favor que tenga el bebé.
     */
    Task AnularPagoAsync(
        Guid pagoId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default);


    /*
     * Anula una cuota (beca, error de generación, etc.).
     * Si tenía pagos imputados, ese dinero queda como saldo a favor
     * y se aplica a otras cuotas pendientes del bebé.
     */
    Task AnularCuotaAsync(
        Guid cuotaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default);


    /*
     * Reactiva una cuota anulada por error (la vuelve a cobrar).
     * - Queda Pendiente o Vencida según su vencimiento.
     * - No mueve dinero. Si el bebé tiene saldo a favor, se aplica.
     * - Guarda la anulación que se deshizo en CuotaReactivacion.
     * No se puede reactivar una cuota posterior a la baja / fin de
     * la inscripción, ni si el bebé ya tiene otra cuota vigente para
     * ese mes.
     */
    Task ReactivarCuotaAsync(
        Guid cuotaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default);


    /*
     * Anula un movimiento MANUAL de caja (ingreso / egreso cargado a
     * mano). Los cobros y los pagos a proveedores se anulan desde su
     * propia pantalla.
     */
    Task AnularMovimientoCajaAsync(
        Guid movimientoCajaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default);


    /*
     * Anula un pago a proveedor y revierte el egreso en la caja del
     * día o en la cuenta bancaria.
     */
    Task AnularPagoProveedorAsync(
        Guid pagoProveedorId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default);
}
