using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

/*
 * Auditoría de reactivaciones de cuotas.
 *
 * Caso típico: se anuló la CUOTA cuando se quería anular el PAGO.
 * Al reactivarla, la cuota vuelve a cobrarse y se limpian sus
 * datos de anulación. Antes de limpiarlos se guarda acá una "foto"
 * de la anulación que se deshizo, para que nunca se pierda quién
 * la había anulado, cuándo y por qué.
 *
 * Mismo criterio que CajaReapertura.
 */
public class CuotaReactivacion : EntidadBase
{
    public Guid CuotaId { get; set; }

    public Cuota Cuota { get; set; } = null!;


    // Reactivación
    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid UsuarioId { get; set; }

    public string Motivo { get; set; } = string.Empty;


    // Anulación que se deshizo
    // (pueden venir vacíos en cuotas anuladas antes de que existiera
    // la auditoría de anulaciones)
    public DateTimeOffset? AnuladaUtcAnterior { get; set; }

    public Guid? AnuladaPorUsuarioIdAnterior { get; set; }

    public string? MotivoAnulacionAnterior { get; set; }
}
