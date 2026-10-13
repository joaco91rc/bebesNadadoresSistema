using BebesNadadores.Application.Proveedores.Dtos;

namespace BebesNadadores.Application.Proveedores;

public interface IPagoProveedorService
{
    Task<IReadOnlyList<PagoProveedorDto>>
        ListarAsync(
            Guid proveedorId,
            CancellationToken ct = default);

    Task<Guid>
        RegistrarAsync(
            RegistrarPagoProveedorDto dto,
            Guid usuarioId,
            CancellationToken ct = default);
}