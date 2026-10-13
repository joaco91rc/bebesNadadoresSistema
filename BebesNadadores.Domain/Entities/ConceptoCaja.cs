using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class ConceptoCaja : EntidadAuditable
{
    public string Nombre { get; set; } =
        string.Empty;

    public TipoMovimientoCaja Tipo { get; set; }

    public bool Activo { get; set; } =
        true;

    public int Orden { get; set; }
}