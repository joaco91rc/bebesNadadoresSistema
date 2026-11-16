using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Inscripciones;

public sealed record RegistrarReservaVacanteDto(
    Guid BebeId, Guid CicloLectivoId, Guid TurnoId,
    MedioPago MedioPago, Guid? CuentaBancariaId, Guid UsuarioId);

public sealed record ReservaVacanteItemDto(
    Guid Id, Guid BebeId, string Bebe, int Anio, Guid TurnoId,
    string Turno, decimal Importe, EstadoReservaVacanteCiclo Estado,
    DateTimeOffset FechaReservaUtc, Guid PagoId,
    Guid? InscripcionId = null, DateTimeOffset? FechaFormalizacionUtc = null);

/// <summary>Evaluación de una reserva antes de crear su inscripción al ciclo.</summary>
public sealed record ReservaFormalizacionItemDto(
    Guid ReservaId, string Bebe, string Turno, decimal Anticipo,
    bool YaAsociada, bool PuedeAsociarse, string Observacion);

public sealed record FormalizacionCicloResultadoDto(
    int Asociadas, int YaAsociadas, int Omitidas, IReadOnlyList<string> Advertencias);

/// <summary>
/// Disponibilidad real de cada turno al inicio del ciclo indicado.
/// Cada alumno ocupa exactamente un lugar aunque tenga reserva e inscripción vinculadas.
/// </summary>
public sealed record DisponibilidadTurnoCicloDto(
    Guid TurnoId, Guid NivelId, string Nivel, int NivelOrden,
    string Turno, DiaSemana Dia, TimeOnly Hora,
    int CupoTotal, int InscripcionesDirectas, int InscripcionesAsociadas,
    int ReservasPendientes, int Ocupados, int Disponibles);

public interface IReservaVacanteCicloService
{
    Task<IReadOnlyList<DisponibilidadTurnoCicloDto>> ObtenerDisponibilidadAsync(
        Guid cicloLectivoId, CancellationToken ct = default);
    Task<Guid> RegistrarAsync(RegistrarReservaVacanteDto datos, CancellationToken ct = default);
    Task<IReadOnlyList<ReservaVacanteItemDto>> ListarAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReservaFormalizacionItemDto>> PrevisualizarFormalizacionAsync(
        Guid cicloLectivoId, CancellationToken ct = default);
    Task<FormalizacionCicloResultadoDto> FormalizarInscripcionesAsync(
        Guid cicloLectivoId, Guid usuarioId, CancellationToken ct = default);
    Task CambiarTurnoAsync(Guid reservaId, Guid turnoDestinoId, Guid usuarioId,
        CancellationToken ct = default);
}
