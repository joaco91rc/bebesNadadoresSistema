using BebesNadadores.Application.Bancos.Dtos;

namespace BebesNadadores.Application.Bancos;

public interface IBancoService
{
    Task<IReadOnlyList<BancoDto>>
        ListarAsync(
            bool incluirInactivos = false,
            CancellationToken ct = default);

    Task<Guid> CrearAsync(
        GuardarBancoDto dto,
        CancellationToken ct = default);

    Task ActualizarAsync(
        Guid id,
        GuardarBancoDto dto,
        CancellationToken ct = default);

    Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default);
}