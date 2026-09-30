using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Inscripciones.Dtos;

public sealed class CambiarInscripcionDto
{
    public Guid NivelId { get; set; }

    public Guid PlanId { get; set; }

    public DateOnly AplicarDesde { get; set; }
        = DateOnly.FromDateTime(DateTime.Today);

    public MedioPago MedioPagoPreferido { get; set; }
        = MedioPago.Transferencia;

    public List<Guid> TurnosIds { get; set; } = [];


    // =========================================================
    // DESCUENTO MANUAL
    // =========================================================

    public TipoDescuento? TipoDescuento { get; set; }

    public decimal? ValorDescuento { get; set; }

    public string? MotivoDescuento { get; set; }
}