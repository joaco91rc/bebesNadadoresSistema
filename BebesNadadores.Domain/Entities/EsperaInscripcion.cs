using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

/// <summary>Interesado en inscribirse. No representa un recupero ni requiere ser alumno.</summary>
public sealed class EsperaInscripcion : EntidadAuditable
{
    public string NombreBebe { get; set; } = string.Empty;
    public string NombreResponsable { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Email { get; set; }
    public Guid NivelId { get; set; }
    public Nivel Nivel { get; set; } = null!;
    public Guid? TurnoId { get; set; }
    public Turno? Turno { get; set; }
    public DiaSemana? DiaPreferido { get; set; }
    public FranjaHoraria FranjaHoraria { get; set; } = FranjaHoraria.Indistinta;
    public EstadoListaEspera Estado { get; set; } = EstadoListaEspera.Activa;
    public DateTimeOffset FechaAltaUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FechaNotificacionUtc { get; set; }
    public string? Observaciones { get; set; }
    public Guid? BebeInscriptoId { get; set; }
    public Bebe? BebeInscripto { get; set; }
}
