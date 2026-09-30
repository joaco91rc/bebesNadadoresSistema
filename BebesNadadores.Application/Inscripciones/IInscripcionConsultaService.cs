using BebesNadadores.Application.Inscripciones.Dtos;

namespace BebesNadadores.Application.Inscripciones;

public interface IInscripcionConsultaService
{
    Task<IReadOnlyList<PlanDisponibleDto>> ListarPlanesAsync(
        DateOnly fecha,
        CancellationToken ct = default);


    Task<IReadOnlyList<TurnoDisponibleDto>> ListarTurnosDisponiblesAsync(
        Guid nivelId,
        DateOnly fechaInicio,
        CancellationToken ct = default);
}