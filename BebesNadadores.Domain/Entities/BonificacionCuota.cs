using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

// No representa dinero: es una reducción de deuda, con auditoría propia.
public sealed class BonificacionCuota : EntidadBase
{
    public Guid CuotaId { get; set; }
    public Cuota Cuota { get; set; } = null!;
    public decimal Importe { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid UsuarioId { get; set; }
    public DateTimeOffset? AnuladaUtc { get; set; }
    public Guid? AnuladaPorUsuarioId { get; set; }
    public string? MotivoAnulacion { get; set; }
}
