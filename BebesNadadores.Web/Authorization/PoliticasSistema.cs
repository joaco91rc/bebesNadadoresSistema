using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;

namespace BebesNadadores.Web.Authorization;

/*
 * =============================================================
 * POLÍTICAS DEL SISTEMA
 * =============================================================
 *
 * Único lugar donde se define qué rol puede hacer qué.
 * Las páginas usan:
 *
 *   @attribute [Authorize(Policy = PoliticasSistema.X)]
 *
 * y los botones / tarjetas:
 *
 *   <AuthorizeView Policy="@PoliticasSistema.X"> ... </AuthorizeView>
 *
 * No usar strings de roles sueltos en las páginas: si cambia una
 * regla, se cambia acá y queda alineado en todo el sistema.
 *
 * Uso real del sistema:
 *   - Administrativa: opera todo el día a día.
 *   - Médica: SOLO aptos médicos.
 *   - Administrador: todo (configuración, usuarios, generar cuotas).
 *
 * Matriz:
 *
 *   Política               Admin  Administrativa  Médica  SoloLectura
 *   ---------------------  -----  --------------  ------  -----------
 *   SoloAdministrador        ✔
 *   GestionAdministrativa    ✔          ✔
 *   GestionCaja              ✔          ✔
 *   GestionMedica            ✔                      ✔
 *   ConsultaGeneral          ✔          ✔                      ✔
 *
 * El rol Profesor existe en Identity pero no se usa: un usuario con
 * ese rol no tiene acceso a ninguna pantalla operativa.
 */
public static class PoliticasSistema
{
    /*
     * Configuración, usuarios, generación manual de cuotas.
     */
    public const string SoloAdministrador = "SoloAdministrador";

    /*
     * Alta / edición de bebés, inscripciones, recuperos, lista de
     * espera, profesores, niveles, ciclos lectivos, turnera y
     * asistencia.
     */
    public const string GestionAdministrativa = "GestionAdministrativa";

    /*
     * Dinero: pagos, caja, bancos, proveedores.
     */
    public const string GestionCaja = "GestionCaja";

    /*
     * Aptos médicos.
     */
    public const string GestionMedica = "GestionMedica";

    /*
     * Consulta (sin modificar): listado y ficha de bebés, reportes.
     */
    public const string ConsultaGeneral = "ConsultaGeneral";


    public static void Configurar(AuthorizationOptions options)
    {
        options.AddPolicy(
            SoloAdministrador,
            policy => policy.RequireRole(
                RolesSistema.Administrador));

        options.AddPolicy(
            GestionAdministrativa,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Administrativa));

        options.AddPolicy(
            GestionCaja,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Administrativa));

        options.AddPolicy(
            GestionMedica,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Medica));

        options.AddPolicy(
            ConsultaGeneral,
            policy => policy.RequireRole(
                RolesSistema.Administrador,
                RolesSistema.Administrativa,
                RolesSistema.SoloLectura));
    }
}
