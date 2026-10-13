using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.AptosMedicos.Dtos;

public sealed record AptoMedicoBebeDto(
    Guid BebeId,
    string Bebe,
    DateOnly FechaNacimiento,

    Guid? NivelId,
    string? Nivel,

    SituacionAptoMedico Situacion,

    Guid? AptoMedicoId,
    ResultadoAptoMedico? Resultado,

    DateOnly? FechaRevision,
    DateOnly? VigenciaHasta,

    string? Observaciones);