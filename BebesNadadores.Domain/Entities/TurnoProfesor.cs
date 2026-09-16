namespace BebesNadadores.Domain.Entities;

public class TurnoProfesor
{
    public Guid TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    public Guid ProfesorId { get; set; }
    public Profesor Profesor { get; set; } = null!;

    public bool EsPrincipal { get; set; }
}
