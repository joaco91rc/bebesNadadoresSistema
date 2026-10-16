using System.Security.Claims;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BebesNadadores.Web.Authorization;

/*
 * =============================================================
 * REVALIDACIÓN DEL USUARIO EN EL CIRCUITO DE BLAZOR
 * =============================================================
 *
 * En Blazor Server la cookie solo se valida en los requests HTTP.
 * Una pestaña abierta (circuito / websocket) puede vivir horas sin
 * volver a pasar por la cookie.
 *
 * Este provider revisa cada minuto que el usuario:
 *   - siga existiendo,
 *   - siga Activo,
 *   - tenga el mismo security stamp con el que inició sesión
 *     (cambia al desactivarlo, cambiarle el rol o la contraseña).
 *
 * Si algo de eso falla, el estado pasa a "no autenticado" y el
 * AuthorizeRouteView lo manda al login.
 */
public sealed class RevalidacionUsuarioAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory,
    IOptions<IdentityOptions> identityOptions)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval =>
        TimeSpan.FromMinutes(1);


    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        await using var scope =
            scopeFactory.CreateAsyncScope();

        var userManager =
            scope.ServiceProvider
                .GetRequiredService<UserManager<AppUser>>();

        return await UsuarioSigueHabilitadoAsync(
            userManager,
            authenticationState.User,
            identityOptions.Value);
    }


    /*
     * Público y estático para poder probarlo sin levantar un circuito.
     */
    public static async Task<bool> UsuarioSigueHabilitadoAsync(
        UserManager<AppUser> userManager,
        ClaimsPrincipal principal,
        IdentityOptions opciones)
    {
        var usuario =
            await userManager.GetUserAsync(principal);

        if (usuario is null || !usuario.Activo)
        {
            return false;
        }

        if (!userManager.SupportsUserSecurityStamp)
        {
            return true;
        }

        var stampSesion =
            principal.FindFirstValue(
                opciones.ClaimsIdentity.SecurityStampClaimType);

        var stampActual =
            await userManager.GetSecurityStampAsync(usuario);

        return stampSesion == stampActual;
    }
}
