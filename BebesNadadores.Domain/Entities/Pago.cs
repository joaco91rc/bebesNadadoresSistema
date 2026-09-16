using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Pago : EntidadAuditable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public Guid? ResponsableId { get; set; }
    public Responsable? Responsable { get; set; }

    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;
    public decimal Importe { get; set; }

    public MedioPago MedioPago { get; set; }
    public string? ReferenciaExterna { get; set; }
    public string? Observaciones { get; set; }

    public Guid? CajaId { get; set; }
    public Caja? Caja { get; set; }

    public ICollection<ImputacionPago> Imputaciones { get; set; } = new List<ImputacionPago>();
}
