using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class Proveedor : EntidadAuditable
{
    public string RazonSocial { get; set; } =
        string.Empty;

    public string? NombreFantasia { get; set; }

    public string? Cuit { get; set; }

    public string? Telefono { get; set; }

    public string? Email { get; set; }

    public string? Direccion { get; set; }

    public string? Observaciones { get; set; }

    public bool Activo { get; set; } =
        true;


    public ICollection<PagoProveedor> Pagos { get; set; } =
        new List<PagoProveedor>();
}