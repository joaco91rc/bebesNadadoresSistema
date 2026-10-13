using BebesNadadores.Application.Dashboard.Dtos;

namespace BebesNadadores.Application.Dashboard;

public interface IDashboardService
{
    Task<DashboardDto> ObtenerAsync(
        CancellationToken ct = default);
}
