using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

/// <summary>
/// Anticipo exclusivo para la primera cuota de un ciclo futuro.
/// Es un pago real, pero jamás debe imputarse a cuotas del ciclo anterior.
/// </summary>
public sealed class ReservaVacanteCiclo : EntidadAuditable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;
    public Guid CicloLectivoId { get; set; }
    public CicloLectivo CicloLectivo { get; set; } = null!;
    public Guid TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;
    // Se completa al asociar la reserva con una inscripción del ciclo.
    // No confundir con Estado=Aplicada: ese estado se refiere al ANTICIPO.
    public Guid? InscripcionId { get; set; }
    public Inscripcion? Inscripcion { get; set; }
    public DateTimeOffset? FechaFormalizacionUtc { get; set; }
    public Guid PagoId { get; set; }
    public Pago Pago { get; set; } = null!;
    public decimal Importe { get; set; } = 40000m;
    public EstadoReservaVacanteCiclo Estado { get; set; } = EstadoReservaVacanteCiclo.Confirmada;
    public DateTimeOffset FechaReservaUtc { get; set; } = DateTimeOffset.UtcNow;
}
