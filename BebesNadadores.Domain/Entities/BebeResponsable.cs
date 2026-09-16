using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class BebeResponsable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public Guid ResponsableId { get; set; }
    public Responsable Responsable { get; set; } = null!;

    public RelacionResponsable Relacion { get; set; }
    public bool EsContactoPrincipal { get; set; }
    public bool AutorizaComunicaciones { get; set; } = true;
}
