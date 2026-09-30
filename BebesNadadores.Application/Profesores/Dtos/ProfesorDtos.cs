using BebesNadadores.Domain.Enums;
namespace BebesNadadores.Application.Profesores.Dtos;

public sealed record ProfesorDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string NombreCompleto,
    string? Telefono,
    string? Email,
    string? Observaciones,
    Guid? UsuarioId,
    bool Activo,
    int TurnosActivos);


public sealed record HorarioProfesorDto(
    Guid TurnoId,
    Guid ProfesorId,
    string Profesor,
    Guid NivelId,
    string Nivel,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    int DuracionMinutos,
    string? Sector,
    bool EsPrincipal,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta);
public sealed class GuardarProfesorDto
{
    public string Nombre { get; set; } = string.Empty;

    public string Apellido { get; set; } = string.Empty;

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? Observaciones { get; set; }

    public bool Activo { get; set; } = true;
}

