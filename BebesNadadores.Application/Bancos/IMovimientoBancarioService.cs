using BebesNadadores.Application.Bancos.Dtos;

namespace BebesNadadores.Application.Bancos;

public interface IMovimientoBancarioService
{
    // Lo dejamos por compatibilidad.
    Task<IReadOnlyList<MovimientoBancarioDto>>
        ListarAsync(
            Guid? cuentaBancariaId,
            DateOnly desde,
            DateOnly hasta,
            CancellationToken ct = default);


    // Nuevo listado avanzado.
    Task<ResumenMovimientosBancariosDto>
        ListarFiltradoAsync(
            MovimientosBancariosFiltroDto filtro,
            CancellationToken ct = default);


    // Detalle de un movimiento y su pago asociado.
    Task<MovimientoBancarioDetalleDto?>
        ObtenerDetalleAsync(
            Guid movimientoId,
            CancellationToken ct = default);


    // Lo conservamos, aunque no lo usemos como KPI principal.
    Task<decimal> ObtenerSaldoAsync(
        Guid cuentaBancariaId,
        CancellationToken ct = default);
}