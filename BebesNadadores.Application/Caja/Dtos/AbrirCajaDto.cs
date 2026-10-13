namespace BebesNadadores.Application.Caja.Dtos;

public sealed class AbrirCajaDto
{
    public decimal SaldoInicial { get; set; }

    public string? Observaciones { get; set; }
}