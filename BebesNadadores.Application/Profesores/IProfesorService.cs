using BebesNadadores.Application.Profesores.Dtos;

namespace BebesNadadores.Application.Profesores;

public interface IProfesorService
{
    Task<IReadOnlyList<ProfesorDto>> ListarAsync(
        bool incluirInactivos = true,
        CancellationToken ct = default);


    Task<ProfesorDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default);


    Task<Guid> CrearAsync(
        GuardarProfesorDto dto,
        CancellationToken ct = default);


    Task ActualizarAsync(
        Guid id,
        GuardarProfesorDto dto,
        CancellationToken ct = default);


    Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default);

        Task<IReadOnlyList<HorarioProfesorDto>>
    ConsultarHorariosAsync(
        Guid profesorId,
        Guid? nivelId = null,
        CancellationToken ct = default);
}