using BebesNadadores.Application.Bancos.Dtos;

namespace BebesNadadores.Application.Bancos;

public interface ICuentaBancariaService
{
    Task<IReadOnlyList<CuentaBancariaDto>>
        ListarAsync(
            bool incluirInactivas = false,
            CancellationToken ct = default);

    Task<CuentaBancariaDto?>
        ObtenerAsync(
            Guid id,
            CancellationToken ct = default);

    Task<Guid> CrearAsync(
        GuardarCuentaBancariaDto dto,
        CancellationToken ct = default);

    Task ActualizarAsync(
        Guid id,
        GuardarCuentaBancariaDto dto,
        CancellationToken ct = default);

    Task CambiarEstadoAsync(
        Guid id,
        bool activa,
        CancellationToken ct = default);
}