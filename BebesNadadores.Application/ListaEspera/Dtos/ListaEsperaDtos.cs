using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.ListaEspera.Dtos;

public sealed record NivelEsperaDto(Guid Id, string Nombre);
public sealed record TurnoEsperaDto(Guid Id, Guid NivelId, string Nombre, DiaSemana Dia, TimeOnly Hora);
public sealed record ListaEsperaItemDto(
    Guid Id, string NombreBebe, string NombreResponsable, string Telefono, string? Email,
    Guid NivelId, string Nivel, Guid? TurnoId, string? Turno,
    DiaSemana? DiaPreferido, FranjaHoraria FranjaHoraria,
    EstadoListaEspera Estado, DateTimeOffset FechaAltaUtc,
    DateTimeOffset? FechaNotificacionUtc, string? Observaciones, Guid? BebeInscriptoId);

public  class CrearListaEsperaDto
{
    public string NombreBebe { get; set; } = string.Empty;
    public string NombreResponsable { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid NivelId { get; set; }
    public Guid? TurnoId { get; set; }
    public DiaSemana? DiaPreferido { get; set; }
    public FranjaHoraria FranjaHoraria { get; set; }
    public string? Observaciones { get; set; }
}
public sealed class ActualizarListaEsperaDto : CrearListaEsperaDto { }

public sealed record BebeInscritoEsperaDto(Guid Id, string Nombre);
