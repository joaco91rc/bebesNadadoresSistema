using BebesNadadores.Application.Inscripciones.Dtos;

namespace BebesNadadores.Application.Inscripciones;

public interface IInscripcionService
{
    Task CambiarAsync(
        Guid bebeId,
        CambiarInscripcionDto dto,
        CancellationToken ct = default);


    Task CrearPrimeraAsync(
        Guid bebeId,
        CambiarInscripcionDto dto,
        CancellationToken ct = default);
}