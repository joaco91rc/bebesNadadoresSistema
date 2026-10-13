using BebesNadadores.Application.AptosMedicos.Dtos;

namespace BebesNadadores.Application.AptosMedicos;

public interface IAptoMedicoService
{
    Task<AptoMedicoResumenDto> ObtenerResumenAsync(
        CancellationToken ct = default);


    Task<IReadOnlyList<FranjaMedicaDto>>
        ObtenerAgendaMedicaAsync(
            DateOnly fecha,
            TimeOnly horaActual,
            CancellationToken ct = default);


    Task<IReadOnlyList<AptoMedicoBebeDto>>
        ListarBebesAsync(
            SituacionAptoMedico? situacion = null,
            Guid? nivelId = null,
            string? buscar = null,
            CancellationToken ct = default);


    Task<IReadOnlyList<AptoMedicoHistorialDto>>
    ObtenerHistorialAsync(
        Guid bebeId,
        CancellationToken ct = default);


   Task<Guid> RegistrarAsync(
    GuardarAptoMedicoDto dto,
    Guid medicoUsuarioId,
    CancellationToken ct = default);
}