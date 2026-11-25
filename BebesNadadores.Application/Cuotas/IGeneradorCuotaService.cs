using BebesNadadores.Application.Cuotas.Dtos;

namespace BebesNadadores.Application.Cuotas;

public interface IGeneradorCuotasService
{
    Task<GeneracionCuotaIndividualDto> GenerarParaBebeAsync(
        Guid bebeId,
        DateOnly periodo,
        Guid usuarioId,
        CancellationToken ct = default);

    Task<GeneracionCuotasResultadoDto> GenerarPeriodosAsync(DateOnly inicio, int cantidad,
        Guid? bebeId = null, Guid? usuarioId = null, CancellationToken ct = default);

    Task<GeneracionCuotasResultadoDto> GenerarAsync(
        DateOnly periodo,
        CancellationToken ct = default);
}