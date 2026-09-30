using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.ControlDocente.Dtos;

public sealed record ClaseControlDocenteDto(
    Guid SesionClaseId,
    Guid TurnoId,
    string Turno,
    string Nivel,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    EstadoSesionClase Estado,
    IReadOnlyList<ProfesorControlDocenteDto> Profesores);