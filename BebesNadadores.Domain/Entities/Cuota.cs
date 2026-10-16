using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Cuota : EntidadAuditable
{
    public Guid InscripcionId { get; set; }
    public Inscripcion Inscripcion { get; set; } = null!;

    // Convención: usar el día 1 del mes. Ej.: 2026-09-01.
    public DateOnly Periodo { get; set; }

    public DateOnly FechaVencimiento { get; set; }
    public decimal Importe { get; set; }

    public EstadoCuota Estado { get; set; } = EstadoCuota.Pendiente;
    public string? Descripcion { get; set; }

    public ICollection<ImputacionPago> Imputaciones { get; set; } = new List<ImputacionPago>();

    public decimal ImportePagado => Imputaciones.Sum(x => x.Importe);
    public decimal Saldo => Math.Max(0, Importe - ImportePagado);


    // =========================================================
    // ANULACIÓN
    //
    // La cuota no se borra: Estado = Anulada y queda registrado
    // quién, cuándo y por qué.
    // =========================================================

    public DateTimeOffset? AnuladaUtc { get; set; }

    public Guid? AnuladaPorUsuarioId { get; set; }

    public string? MotivoAnulacion { get; set; }


    // =========================================================
    // REACTIVACIONES
    //
    // Si se anuló por error, se puede reactivar. Cada reactivación
    // guarda la anulación que se deshizo (ver CuotaReactivacion).
    // =========================================================

    public ICollection<CuotaReactivacion> Reactivaciones { get; set; }
        = new List<CuotaReactivacion>();
}
