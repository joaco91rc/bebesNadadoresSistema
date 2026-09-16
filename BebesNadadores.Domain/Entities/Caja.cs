using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Caja : EntidadAuditable
{
    public DateTimeOffset FechaAperturaUtc { get; set; }
    public DateTimeOffset? FechaCierreUtc { get; set; }

    public decimal SaldoInicial { get; set; }
    public decimal? SaldoFinalDeclarado { get; set; }

    public Guid UsuarioAperturaId { get; set; }
    public Guid? UsuarioCierreId { get; set; }

    public EstadoCaja Estado { get; set; } = EstadoCaja.Abierta;

    public string? Observaciones { get; set; }

    public ICollection<MovimientoCaja> Movimientos { get; set; } = new List<MovimientoCaja>();
    public ICollection<Pago> Pagos { get; set; } = new List<Pago>();
}
