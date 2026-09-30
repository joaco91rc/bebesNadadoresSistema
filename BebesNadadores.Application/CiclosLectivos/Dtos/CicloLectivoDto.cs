using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.CiclosLectivos.Dtos;

public sealed record CicloLectivoDto(
    Guid Id,
    int Anio,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    EstadoCicloLectivo Estado,
    DateTimeOffset? FechaPublicacionUtc,
    string? Observaciones,
    int CantidadTurnos,
    int CantidadSesiones);