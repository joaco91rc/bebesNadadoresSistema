namespace BebesNadadores.Application.Caja.Dtos;

public sealed class CerrarCajaDto
{
    public decimal SaldoFinalDeclarado { get; set; }

    public string? Observaciones { get; set; }
}