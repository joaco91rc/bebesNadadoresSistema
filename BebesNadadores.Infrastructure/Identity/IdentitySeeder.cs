using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BebesNadadores.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var roleManager =
            services.GetRequiredService<
                RoleManager<IdentityRole<Guid>>>();

        var userManager =
            services.GetRequiredService<
                UserManager<AppUser>>();


        // =========================================================
        // ROLES
        // =========================================================

        foreach (var nombreRol in RolesSistema.Todos)
        {
            if (await roleManager.RoleExistsAsync(nombreRol))
            {
                continue;
            }

            var result =
                await roleManager.CreateAsync(
                    new IdentityRole<Guid>
                    {
                        Id =
                            Guid.NewGuid(),

                        Name =
                            nombreRol,

                        NormalizedName =
                            nombreRol.ToUpperInvariant()
                    });

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el rol {nombreRol}: " +
                    string.Join(
                        ", ",
                        result.Errors.Select(
                            x => x.Description)));
            }
        }


        // =========================================================
        // ADMINISTRADOR
        // =========================================================

        var adminEmail =
            configuration["SeedAdmin:Email"];

        var adminPassword =
            configuration["SeedAdmin:Password"];

        var adminNombre =
            configuration["SeedAdmin:NombreCompleto"]
            ?? "Administrador";


        if (!string.IsNullOrWhiteSpace(adminEmail) &&
            !string.IsNullOrWhiteSpace(adminPassword))
        {
            await CrearUsuarioAsync(
                userManager,
                adminEmail,
                adminPassword,
                adminNombre,
                RolesSistema.Administrador);
        }


        var medicaEmail =
    configuration["SeedMedica:Email"];

var medicaPassword =
    configuration["SeedMedica:Password"];

var medicaNombreCompleto =
    configuration["SeedMedica:NombreCompleto"]
    ?? "Médica Bebés Nadadores";

if (!string.IsNullOrWhiteSpace(medicaEmail) &&
    !string.IsNullOrWhiteSpace(medicaPassword))
{
    var medica =
        await userManager.FindByEmailAsync(
            medicaEmail);

    if (medica is null)
    {
        medica =
            new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = medicaEmail,
                Email = medicaEmail,
                EmailConfirmed = true,
                NombreCompleto = medicaNombreCompleto,
                Activo = true
            };

        var create =
            await userManager.CreateAsync(
                medica,
                medicaPassword);

        if (!create.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo crear el usuario médica: " +
                string.Join(
                    ", ",
                    create.Errors.Select(
                        x => x.Description)));
        }
    }

    if (!await userManager.IsInRoleAsync(
        medica,
        RolesSistema.Medica))
    {
        var addRole =
            await userManager.AddToRoleAsync(
                medica,
                RolesSistema.Medica);

        if (!addRole.Succeeded)
        {
            throw new InvalidOperationException(
                "No se pudo asignar el rol Médica: " +
                string.Join(
                    ", ",
                    addRole.Errors.Select(
                        x => x.Description)));
        }
    }
}
    }


    // =============================================================
    // CREAR USUARIO + ASIGNAR ROL
    // =============================================================

    private static async Task CrearUsuarioAsync(
        UserManager<AppUser> userManager,
        string email,
        string password,
        string nombreCompleto,
        string rol)
    {
        var usuario =
            await userManager.FindByEmailAsync(
                email);


        if (usuario is null)
        {
            usuario =
                new AppUser
                {
                    Id =
                        Guid.NewGuid(),

                    UserName =
                        email,

                    Email =
                        email,

                    EmailConfirmed =
                        true,

                    NombreCompleto =
                        nombreCompleto,

                    Activo =
                        true
                };


            var create =
                await userManager.CreateAsync(
                    usuario,
                    password);


            if (!create.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo crear el usuario {email}: " +
                    string.Join(
                        ", ",
                        create.Errors.Select(
                            x => x.Description)));
            }
        }


        // Si existe pero estaba desactivado,
        // lo reactivamos para poder usarlo.
        if (!usuario.Activo)
        {
            usuario.Activo =
                true;

            var update =
                await userManager.UpdateAsync(
                    usuario);

            if (!update.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo activar el usuario {email}: " +
                    string.Join(
                        ", ",
                        update.Errors.Select(
                            x => x.Description)));
            }
        }


        // =========================================================
        // ASIGNAR ROL
        // =========================================================

        if (!await userManager.IsInRoleAsync(
            usuario,
            rol))
        {
            var addRole =
                await userManager.AddToRoleAsync(
                    usuario,
                    rol);


            if (!addRole.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo asignar el rol {rol} " +
                    $"al usuario {email}: " +
                    string.Join(
                        ", ",
                        addRole.Errors.Select(
                            x => x.Description)));
            }
        }
    }
}