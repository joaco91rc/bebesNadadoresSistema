using BebesNadadores.Application.Reportes.Dtos;

namespace BebesNadadores.Application.Reportes;

public interface IReporteService
{
    Task<ReporteGeneralDto> ObtenerAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default);
}
