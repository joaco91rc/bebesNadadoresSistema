using BebesNadadores.Application.Configuracion.Dtos;

namespace BebesNadadores.Application.Configuracion;

public interface IConfiguracionSistemaService
{
    Task<ConfiguracionSistemaDto> ObtenerAsync(
        CancellationToken ct = default);

    Task GuardarInstitucionAsync(
        GuardarInstitucionDto dto,
        Guid usuarioId,
        CancellationToken ct = default);

    Task GuardarReglasAsync(
        GuardarReglasSistemaDto dto,
        Guid usuarioId,
        CancellationToken ct = default);
}
