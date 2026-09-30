using BebesNadadores.Application.CiclosLectivos.Dtos;

namespace BebesNadadores.Application.CiclosLectivos;

public interface ICicloLectivoService
{
    Task<IReadOnlyList<CicloLectivoDto>> ListarAsync(
        CancellationToken ct = default);

    Task<CicloLectivoDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default);

    Task<CicloLectivoDto?> ObtenerPorAnioAsync(
        int anio,
        CancellationToken ct = default);

    Task<Guid> CrearAsync(
        GuardarCicloLectivoDto dto,
        CancellationToken ct = default);

    Task ActualizarAsync(
        Guid id,
        GuardarCicloLectivoDto dto,
        CancellationToken ct = default);

    Task CerrarAsync(
        Guid id,
        CancellationToken ct = default);

       
}