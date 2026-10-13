namespace BebesNadadores.Application.Proveedores.Dtos;

public sealed record ProveedorDto(
    Guid Id,
    string RazonSocial,
    string? NombreFantasia,
    string? Cuit,
    string? Telefono,
    string? Email,
    string? Direccion,
    string? Observaciones,
    bool Activo,
    decimal TotalPagado,
    DateTimeOffset? UltimoPagoUtc);