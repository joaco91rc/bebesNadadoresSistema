using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BebesNadadores.Web.Authorization;

public static class PoliticasSistema
{
    public const string GestionAdministrativa = "GestionAdministrativa";
    public const string GestionMedica = "GestionMedica";
    public const string GestionProfesores = "GestionProfesores";
    public const string GestionCaja = "GestionCaja";

    public static void Configurar(AuthorizationOptions options)
    {
        options.AddPolicy(
            GestionAdministrativa,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Administrativa));

        options.AddPolicy(
            GestionMedica,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Medica));

        options.AddPolicy(
            GestionProfesores,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Administrativa,
                RolesSistema.Profesor));

        options.AddPolicy(
            GestionCaja,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Administrativa));
    }
}
