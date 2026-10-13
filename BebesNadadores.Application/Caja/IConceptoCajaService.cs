using BebesNadadores.Application.Caja.Dtos;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Caja;

public interface IConceptoCajaService
{
    Task<IReadOnlyList<ConceptoCajaDto>>
        ListarAsync(
            TipoMovimientoCaja? tipo = null,
            bool incluirInactivos = false,
            CancellationToken ct = default);


    Task<Guid>
        CrearAsync(
            GuardarConceptoCajaDto dto,
            CancellationToken ct = default);


    Task ActualizarAsync(
        Guid id,
        GuardarConceptoCajaDto dto,
        CancellationToken ct = default);


    Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default);

        Task AsegurarConceptosSistemaAsync(
    CancellationToken ct = default);
}