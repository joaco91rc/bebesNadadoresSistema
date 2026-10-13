using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class CuentaBancaria : EntidadAuditable
{
    public Guid BancoId { get; set; }

    public Banco Banco { get; set; }
        = null!;


    /*
     * Nombre interno para identificarla rápidamente.
     *
     * Ej:
     * "Provincia Principal"
     * "Mercado Pago"
     * "Galicia Recaudación"
     */
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


    /*
     * Nos permite empezar a usar el sistema
     * con una cuenta que ya tenía dinero.
     */
    public decimal SaldoInicial { get; set; }


    public bool Activa { get; set; }
        = true;


    public ICollection<MovimientoBancario>
        Movimientos { get; set; }
            = new List<MovimientoBancario>();
}