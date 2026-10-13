namespace BebesNadadores.Application.Proveedores.Dtos;

public sealed class GuardarProveedorDto
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
}