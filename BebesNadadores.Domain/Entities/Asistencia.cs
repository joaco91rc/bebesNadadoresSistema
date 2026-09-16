using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Asistencia : EntidadAuditable
{
    public Guid SesionClaseId { get; set; }
    public SesionClase SesionClase { get; set; } = null!;

    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public Guid InscripcionId { get; set; }
    public Inscripcion Inscripcion { get; set; } = null!;

    public EstadoAsistencia Estado { get; set; } = EstadoAsistencia.Pendiente;
    public bool EsRecupero { get; set; }

    public string? Observaciones { get; set; }
    public DateTimeOffset? RegistradaUtc { get; set; }
    public Guid? RegistradaPorUsuarioId { get; set; }
}
