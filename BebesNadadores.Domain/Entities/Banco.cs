using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class Banco : EntidadAuditable
{
    public string Nombre { get; set; }
        = string.Empty;

    public bool Activo { get; set; }
        = true;


    public ICollection<CuentaBancaria>
        Cuentas { get; set; }
            = new List<CuentaBancaria>();
}