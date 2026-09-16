using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class InvitacionVacante : EntidadAuditable
{
    public Guid SesionClaseId { get; set; }
    public SesionClase SesionClase { get; set; } = null!;

    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public Guid ResponsableId { get; set; }
    public Responsable Responsable { get; set; } = null!;

    public Guid? RecuperacionId { get; set; }
    public Recuperacion? Recuperacion { get; set; }

    public CanalNotificacion Canal { get; set; }

    // Se envía dentro del link público. Debe ser imposible de adivinar.
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public EstadoInvitacionVacante Estado { get; set; } = EstadoInvitacionVacante.Pendiente;

    public DateTimeOffset? EnviadaUtc { get; set; }
    public DateTimeOffset? RespondidaUtc { get; set; }
    public DateTimeOffset VenceUtc { get; set; }
}
