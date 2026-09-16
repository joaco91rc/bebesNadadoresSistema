using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class TarifaPlan : EntidadAuditable
{
    public Guid PlanId { get; set; }
    public Plan Plan { get; set; } = null!;

    public decimal PrecioMensual { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
}
