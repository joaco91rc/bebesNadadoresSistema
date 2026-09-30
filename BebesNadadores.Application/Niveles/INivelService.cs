using BebesNadadores.Application.Niveles.Dtos;

namespace BebesNadadores.Application.Niveles;

public interface INivelService
{
    Task<IReadOnlyList<NivelDto>> ListarAsync(
        bool incluirInactivos = true,
        CancellationToken ct = default);

    Task<NivelDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default);

    Task<Guid> CrearAsync(
        GuardarNivelDto dto,
        CancellationToken ct = default);

    Task ActualizarAsync(
        Guid id,
        GuardarNivelDto dto,
        CancellationToken ct = default);

    Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default);
}