using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Inscripcion : EntidadAuditable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public DateOnly FechaInicio { get; set; }
    public DateOnly? FechaFin { get; set; }

    public EstadoInscripcion Estado { get; set; } = EstadoInscripcion.Activa;
    public MedioPago MedioPagoPreferido { get; set; } = MedioPago.Transferencia;

    public string? Observaciones { get; set; }

    public ICollection<InscripcionTurno> Turnos { get; set; } = new List<InscripcionTurno>();
    public ICollection<Cuota> Cuotas { get; set; } = new List<Cuota>();
}
