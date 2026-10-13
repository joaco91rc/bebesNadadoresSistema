using BebesNadadores.Application.Pagos.Dtos;

namespace BebesNadadores.Application.Pagos;

public interface IPagoService
{
    Task<IReadOnlyList<BebePagoResumenDto>>
        ListarBebesAsync(
            string? buscar = null,
            CancellationToken ct = default);


    Task<CuentaCorrienteBebeDto?>
        ObtenerCuentaCorrienteAsync(
            Guid bebeId,
            CancellationToken ct = default);


    Task<PreviewPagoDto>
        PrevisualizarAsync(
            RegistrarPagoDto dto,
            CancellationToken ct = default);


    Task<RegistrarPagoResultadoDto>
        RegistrarAsync(
            RegistrarPagoDto dto,
            Guid usuarioId,
            CancellationToken ct = default);


    Task AplicarSaldosAFavorAsync(
        Guid bebeId,
        CancellationToken ct = default);
}