using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.AptosMedicos.Dtos;

public sealed record AptoMedicoHistorialDto(
    Guid Id,

    DateOnly FechaRevision,
    DateOnly VigenciaHasta,

    ResultadoAptoMedico Resultado,

    string? Observaciones,

    string? Medico);