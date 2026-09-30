using BebesNadadores.Application.Turnera.Dtos;

namespace BebesNadadores.Application.Turnera;

public interface IDiaNoLaborableService
{
    Task<IReadOnlyList<DiaNoLaborableDto>> ListarAsync(
        int anio,
        CancellationToken ct = default);


    Task<Guid> CrearAsync(
        GuardarDiaNoLaborableDto dto,
        CancellationToken ct = default);


    Task ActualizarAsync(
        Guid id,
        GuardarDiaNoLaborableDto dto,
        CancellationToken ct = default);


    Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default);
}