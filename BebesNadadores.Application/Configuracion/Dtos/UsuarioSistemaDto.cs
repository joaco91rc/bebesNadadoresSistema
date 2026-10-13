namespace BebesNadadores.Application.Configuracion.Dtos;

public sealed record UsuarioSistemaDto(
    Guid Id,
    string NombreCompleto,
    string Email,
    string Rol,
    bool Activo,
    Guid? ProfesorId,
    string? ProfesorVinculado);

public sealed record ProfesorParaUsuarioDto(
    Guid Id,
    string NombreCompleto,
    Guid? UsuarioId);

public sealed class GuardarUsuarioSistemaDto
{
    public Guid? Id { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
    public string? Password { get; set; }
    public Guid? ProfesorId { get; set; }
}
