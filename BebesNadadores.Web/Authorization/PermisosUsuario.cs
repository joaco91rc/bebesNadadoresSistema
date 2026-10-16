using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;

namespace BebesNadadores.Web.Authorization;

/*
 * =============================================================
 * PERMISOS DEL USUARIO ACTUAL (para usar en el código C#)
 * =============================================================
 *
 * Ocultar un botón con <AuthorizeView> no alcanza: el handler
 * sigue existiendo en el servidor. Las acciones sensibles validan
 * acá antes de ejecutar.
 *
 * Uso:
 *   @inject PermisosUsuario Permisos
 *
 *   _puedeGestionar = await Permisos.TieneAsync(PoliticasSistema.GestionAdministrativa);
 *   await Permisos.ExigirAsync(PoliticasSistema.GestionAdministrativa);
 */
public sealed class PermisosUsuario(
    AuthenticationStateProvider authenticationStateProvider,
    IAuthorizationService authorizationService)
{
    private readonly Dictionary<string, bool> _cache = [];


    public async Task<bool> TieneAsync(
        string politica)
    {
        if (_cache.TryGetValue(politica, out var valor))
        {
            return valor;
        }

        var estado =
            await authenticationStateProvider
                .GetAuthenticationStateAsync();

        var resultado =
            await authorizationService.AuthorizeAsync(
                estado.User,
                politica);

        _cache[politica] =
            resultado.Succeeded;

        return resultado.Succeeded;
    }


    public async Task ExigirAsync(
        string politica)
    {
        if (!await TieneAsync(politica))
        {
            throw new InvalidOperationException(
                "No tiene permisos para realizar esta acción.");
        }
    }
}
