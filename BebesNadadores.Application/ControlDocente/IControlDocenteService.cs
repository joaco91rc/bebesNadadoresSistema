using BebesNadadores.Application.ControlDocente.Dtos;

namespace BebesNadadores.Application.ControlDocente;

public interface IControlDocenteService
{
    Task<ControlDocenteDiaDto>
        ObtenerDiaAsync(
            DateOnly fecha,
            CancellationToken ct = default);


    Task ConfirmarProfesorAsync(
        ConfirmarProfesorClaseDto dto,
        CancellationToken ct = default);


    Task ConfirmarClaseCompletaAsync(
        Guid sesionClaseId,
        Guid? usuarioId,
        CancellationToken ct = default);


    Task ConfirmarDiaCompletoAsync(
        DateOnly fecha,
        Guid? usuarioId,
        CancellationToken ct = default);

         Task RegistrarReemplazoAsync(
    RegistrarReemplazoDocenteDto dto,
    CancellationToken ct = default);

    Task CorregirProfesorComoDictoAsync(
    Guid sesionClaseId,
    Guid profesorId,
    Guid? usuarioId,
    CancellationToken ct = default);
}