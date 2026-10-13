using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Caja.Dtos;

public sealed class GuardarConceptoCajaDto
{
    public string Nombre { get; set; } =
        string.Empty;

    public TipoMovimientoCaja Tipo { get; set; }

    public bool Activo { get; set; } =
        true;

    public int Orden { get; set; }
}