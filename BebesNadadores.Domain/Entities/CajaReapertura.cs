using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

/*
 * Auditoría de reaperturas de caja.
 *
 * Al reabrir, la caja vuelve a Abierta y se limpia su cierre
 * (saldo declarado, fecha y usuario). Antes de limpiarlo se
 * guarda acá una "foto" del cierre anterior, para que nunca se
 * pierda quién la había cerrado, cuándo y con qué saldo.
 *
 * Los movimientos no se tocan: siguen vinculados a la misma caja.
 */
public class CajaReapertura : EntidadBase
{
    public Guid CajaId { get; set; }

    public Caja Caja { get; set; } = null!;


    // Reapertura
    public DateTimeOffset FechaUtc { get; set; } = DateTimeOffset.UtcNow;

    public Guid UsuarioId { get; set; }

    public string Motivo { get; set; } = string.Empty;


    // Cierre que se deshizo
    public DateTimeOffset FechaCierreAnteriorUtc { get; set; }

    public Guid UsuarioCierreAnteriorId { get; set; }

    public decimal SaldoFinalDeclaradoAnterior { get; set; }
}
