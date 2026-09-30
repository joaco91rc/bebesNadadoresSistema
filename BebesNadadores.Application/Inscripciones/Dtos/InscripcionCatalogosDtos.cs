using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Inscripciones.Dtos;

public sealed record PlanDisponibleDto(
    Guid Id,
    string Nombre,
    int VecesPorSemana,
    decimal? PrecioMensual);


public sealed record TurnoDisponibleDto(
    Guid TurnoId,
    string Nombre,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string Nivel,
    string? Sector,
    int Cupo,
    int Ocupados,
    int Disponibles)
{
    public bool TieneCupo =>
        Disponibles > 0;
}