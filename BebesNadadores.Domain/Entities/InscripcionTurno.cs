namespace BebesNadadores.Domain.Entities;

public class InscripcionTurno
{
    public Guid InscripcionId { get; set; }
    public Inscripcion Inscripcion { get; set; } = null!;

    public Guid TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    public bool EsPrincipal { get; set; } = true;
}
