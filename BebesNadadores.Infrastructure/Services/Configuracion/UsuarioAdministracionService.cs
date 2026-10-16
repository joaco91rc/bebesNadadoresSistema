using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Configuracion.Dtos;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BebesNadadores.Infrastructure.Services.Configuracion;

public sealed class UsuarioAdministracionService(
    IServiceScopeFactory scopeFactory,
    IDbContextFactory<AppDbContext> factory)
    : IUsuarioAdministracionService
{
    public async Task<IReadOnlyList<UsuarioSistemaDto>> ListarAsync(
        CancellationToken ct = default)
    {
        using var scope =
            scopeFactory.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();

        var usuarios =
            await userManager.Users
                .AsNoTracking()
                .OrderBy(x => x.NombreCompleto)
                .ThenBy(x => x.Email)
                .ToListAsync(ct);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var profesores =
            await db.Profesores
                .AsNoTracking()
                .Where(x => x.UsuarioId.HasValue)
                .Select(x => new
                {
                    x.Id,
                    x.UsuarioId,
                    Nombre =
                        (x.Nombre + " " + x.Apellido)
                            .Trim()
                })
                .ToListAsync(ct);

        var profesorPorUsuario =
            profesores
                .Where(x => x.UsuarioId.HasValue)
                .ToDictionary(
                    x => x.UsuarioId!.Value,
                    x => x);

        var resultado =
            new List<UsuarioSistemaDto>();

        /*
         * Los roles se consultan secuencialmente sobre el mismo
         * UserManager/contexto. En Blazor Server esto evita iniciar
         * operaciones simultáneas sobre un DbContext de Identity.
         */
        foreach (var usuario in usuarios)
        {
            ct.ThrowIfCancellationRequested();

            var roles =
                await userManager
                    .GetRolesAsync(usuario);

            var rol =
                roles.FirstOrDefault()
                ?? string.Empty;

            profesorPorUsuario.TryGetValue(
                usuario.Id,
                out var profesor);

            resultado.Add(
                new UsuarioSistemaDto(
                    usuario.Id,
                    usuario.NombreCompleto,
                    usuario.Email
                        ?? usuario.UserName
                        ?? string.Empty,
                    rol,
                    usuario.Activo,
                    profesor?.Id,
                    profesor?.Nombre));
        }

        return resultado;
    }


    public async Task<IReadOnlyList<string>> ListarRolesAsync(
        CancellationToken ct = default)
    {
        using var scope =
            scopeFactory.CreateScope();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        return await roleManager.Roles
            .AsNoTracking()
            .Where(x => x.Name != null)
            .OrderBy(x => x.Name)
            .Select(x => x.Name!)
            .ToListAsync(ct);
    }


    public async Task<IReadOnlyList<ProfesorParaUsuarioDto>>
        ListarProfesoresAsync(
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        return await db.Profesores
            .AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Apellido)
            .ThenBy(x => x.Nombre)
            .Select(x =>
                new ProfesorParaUsuarioDto(
                    x.Id,
                    (x.Nombre + " " + x.Apellido)
                        .Trim(),
                    x.UsuarioId))
            .ToListAsync(ct);
    }


    public async Task<Guid> GuardarAsync(
        GuardarUsuarioSistemaDto dto,
        Guid usuarioActualId,
        CancellationToken ct = default)
    {
        ValidarDatosBasicos(dto);

        using var scope =
            scopeFactory.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();

        var roleManager =
            scope.ServiceProvider
                .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        var rolExiste =
            await roleManager
                .RoleExistsAsync(dto.Rol);

        if (!rolExiste)
        {
            throw new InvalidOperationException(
                "El rol seleccionado no existe.");
        }

        if (string.Equals(
                dto.Rol,
                RolesSistema.Profesor,
                StringComparison.OrdinalIgnoreCase)
            &&
            !dto.ProfesorId.HasValue)
        {
            throw new InvalidOperationException(
                "Para un usuario Profesor debe seleccionar " +
                "el profesor al que se vinculará la cuenta.");
        }

        if (!dto.Id.HasValue)
        {
            return await CrearAsync(
                userManager,
                dto,
                ct);
        }

        return await ActualizarAsync(
            userManager,
            dto,
            usuarioActualId,
            ct);
    }


    public async Task ResetearPasswordAsync(
        Guid usuarioId,
        string nuevaPassword,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(nuevaPassword))
        {
            throw new InvalidOperationException(
                "Debe indicar la nueva contraseña.");
        }

        using var scope =
            scopeFactory.CreateScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();

        var usuario =
            await userManager
                .FindByIdAsync(
                    usuarioId.ToString())
            ?? throw new InvalidOperationException(
                "El usuario indicado no existe.");

        var token =
            await userManager
                .GeneratePasswordResetTokenAsync(
                    usuario);

        var resultado =
            await userManager.ResetPasswordAsync(
                usuario,
                token,
                nuevaPassword);

        ValidarResultadoIdentity(
            resultado,
            "No se pudo cambiar la contraseña");
    }


    private async Task<Guid> CrearAsync(
        UserManager<AppUser> userManager,
        GuardarUsuarioSistemaDto dto,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(dto.Password))
        {
            throw new InvalidOperationException(
                "Debe indicar una contraseña para el nuevo usuario.");
        }

        var email =
            dto.Email.Trim();

        var existente =
            await userManager
                .FindByEmailAsync(email);

        if (existente is not null)
        {
            throw new InvalidOperationException(
                "Ya existe un usuario con ese email.");
        }

        if (string.Equals(
                dto.Rol,
                RolesSistema.Profesor,
                StringComparison.OrdinalIgnoreCase))
        {
            await ValidarProfesorDisponibleAsync(
                dto.ProfesorId!.Value,
                usuarioIdPermitido: null,
                ct);
        }

        var usuario =
            new AppUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                NombreCompleto =
                    dto.NombreCompleto.Trim(),
                Activo = dto.Activo
            };

        var crear =
            await userManager.CreateAsync(
                usuario,
                dto.Password);

        ValidarResultadoIdentity(
            crear,
            "No se pudo crear el usuario");

        var agregarRol =
            await userManager.AddToRoleAsync(
                usuario,
                dto.Rol);

        if (!agregarRol.Succeeded)
        {
            await userManager.DeleteAsync(usuario);

            ValidarResultadoIdentity(
                agregarRol,
                "No se pudo asignar el rol");
        }

        try
        {
            await ActualizarVinculoProfesorAsync(
                usuario.Id,
                dto.Rol,
                dto.ProfesorId,
                ct);
        }
        catch
        {
            /*
             * Si la vinculación del profesor falla, no dejamos
             * una cuenta incompleta creada en Identity.
             */
            await userManager.DeleteAsync(usuario);
            throw;
        }

        return usuario.Id;
    }


    private async Task<Guid> ActualizarAsync(
        UserManager<AppUser> userManager,
        GuardarUsuarioSistemaDto dto,
        Guid usuarioActualId,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var usuario =
            await userManager
                .FindByIdAsync(
                    dto.Id!.Value.ToString())
            ?? throw new InvalidOperationException(
                "El usuario indicado no existe.");

        if (usuario.Id == usuarioActualId &&
            !dto.Activo)
        {
            throw new InvalidOperationException(
                "No puede desactivar su propio usuario.");
        }

        var rolesActuales =
            await userManager
                .GetRolesAsync(usuario);

        /*
         * Para cortar la sesión si se desactiva o cambia de rol
         * (ver "INVALIDAR SESIONES" más abajo).
         */
        var estabaActivo =
            usuario.Activo;

        var cambiaRol =
            rolesActuales.Count != 1
            ||
            !string.Equals(
                rolesActuales[0],
                dto.Rol,
                StringComparison.OrdinalIgnoreCase);

        var eraAdministrador =
            rolesActuales.Contains(
                RolesSistema.Administrador,
                StringComparer.OrdinalIgnoreCase);

        if (usuario.Id == usuarioActualId &&
            eraAdministrador &&
            !string.Equals(
                dto.Rol,
                RolesSistema.Administrador,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "No puede quitarse a sí mismo el rol Administrador.");
        }

        if (
            eraAdministrador
            &&
            (
                !dto.Activo
                ||
                !string.Equals(
                    dto.Rol,
                    RolesSistema.Administrador,
                    StringComparison.OrdinalIgnoreCase)
            ))
        {
            await ValidarQueQuedeOtroAdministradorAsync(
                userManager,
                usuario.Id);
        }

        var email =
            dto.Email.Trim();

        var usuarioMismoEmail =
            await userManager
                .FindByEmailAsync(email);

        if (
            usuarioMismoEmail is not null
            &&
            usuarioMismoEmail.Id != usuario.Id)
        {
            throw new InvalidOperationException(
                "Ya existe otro usuario con ese email.");
        }

        if (string.Equals(
                dto.Rol,
                RolesSistema.Profesor,
                StringComparison.OrdinalIgnoreCase))
        {
            await ValidarProfesorDisponibleAsync(
                dto.ProfesorId!.Value,
                usuario.Id,
                ct);
        }

        if (!string.Equals(
            usuario.Email,
            email,
            StringComparison.OrdinalIgnoreCase))
        {
            var emailResult =
                await userManager.SetEmailAsync(
                    usuario,
                    email);

            ValidarResultadoIdentity(
                emailResult,
                "No se pudo cambiar el email");
        }

        if (!string.Equals(
            usuario.UserName,
            email,
            StringComparison.OrdinalIgnoreCase))
        {
            var userNameResult =
                await userManager.SetUserNameAsync(
                    usuario,
                    email);

            ValidarResultadoIdentity(
                userNameResult,
                "No se pudo cambiar el nombre de usuario");
        }

        usuario.NombreCompleto =
            dto.NombreCompleto.Trim();

        usuario.Activo =
            dto.Activo;

        var actualizar =
            await userManager.UpdateAsync(usuario);

        ValidarResultadoIdentity(
            actualizar,
            "No se pudo actualizar el usuario");

        var rolesAQuitar =
            rolesActuales
                .Where(x =>
                    !string.Equals(
                        x,
                        dto.Rol,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (rolesAQuitar.Count > 0)
        {
            var quitar =
                await userManager.RemoveFromRolesAsync(
                    usuario,
                    rolesAQuitar);

            ValidarResultadoIdentity(
                quitar,
                "No se pudieron actualizar los roles");
        }

        if (!await userManager.IsInRoleAsync(
            usuario,
            dto.Rol))
        {
            var agregar =
                await userManager.AddToRoleAsync(
                    usuario,
                    dto.Rol);

            ValidarResultadoIdentity(
                agregar,
                "No se pudo asignar el rol");
        }

        await ActualizarVinculoProfesorAsync(
            usuario.Id,
            dto.Rol,
            dto.ProfesorId,
            ct);

        // =========================================================
        // INVALIDAR SESIONES
        //
        // Cambiar el security stamp invalida la cookie (se valida
        // cada 1 minuto) y el circuito de Blazor que esté abierto
        // (RevalidacionUsuarioAuthenticationStateProvider).
        // Así un usuario desactivado o con otro rol no sigue
        // operando con la sesión vieja.
        // =========================================================

        if ((estabaActivo && !usuario.Activo) || cambiaRol)
        {
            var stamp =
                await userManager
                    .UpdateSecurityStampAsync(usuario);

            ValidarResultadoIdentity(
                stamp,
                "No se pudo cerrar la sesión del usuario");
        }

        return usuario.Id;
    }


    private static async Task ValidarQueQuedeOtroAdministradorAsync(
        UserManager<AppUser> userManager,
        Guid usuarioExcluidoId)
    {
        var administradores =
            await userManager
                .GetUsersInRoleAsync(
                    RolesSistema.Administrador);

        var quedaOtro =
            administradores.Any(x =>
                x.Id != usuarioExcluidoId
                &&
                x.Activo);

        if (!quedaOtro)
        {
            throw new InvalidOperationException(
                "Debe existir al menos un Administrador activo en el sistema.");
        }
    }


    private async Task ValidarProfesorDisponibleAsync(
        Guid profesorId,
        Guid? usuarioIdPermitido,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var profesor =
            await db.Profesores
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == profesorId,
                    ct)
            ?? throw new InvalidOperationException(
                "El profesor seleccionado no existe.");

        if (!profesor.Activo)
        {
            throw new InvalidOperationException(
                "El profesor seleccionado está inactivo.");
        }

        if (
            profesor.UsuarioId.HasValue
            &&
            profesor.UsuarioId != usuarioIdPermitido)
        {
            throw new InvalidOperationException(
                "El profesor seleccionado ya tiene un usuario vinculado.");
        }
    }


    private async Task ActualizarVinculoProfesorAsync(
        Guid usuarioId,
        string rol,
        Guid? profesorId,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var vinculadosActualmente =
            await db.Profesores
                .Where(x =>
                    x.UsuarioId == usuarioId)
                .ToListAsync(ct);

        foreach (var profesor in
            vinculadosActualmente)
        {
            profesor.UsuarioId = null;
            profesor.ModificadoUtc =
                DateTimeOffset.UtcNow;
            profesor.ModificadoPorUsuarioId =
                usuarioId;
        }

        if (
            string.Equals(
                rol,
                RolesSistema.Profesor,
                StringComparison.OrdinalIgnoreCase)
            &&
            profesorId.HasValue)
        {
            var profesor =
                await db.Profesores
                    .FirstOrDefaultAsync(
                        x => x.Id == profesorId.Value,
                        ct)
                ?? throw new InvalidOperationException(
                    "El profesor seleccionado no existe.");

            if (
                profesor.UsuarioId.HasValue
                &&
                profesor.UsuarioId != usuarioId)
            {
                throw new InvalidOperationException(
                    "El profesor seleccionado ya tiene un usuario vinculado.");
            }

            profesor.UsuarioId =
                usuarioId;
            profesor.ModificadoUtc =
                DateTimeOffset.UtcNow;
            profesor.ModificadoPorUsuarioId =
                usuarioId;
        }

        await db.SaveChangesAsync(ct);
    }


    private static void ValidarDatosBasicos(
        GuardarUsuarioSistemaDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.NombreCompleto))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre completo.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Email))
        {
            throw new InvalidOperationException(
                "Debe indicar el email del usuario.");
        }

        if (string.IsNullOrWhiteSpace(
            dto.Rol))
        {
            throw new InvalidOperationException(
                "Debe seleccionar un rol.");
        }
    }


    private static void ValidarResultadoIdentity(
        IdentityResult resultado,
        string prefijo)
    {
        if (resultado.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            prefijo + ": " +
            string.Join(
                ", ",
                resultado.Errors.Select(
                    x => x.Description)));
    }
}
