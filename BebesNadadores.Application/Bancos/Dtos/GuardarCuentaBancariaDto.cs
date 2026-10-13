using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bancos.Dtos;

public sealed class GuardarCuentaBancariaDto
{
    public Guid BancoId { get; set; }

    public string Nombre { get; set; }
        = string.Empty;

    public TipoCuentaBancaria Tipo { get; set; }
        = TipoCuentaBancaria.CajaAhorro;

    public string? NumeroCuenta { get; set; }

    public string? CbuCvu { get; set; }

    public string? Alias { get; set; }

    public string Titular { get; set; }
        = string.Empty;

    public string Moneda { get; set; }
        = "ARS";

    public decimal SaldoInicial { get; set; }

    public bool Activa { get; set; }
        = true;
}