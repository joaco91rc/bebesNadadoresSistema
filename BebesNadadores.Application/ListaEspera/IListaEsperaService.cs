using BebesNadadores.Application.ListaEspera.Dtos;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.ListaEspera;

public interface IListaEsperaService
{
    Task<IReadOnlyList<ListaEsperaItemDto>> ListarAsync(bool incluirFinalizadas = false, CancellationToken ct = default);
    Task<IReadOnlyList<NivelEsperaDto>> ListarNivelesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<TurnoEsperaDto>> ListarTurnosAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BebeInscritoEsperaDto>> BuscarInscritosAsync(string texto, CancellationToken ct = default);
    Task<Guid> AgregarAsync(CrearListaEsperaDto dto, CancellationToken ct = default);
    Task ActualizarAsync(Guid id, ActualizarListaEsperaDto dto, CancellationToken ct = default);
    Task CambiarEstadoAsync(Guid id, EstadoListaEspera estado, Guid? bebeInscriptoId = null, CancellationToken ct = default);
}
