using BebesNadadores.Application.Caja.Dtos;

namespace BebesNadadores.Application.Caja;

public interface ICajaService
{
    Task<CajaDetalleDto?>
        ObtenerHoyAsync(
            CancellationToken ct = default);


    Task<CajaDetalleDto?>
        ObtenerAsync(
            Guid cajaId,
            CancellationToken ct = default);


    Task<IReadOnlyList<CajaResumenDto>>
        ListarHistoricoAsync(
            DateOnly? desde = null,
            DateOnly? hasta = null,
            int cantidad = 90,
            CancellationToken ct = default);


    Task<Guid>
        AbrirAsync(
            AbrirCajaDto dto,
            Guid usuarioId,
            CancellationToken ct = default);


    Task<CerrarCajaResultadoDto>
        CerrarAsync(
            Guid cajaId,
            CerrarCajaDto dto,
            Guid usuarioId,
            CancellationToken ct = default);


    Task<Guid>
        RegistrarMovimientoManualAsync(
            Guid cajaId,
            RegistrarMovimientoCajaDto dto,
            CancellationToken ct = default);
}