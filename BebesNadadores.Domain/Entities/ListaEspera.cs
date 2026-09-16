using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class ListaEspera : EntidadAuditable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public Guid NivelId { get; set; }
    public Nivel Nivel { get; set; } = null!;

    public Guid? TurnoId { get; set; }
    public Turno? Turno { get; set; }

    public Guid? RecuperacionId { get; set; }
    public Recuperacion? Recuperacion { get; set; }

    public DiaSemana? DiaPreferido { get; set; }
    public FranjaHoraria FranjaHoraria { get; set; } = FranjaHoraria.Indistinta;

    public EstadoListaEspera Estado { get; set; } = EstadoListaEspera.Activa;
    public DateTimeOffset FechaAltaUtc { get; set; } = DateTimeOffset.UtcNow;

    public string? Observaciones { get; set; }
}
