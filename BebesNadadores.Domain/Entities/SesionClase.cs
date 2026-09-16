using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class SesionClase : EntidadAuditable
{
    public Guid TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    public DateOnly Fecha { get; set; }

    // Snapshot para conservar el histórico aunque luego cambie el turno base.
    public TimeOnly HoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    public Guid NivelId { get; set; }
    public Nivel Nivel { get; set; } = null!;

    public int Cupo { get; set; }
    public string? Sector { get; set; }

    public EstadoSesionClase Estado { get; set; } = EstadoSesionClase.Programada;
    public string? MotivoCancelacion { get; set; }

    public ICollection<Asistencia> Asistencias { get; set; } = new List<Asistencia>();
    public ICollection<InvitacionVacante> InvitacionesVacante { get; set; } = new List<InvitacionVacante>();
}
