using BebesNadadores.Application.Turnera.Dtos;
using BebesNadadores.Application.CiclosLectivos.Dtos;

namespace BebesNadadores.Application.Turnera;

public interface ITurneraService
{
    Task<IReadOnlyList<TurnoDto>> ListarTurnosAsync(
        bool incluirBajas = false,
        CancellationToken ct = default);


    Task<TurnoDto?> ObtenerTurnoAsync(
        Guid turnoId,
        CancellationToken ct = default);


    Task<Guid> CrearTurnoAsync(
        CrearTurnoDto dto,
        CancellationToken ct = default);


    Task ActualizarTurnoAsync(
        Guid turnoId,
        ActualizarTurnoDto dto,
        CancellationToken ct = default);


    Task CambiarEstadoTurnoAsync(
        Guid turnoId,
        Domain.Enums.EstadoTurno estado,
        CancellationToken ct = default);


    Task<GeneracionSesionesDto> GenerarSesionesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default);


    Task<IReadOnlyList<SesionClaseDto>> ObtenerAgendaAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default);


    Task<SesionClaseDetalleDto?> ObtenerSesionAsync(
        Guid sesionId,
        CancellationToken ct = default);


    Task CancelarSesionAsync(
        Guid sesionId,
        string motivo,
        CancellationToken ct = default);


    Task ReactivarSesionAsync(
        Guid sesionId,
        CancellationToken ct = default);


    Task FinalizarSesionAsync(
        Guid sesionId,
        CancellationToken ct = default);


    Task<IReadOnlyList<SesionClaseDto>>
        ObtenerClasesEnCursoAsync(
            DateTime fechaHora,
            CancellationToken ct = default);

    Task<IReadOnlyList<NivelTurneraDto>>
    ListarNivelesAsync(
        CancellationToken ct = default);


    Task<IReadOnlyList<ProfesorDisponibleDto>>
    ListarProfesoresAsync(
        CancellationToken ct = default);

        Task QuitarTurnoDeGrillaAsync(
    Guid turnoId,
    DateOnly aplicarDesde,
    CancellationToken ct = default);

    Task<IReadOnlyList<TurnoDto>>
    ListarTurnosCicloAsync(
        Guid cicloLectivoId,
        bool incluirBajas = false,
        CancellationToken ct = default);

        Task<PublicarCicloLectivoResultadoDto>
    PublicarCicloLectivoAsync(
        Guid cicloLectivoId,
        CancellationToken ct = default);

        Task ActualizarSesionClaseAsync(
    Guid sesionId,
    ActualizarSesionClaseDto dto,
    CancellationToken ct = default);
}