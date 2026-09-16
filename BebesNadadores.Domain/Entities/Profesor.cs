using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class Profesor : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Observaciones { get; set; }

    // Vinculación opcional con Identity sin acoplar Domain a AppUser.
    public Guid? UsuarioId { get; set; }

    public bool Activo { get; set; } = true;

    public ICollection<TurnoProfesor> Turnos { get; set; } = new List<TurnoProfesor>();
}
