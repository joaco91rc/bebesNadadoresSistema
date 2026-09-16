using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Turno : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;

    public DiaSemana DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public int DuracionMinutos { get; set; }

    public Guid NivelId { get; set; }
    public Nivel Nivel { get; set; } = null!;

    public int Cupo { get; set; }
    public string? Sector { get; set; }
    public EstadoTurno Estado { get; set; } = EstadoTurno.Activo;

    public TimeOnly HoraFin => HoraInicio.AddMinutes(DuracionMinutos);

    public ICollection<TurnoProfesor> Profesores { get; set; } = new List<TurnoProfesor>();
    public ICollection<InscripcionTurno> Inscripciones { get; set; } = new List<InscripcionTurno>();
    public ICollection<SesionClase> Sesiones { get; set; } = new List<SesionClase>();
}
