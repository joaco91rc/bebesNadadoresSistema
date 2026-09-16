using Microsoft.AspNetCore.Identity;

namespace BebesNadadores.Infrastructure.Identity;

public class AppUser : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
