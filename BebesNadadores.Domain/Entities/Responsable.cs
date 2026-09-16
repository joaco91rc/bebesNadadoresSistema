using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class Responsable : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string? Dni { get; set; }
    public string Telefono { get; set; } = string.Empty;
    public string WhatsApp { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Domicilio { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<BebeResponsable> Bebes { get; set; } = new List<BebeResponsable>();
}
