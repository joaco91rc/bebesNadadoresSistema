using BebesNadadores.Application.Configuracion.Dtos;

namespace BebesNadadores.Application.Configuracion;

public interface IUsuarioAdministracionService
{
    Task<IReadOnlyList<UsuarioSistemaDto>> ListarAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListarRolesAsync(
        CancellationToken ct = default);

    Task<IReadOnlyList<ProfesorParaUsuarioDto>>
        ListarProfesoresAsync(
            CancellationToken ct = default);

    Task<Guid> GuardarAsync(
        GuardarUsuarioSistemaDto dto,
        Guid usuarioActualId,
        CancellationToken ct = default);

    Task ResetearPasswordAsync(
        Guid usuarioId,
        string nuevaPassword,
        CancellationToken ct = default);
}
