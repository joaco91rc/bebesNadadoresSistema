namespace BebesNadadores.Application.AptosMedicos.Dtos;

public sealed record ClaseMedicaDto(
    Guid SesionClaseId,
    Guid TurnoId,

    string Turno,
    string Nivel,

    TimeOnly HoraInicio,
    TimeOnly HoraFin,

    IReadOnlyList<string> Profesores,

    IReadOnlyList<AptoMedicoBebeDto> Bebes);