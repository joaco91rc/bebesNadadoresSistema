using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Domain.Entities;
namespace BebesNadadores.Domain.Entities;

public class SesionClase : EntidadAuditable
{
    public Guid TurnoId { get; set; }

    public Turno Turno { get; set; } = null!;


    public DateOnly Fecha { get; set; }


    // Snapshot histórico del turno
    public TimeOnly HoraInicio { get; set; }

    public int DuracionMinutos { get; set; }


    public Guid NivelId { get; set; }

    public Nivel Nivel { get; set; } = null!;


    public int Cupo { get; set; }

    public string? Sector { get; set; }


    public EstadoSesionClase Estado { get; set; }
        = EstadoSesionClase.Programada;


    public string? MotivoCancelacion { get; set; }


    /*
     * Si fue cancelada por feriado,
     * cierre del natatorio, mantenimiento, etc.
     */
    public Guid? DiaNoLaborableId { get; set; }

    public DiaNoLaborable? DiaNoLaborable { get; set; }


    public TimeOnly HoraFin =>
        HoraInicio.AddMinutes(DuracionMinutos);


    public ICollection<Asistencia> Asistencias { get; set; }
        = new List<Asistencia>();

    public ICollection<InvitacionVacante> InvitacionesVacante { get; set; }
        = new List<InvitacionVacante>();

        public bool EsModificadaManualmente { get; set; }

public ICollection<SesionClaseProfesor> Profesores { get; set; }
    = new List<SesionClaseProfesor>();

    public ICollection<ReservaSesionClase> Reservas { get; set; }
    = new List<ReservaSesionClase>();
}