using BebesNadadores.Application.Cuotas.Dtos;

namespace BebesNadadores.Application.Cuotas;

public interface IBonificacionCuotaService
{
    Task BonificarAsync(Guid cuotaId, decimal importe, string motivo, Guid usuarioId, CancellationToken ct = default);
    Task RevertirAsync(Guid bonificacionId, string motivo, Guid usuarioId, CancellationToken ct = default);
    Task<IReadOnlyList<BonificacionCuotaDto>> ListarAsync(DateOnly? periodo = null, CancellationToken ct = default);
}
