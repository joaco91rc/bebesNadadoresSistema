using BebesNadadores.Application.Cuotas.Dtos;

namespace BebesNadadores.Application.Cuotas;

public interface IGeneradorCuotasService
{
    Task<GeneracionCuotasResultadoDto> GenerarAsync(
        DateOnly periodo,
        CancellationToken ct = default);
}