using BebesNadadores.Application.Recuperos.Dtos;

namespace BebesNadadores.Application.Recuperos;

public interface IRecuperosService
{
    Task<RecuperosPanelDto> ObtenerPanelAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default);


    Task<IReadOnlyList<CandidatoRecuperoDto>>
        ListarCandidatosAsync(
            Guid sesionClaseId,
            CancellationToken ct = default);
Task<IReadOnlyList<VacanteRecuperoDto>>
    ListarClasesCompatiblesAsync(
        Guid recuperacionId,
        DateOnly desde,
        DateOnly hasta,
        bool respetarDiaPreferido = true,
        CancellationToken ct = default);

    Task ReservarAsync(
        Guid recuperacionId,
        Guid sesionClaseId,
        CancellationToken ct = default);


    Task LiberarReservaAsync(
        Guid recuperacionId,
        CancellationToken ct = default);
}