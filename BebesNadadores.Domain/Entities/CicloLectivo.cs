using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class CicloLectivo : EntidadAuditable
{
    public int Anio { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public EstadoCicloLectivo Estado { get; set; }
        = EstadoCicloLectivo.Borrador;

    public DateTimeOffset? FechaPublicacionUtc { get; set; }

    public string? Observaciones { get; set; }
public ICollection<Turno> Turnos { get; set; }  = new List<Turno>();
}