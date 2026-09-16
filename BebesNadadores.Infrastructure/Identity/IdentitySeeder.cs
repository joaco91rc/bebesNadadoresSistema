using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BebesNadadores.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<AppUser>>();

        foreach (var nombreRol in RolesSistema.Todos)
        {
            if (!await roleManager.RoleExistsAsync(nombreRol))
            {
                var result = await roleManager.CreateAsync(
                    new IdentityRole<Guid>
                    {
                        Id = Guid.NewGuid(),
                        Name = nombreRol,
                        NormalizedName = nombreRol.ToUpperInvariant()
                    });

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"No se pudo crear el rol {nombreRol}: " +
                        string.Join(", ", result.Errors.Select(x => x.Description)));
                }
            }
        }

        var email = configuration["SeedAdmin:Email"];
        var password = configuration["SeedAdmin:Password"];
        var nombreCompleto = configuration["SeedAdmin:NombreCompleto"] ?? "Administrador";

        // Si no configurás SeedAdmin, solamente se crean los roles.
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var admin = await userManager.FindByEmailAsync(email);

        if (admin is null)
        {
            admin = new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto = nombreCompleto,
                Activo = true
            };

            var create = await userManager.CreateAsync(admin, password);

            if (!create.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo crear el usuario administrador: " +
                    string.Join(", ", create.Errors.Select(x => x.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(admin, RolesSistema.Administrador))
        {
            var addRole = await userManager.AddToRoleAsync(admin, RolesSistema.Administrador);

            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException(
                    "No se pudo asignar el rol Administrador: " +
                    string.Join(", ", addRole.Errors.Select(x => x.Description)));
            }
        }
    }
}
