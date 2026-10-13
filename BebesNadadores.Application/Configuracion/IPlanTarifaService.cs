using BebesNadadores.Application.Configuracion.Dtos;

namespace BebesNadadores.Application.Configuracion;

public interface IPlanTarifaService
{
    Task<IReadOnlyList<PlanTarifaDto>> ListarAsync(
        CancellationToken ct = default);

    Task<Guid> GuardarPlanAsync(
        GuardarPlanDto dto,
        Guid usuarioId,
        CancellationToken ct = default);

    Task CambiarEstadoPlanAsync(
        Guid planId,
        bool activo,
        Guid usuarioId,
        CancellationToken ct = default);

    Task<Guid> GuardarTarifaAsync(
        GuardarTarifaPlanDto dto,
        Guid usuarioId,
        CancellationToken ct = default);
}
