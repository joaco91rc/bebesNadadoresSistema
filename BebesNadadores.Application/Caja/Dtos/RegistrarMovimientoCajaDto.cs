using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Caja.Dtos;

public sealed class RegistrarMovimientoCajaDto
{
    public Guid ConceptoCajaId { get; set; }

    public decimal Importe { get; set; }

    public string? Detalle { get; set; }

    public string? Referencia { get; set; }
}