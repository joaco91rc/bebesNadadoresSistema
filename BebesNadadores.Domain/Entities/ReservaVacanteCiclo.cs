using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

/// <summary>
/// Lugar apartado para un ciclo futuro. La reserva puede existir antes del pago.
/// Si PagoId es null, se ocupa el cupo, pero NO hay anticipo cobrado.
/// Estado = Confirmada/Aplicada/Cancelada conserva el significado original;
/// el estado de cobro se obtiene de PagoId.
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
    // Estado = Aplicada se refiere al anticipo, no a esta asociación.
    public Guid? InscripcionId { get; set; }
    public Inscripcion? Inscripcion { get; set; }
    public DateTimeOffset? FechaFormalizacionUtc { get; set; }

    // Puede no haberse cobrado todavía.
    public Guid? PagoId { get; set; }
    public Pago? Pago { get; set; }

    // Es el importe a cobrar por la reserva, NO dinero ingresado hasta que PagoId tenga valor.
    public decimal Importe { get; set; } = 40000m;
    public EstadoReservaVacanteCiclo Estado { get; set; } = EstadoReservaVacanteCiclo.Confirmada;
    public DateTimeOffset FechaReservaUtc { get; set; } = DateTimeOffset.UtcNow;
}
