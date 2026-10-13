using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bancos.Dtos;

public sealed class MovimientosBancariosFiltroDto
{
    public Guid CuentaBancariaId { get; set; }

    public DateOnly? Desde { get; set; }

    public DateOnly? Hasta { get; set; }

    public TipoMovimientoBancario? Tipo { get; set; }

    public OrigenMovimientoBancario? Origen { get; set; }

    public string? Buscar { get; set; }
}