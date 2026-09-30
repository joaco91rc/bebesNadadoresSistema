using BebesNadadores.Application.Asistencia.Dtos;

namespace BebesNadadores.Application.Asistencia;

public interface IAsistenciaService
{
    Task<AsistenciaSesionDto?> ObtenerSesionAsync(
        Guid sesionId,
        CancellationToken ct = default);


    Task MarcarAsync(
        RegistrarAsistenciaDto dto,
        CancellationToken ct = default);


    Task MarcarMasivoAsync(
        RegistrarAsistenciaMasivaDto dto,
        CancellationToken ct = default);

        Task<IReadOnlyList<AsistenciaClaseDiaDto>>
    ListarSesionesDiaAsync(
        DateOnly fecha,
        CancellationToken ct = default);
}