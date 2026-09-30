using BebesNadadores.Application.Bebes.Dtos;

namespace BebesNadadores.Application.Bebes;

public interface IBebeService
{
    Task<IReadOnlyList<BebeDto>> ListarAsync(
        bool incluirBajas = false,
        CancellationToken ct = default);


    Task<BebeDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default);


    Task<Guid> CrearAsync(
        GuardarBebeDto dto,
        CancellationToken ct = default);


    Task ActualizarAsync(
        Guid id,
        GuardarBebeDto dto,
        CancellationToken ct = default);


    Task DarDeBajaAsync(
        Guid id,
        DarDeBajaBebeDto dto,
        CancellationToken ct = default);


    Task ReactivarAsync(
        Guid id,
        CancellationToken ct = default);


    Task<IReadOnlyList<CumpleaniosBebeDto>>
        ObtenerCumpleaniosMesAsync(
            int anio,
            int mes,
            CancellationToken ct = default);
}