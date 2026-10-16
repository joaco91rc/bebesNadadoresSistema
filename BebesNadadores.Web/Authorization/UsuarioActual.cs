using System.Security.Claims;
using BebesNadadores.Application.Abstractions;
using Microsoft.AspNetCore.Components.Authorization;

namespace BebesNadadores.Web.Authorization;

/*
 * Usuario logueado, para auditoría y logs.
 *
 * - Dentro de Blazor (circuito) se lee del AuthenticationStateProvider.
 * - En un request HTTP común (login, descarga de reportes) ese
 *   provider no está inicializado: se usa el HttpContext.
 * - En un job automático no hay ninguno: devuelve null.
 */
public sealed class UsuarioActual(
    AuthenticationStateProvider authenticationStateProvider,
    IHttpContextAccessor httpContextAccessor)
    : IUsuarioActual
{
    public async Task<UsuarioActualDto?> ObtenerAsync(
        CancellationToken ct = default)
    {
        ClaimsPrincipal? usuario = null;

        try
        {
            var estado =
                await authenticationStateProvider
                    .GetAuthenticationStateAsync();

            usuario = estado.User;
        }
        catch (InvalidOperationException)
        {
            // Fuera de un circuito de Blazor.
        }

        if (usuario?.Identity?.IsAuthenticated != true)
        {
            usuario = httpContextAccessor.HttpContext?.User;
        }

        if (usuario?.Identity?.IsAuthenticated != true)
            return null;

        if (!Guid.TryParse(
            usuario.FindFirstValue(ClaimTypes.NameIdentifier),
            out var id))
        {
            return null;
        }

        return new UsuarioActualDto(
            id,
            usuario.Identity.Name
                ?? usuario.FindFirstValue(ClaimTypes.Email)
                ?? id.ToString());
    }
}
