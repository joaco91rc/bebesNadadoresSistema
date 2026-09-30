using BebesNadadores.Application.ListaEspera.Dtos;

namespace BebesNadadores.Application.ListaEspera;

public interface IListaEsperaService
{
    Task<IReadOnlyList<BebeListaEsperaBusquedaDto>>
        BuscarBebesAsync(
            string? texto,
            int maxResultados = 10,
            CancellationToken ct = default);


    Task<IReadOnlyList<ListaEsperaItemDto>>
        ListarAsync(
            bool incluirFinalizadas = false,
            CancellationToken ct = default);


    Task<Guid> AgregarAsync(
        CrearListaEsperaDto dto,
        CancellationToken ct = default);


    Task ActualizarAsync(
        Guid listaEsperaId,
        ActualizarListaEsperaDto dto,
        CancellationToken ct = default);


    Task CancelarAsync(
        Guid listaEsperaId,
        CancellationToken ct = default);
}