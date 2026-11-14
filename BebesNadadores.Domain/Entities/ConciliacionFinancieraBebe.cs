namespace BebesNadadores.Domain.Entities;

/// <summary>
/// Evidencia de incertidumbre histórica al corte de la última migración validada.
/// No almacena deuda ni reemplaza Cuotas/Pagos/ImputacionesPago.
/// </summary>
public sealed class ConciliacionFinancieraBebe
{
    public Guid BebeId { get; set; }
    public DateOnly FechaCorte { get; set; }
    public int MesesVencidosSinConciliar { get; set; }
    public string CodigoMigracion { get; set; } = string.Empty;
    public DateTimeOffset ImportadaUtc { get; set; }
}
