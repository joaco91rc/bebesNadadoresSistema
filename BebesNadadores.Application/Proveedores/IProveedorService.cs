using BebesNadadores.Application.Proveedores.Dtos;

namespace BebesNadadores.Application.Proveedores;

public interface IProveedorService
{
    Task<IReadOnlyList<ProveedorDto>>
        ListarAsync(
            bool incluirInactivos = false,
            CancellationToken ct = default);

    Task<ProveedorDto?>
        ObtenerAsync(
            Guid id,
            CancellationToken ct = default);

    Task<Guid>
        CrearAsync(
            GuardarProveedorDto dto,
            CancellationToken ct = default);

    Task ActualizarAsync(
        Guid id,
        GuardarProveedorDto dto,
        CancellationToken ct = default);

    Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default);
}