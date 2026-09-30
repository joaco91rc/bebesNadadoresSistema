using BebesNadadores.Application.Bebes.Dtos;

namespace BebesNadadores.Application.Bebes;

public interface IBebeLegajoService
{
    Task<BebeLegajoDto?> ObtenerAsync(
        Guid bebeId,
        CancellationToken ct = default);
}