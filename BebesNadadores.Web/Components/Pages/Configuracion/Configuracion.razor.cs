using System.Globalization;
using System.Security.Claims;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Configuracion.Dtos;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace BebesNadadores.Web.Components.Pages.Configuracion;

public partial class Configuracion : ComponentBase
{
    [Inject]
    protected IConfiguracionSistemaService ConfiguracionService { get; set; }
        = null!;

    [Inject]
    protected IUsuarioAdministracionService UsuarioService { get; set; }
        = null!;

    [Inject]
    protected IPlanTarifaService PlanTarifaService { get; set; }
        = null!;

    [Inject]
    protected AuthenticationStateProvider AuthenticationStateProvider { get; set; }
        = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; }
        = null!;


    protected bool _cargando = true;
    protected bool _guardando;
    protected string? _error;
    protected string? _mensaje;
    protected Guid _usuarioActualId;

    protected SeccionConfiguracion _seccion =
        SeccionConfiguracion.Resumen;

    protected ConfiguracionSistemaDto? _configuracion;

    protected List<UsuarioSistemaDto> _usuarios = [];
    protected List<string> _roles = [];
    protected List<ProfesorParaUsuarioDto> _profesores = [];
    protected List<PlanTarifaDto> _planes = [];

    protected GuardarInstitucionDto _institucionForm = new();
    protected GuardarReglasSistemaDto _reglasForm = new();

    protected bool _mostrarUsuarioModal;
    protected GuardarUsuarioSistemaDto _usuarioForm = new();
    protected string _confirmacionPassword = string.Empty;

    protected bool _mostrarPasswordModal;
    protected UsuarioSistemaDto? _usuarioPassword;
    protected string _nuevaPassword = string.Empty;
    protected string _confirmacionNuevaPassword = string.Empty;

    protected bool _mostrarPlanModal;
    protected GuardarPlanDto _planForm = new();

    protected bool _mostrarTarifaModal;
    protected GuardarTarifaPlanDto _tarifaForm = new();
    protected PlanTarifaDto? _planTarifaSeleccionado;
    protected bool _corrigiendoTarifa;


    protected bool UsuarioEsNuevo =>
        !_usuarioForm.Id.HasValue;

    protected bool EsRolProfesor =>
        string.Equals(
            _usuarioForm.Rol,
            RolesSistema.Profesor,
            StringComparison.OrdinalIgnoreCase);

    protected IEnumerable<ProfesorParaUsuarioDto>
        ProfesoresSeleccionables =>
            _profesores.Where(x =>
                !x.UsuarioId.HasValue
                ||
                x.UsuarioId == _usuarioForm.Id);

    protected int UsuariosActivos =>
        _usuarios.Count(x => x.Activo);

    protected int PlanesActivos =>
        _planes.Count(x => x.Activo);

    protected int PlanesSinTarifa =>
        _planes.Count(x =>
            x.Activo &&
            !x.PrecioActual.HasValue);


    protected override async Task OnInitializedAsync()
    {
        try
        {
            var auth =
                await AuthenticationStateProvider
                    .GetAuthenticationStateAsync();

            var idTexto =
                auth.User
                    .FindFirst(
                        ClaimTypes.NameIdentifier)
                    ?.Value;

            if (!Guid.TryParse(
                idTexto,
                out _usuarioActualId))
            {
                throw new InvalidOperationException(
                    "No se pudo identificar al usuario administrador.");
            }

            await CargarTodoAsync();
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _cargando = false;
        }
    }


    protected async Task CargarTodoAsync()
    {
        _error = null;

        _configuracion =
            await ConfiguracionService.ObtenerAsync();

        CopiarConfiguracionAFormularios();

        /*
         * Identity usa un contexto scoped interno.
         * Cargamos usuarios/roles en forma secuencial para no
         * ejecutar operaciones concurrentes sobre UserManager.
         */
        _usuarios =
            (await UsuarioService.ListarAsync())
                .ToList();

        _roles =
            (await UsuarioService.ListarRolesAsync())
                .ToList();

        _profesores =
            (await UsuarioService.ListarProfesoresAsync())
                .ToList();

        _planes =
            (await PlanTarifaService.ListarAsync())
                .ToList();
    }


    private void CopiarConfiguracionAFormularios()
    {
        if (_configuracion is null)
        {
            return;
        }

        _institucionForm =
            new GuardarInstitucionDto
            {
                NombreInstitucion =
                    _configuracion.NombreInstitucion,
                WhatsApp =
                    _configuracion.WhatsApp,
                Direccion =
                    _configuracion.Direccion,
                LogoUrl =
                    _configuracion.LogoUrl
            };

        _reglasForm =
            new GuardarReglasSistemaDto
            {
                DiasVigenciaRecupero =
                    _configuracion.DiasVigenciaRecupero,
                MesesVigenciaApto =
                    _configuracion.MesesVigenciaApto,
                DiasAlertaApto =
                    _configuracion.DiasAlertaApto,
                DiaVencimientoCuota =
                    _configuracion.DiaVencimientoCuota,
                EdadMaximaMeses =
                    _configuracion.EdadMaximaMeses
            };
    }


    protected void SeleccionarSeccion(
        SeccionConfiguracion seccion)
    {
        _seccion = seccion;
        _error = null;
        _mensaje = null;
    }


    protected async Task ActualizarAsync()
    {
        if (_cargando)
        {
            return;
        }

        _cargando = true;
        _mensaje = null;
        _error = null;

        try
        {
            await CargarTodoAsync();
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _cargando = false;
        }
    }


    // =========================================================
    // USUARIOS
    // =========================================================

    protected void NuevoUsuario()
    {
        _usuarioForm =
            new GuardarUsuarioSistemaDto
            {
                Activo = true,
                Rol =
                    _roles.Contains(
                        RolesSistema.Administrativa)
                        ? RolesSistema.Administrativa
                        : _roles.FirstOrDefault()
                            ?? string.Empty
            };

        _confirmacionPassword =
            string.Empty;

        _mostrarUsuarioModal =
            true;
    }


    protected void EditarUsuario(
        UsuarioSistemaDto usuario)
    {
        _usuarioForm =
            new GuardarUsuarioSistemaDto
            {
                Id = usuario.Id,
                NombreCompleto =
                    usuario.NombreCompleto,
                Email = usuario.Email,
                Rol = usuario.Rol,
                Activo = usuario.Activo,
                ProfesorId =
                    usuario.ProfesorId
            };

        _confirmacionPassword =
            string.Empty;

        _mostrarUsuarioModal =
            true;
    }


    protected void RolUsuarioCambio()
    {
        if (!EsRolProfesor)
        {
            _usuarioForm.ProfesorId = null;
        }
    }


    protected async Task GuardarUsuarioAsync()
    {
        if (_guardando)
        {
            return;
        }

        if (
            UsuarioEsNuevo
            &&
            !string.Equals(
                _usuarioForm.Password,
                _confirmacionPassword,
                StringComparison.Ordinal))
        {
            _error =
                "La contraseña y su confirmación no coinciden.";
            return;
        }

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await UsuarioService.GuardarAsync(
                _usuarioForm,
                _usuarioActualId);

            _mostrarUsuarioModal = false;

            _usuarios =
                (await UsuarioService.ListarAsync())
                    .ToList();

            _profesores =
                (await UsuarioService.ListarProfesoresAsync())
                    .ToList();

            _mensaje =
                UsuarioEsNuevo
                    ? "Usuario creado correctamente."
                    : "Usuario actualizado correctamente.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    protected void AbrirPassword(
        UsuarioSistemaDto usuario)
    {
        _usuarioPassword = usuario;
        _nuevaPassword = string.Empty;
        _confirmacionNuevaPassword = string.Empty;
        _mostrarPasswordModal = true;
    }


    protected async Task GuardarPasswordAsync()
    {
        if (_guardando ||
            _usuarioPassword is null)
        {
            return;
        }

        if (!string.Equals(
            _nuevaPassword,
            _confirmacionNuevaPassword,
            StringComparison.Ordinal))
        {
            _error =
                "La nueva contraseña y su confirmación no coinciden.";
            return;
        }

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await UsuarioService
                .ResetearPasswordAsync(
                    _usuarioPassword.Id,
                    _nuevaPassword);

            _mostrarPasswordModal = false;
            _mensaje =
                $"Contraseña actualizada para {_usuarioPassword.NombreCompleto}.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    // =========================================================
    // PLANES / TARIFAS
    // =========================================================

    protected void NuevoPlan()
    {
        _planForm =
            new GuardarPlanDto
            {
                VecesPorSemana = 1,
                Activo = true
            };

        _mostrarPlanModal = true;
    }


    protected void EditarPlan(
        PlanTarifaDto plan)
    {
        _planForm =
            new GuardarPlanDto
            {
                Id = plan.Id,
                Nombre = plan.Nombre,
                Descripcion =
                    plan.Descripcion,
                VecesPorSemana =
                    plan.VecesPorSemana,
                Activo = plan.Activo
            };

        _mostrarPlanModal = true;
    }


    protected async Task GuardarPlanAsync()
    {
        if (_guardando)
        {
            return;
        }

        var esNuevo =
            !_planForm.Id.HasValue;

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await PlanTarifaService
                .GuardarPlanAsync(
                    _planForm,
                    _usuarioActualId);

            _mostrarPlanModal = false;

            await RecargarPlanesAsync();

            _mensaje =
                esNuevo
                    ? "Plan creado correctamente."
                    : "Plan actualizado correctamente.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    protected async Task CambiarEstadoPlanAsync(
        PlanTarifaDto plan)
    {
        if (_guardando)
        {
            return;
        }

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await PlanTarifaService
                .CambiarEstadoPlanAsync(
                    plan.Id,
                    !plan.Activo,
                    _usuarioActualId);

            await RecargarPlanesAsync();

            _mensaje =
                plan.Activo
                    ? "Plan desactivado. Las inscripciones históricas se conservan."
                    : "Plan activado nuevamente.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    protected void NuevaTarifa(
        PlanTarifaDto plan)
    {
        _planTarifaSeleccionado = plan;
        _corrigiendoTarifa = false;

        var hoy =
            ObtenerHoyArgentina();

        var primerDiaMes =
            new DateOnly(
                hoy.Year,
                hoy.Month,
                1);

        /*
         * Si el plan ya tiene precio vigente, proponemos
         * el mes próximo para evitar cambiar accidentalmente
         * la cuota del mes en curso.
         */
        if (plan.PrecioActual.HasValue)
        {
            primerDiaMes =
                primerDiaMes.AddMonths(1);
        }

        _tarifaForm =
            new GuardarTarifaPlanDto
            {
                PlanId = plan.Id,
                VigenciaDesde =
                    primerDiaMes
            };

        _mostrarTarifaModal = true;
    }


    protected void CorregirTarifa(
        PlanTarifaDto plan,
        TarifaPlanDto tarifa)
    {
        _planTarifaSeleccionado = plan;
        _corrigiendoTarifa = true;

        _tarifaForm =
            new GuardarTarifaPlanDto
            {
                PlanId = plan.Id,
                PrecioMensual =
                    tarifa.PrecioMensual,
                VigenciaDesde =
                    tarifa.VigenciaDesde
            };

        _mostrarTarifaModal = true;
    }


    protected Task CambiarVigenciaTarifaAsync(
        DateOnly fecha)
    {
        _tarifaForm.VigenciaDesde =
            new DateOnly(
                fecha.Year,
                fecha.Month,
                1);

        return Task.CompletedTask;
    }


    protected async Task GuardarTarifaAsync()
    {
        if (_guardando)
        {
            return;
        }

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await PlanTarifaService
                .GuardarTarifaAsync(
                    _tarifaForm,
                    _usuarioActualId);

            _mostrarTarifaModal = false;

            await RecargarPlanesAsync();

            _mensaje =
                _corrigiendoTarifa
                    ? "Tarifa corregida correctamente."
                    : "Nueva tarifa registrada. El historial anterior se conserva.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    private async Task RecargarPlanesAsync()
    {
        _planes =
            (await PlanTarifaService.ListarAsync())
                .ToList();
    }


    // =========================================================
    // REGLAS
    // =========================================================

    protected async Task GuardarReglasAsync()
    {
        if (_guardando)
        {
            return;
        }

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await ConfiguracionService
                .GuardarReglasAsync(
                    _reglasForm,
                    _usuarioActualId);

            _configuracion =
                await ConfiguracionService.ObtenerAsync();

            CopiarConfiguracionAFormularios();

            _mensaje =
                "Reglas generales actualizadas.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    // =========================================================
    // INSTITUCIÓN
    // =========================================================

    protected async Task GuardarInstitucionAsync()
    {
        if (_guardando)
        {
            return;
        }

        _guardando = true;
        _error = null;
        _mensaje = null;

        try
        {
            await ConfiguracionService
                .GuardarInstitucionAsync(
                    _institucionForm,
                    _usuarioActualId);

            _configuracion =
                await ConfiguracionService.ObtenerAsync();

            CopiarConfiguracionAFormularios();

            _mensaje =
                "Datos de la institución actualizados.";
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _guardando = false;
        }
    }


    // =========================================================
    // NAVEGACIÓN OPERATIVA
    // =========================================================

    protected void IrATurnera() =>
        Navigation.NavigateTo("/turnos");

    protected void IrANiveles() =>
        Navigation.NavigateTo("/niveles");

    protected void IrAProfesores() =>
        Navigation.NavigateTo("/profesores");

    protected void IrABancos() =>
        Navigation.NavigateTo("/bancos");

    protected void IrACaja() =>
        Navigation.NavigateTo("/caja");


    // =========================================================
    // HELPERS
    // =========================================================

    protected string FormatearPrecio(
        decimal precio)
    {
        return precio.ToString(
            "C0",
            CultureInfo.GetCultureInfo(
                "es-AR"));
    }


    protected string FormatearFecha(
        DateOnly? fecha)
    {
        return fecha.HasValue
            ? fecha.Value.ToString(
                "dd/MM/yyyy",
                CultureInfo.GetCultureInfo(
                    "es-AR"))
            : "Sin límite";
    }


    protected string ObtenerNombreRol(
        string rol)
    {
        return rol switch
        {
            RolesSistema.Administrador =>
                "Administrador",

            RolesSistema.Administrativa =>
                "Administrativa",

            RolesSistema.Medica =>
                "Médica",

            RolesSistema.Profesor =>
                "Profesor",

            RolesSistema.SoloLectura =>
                "Solo lectura",

            _ => rol
        };
    }


    protected string ObtenerIniciales(
        string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            return "US";
        }

        var partes =
            nombre.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length == 1)
        {
            return partes[0][..1]
                .ToUpperInvariant();
        }

        return $"{partes[0][0]}{partes[^1][0]}"
            .ToUpperInvariant();
    }


    protected string ObtenerEdadMaximaTexto()
    {
        var meses =
            _reglasForm.EdadMaximaMeses;

        if (meses % 12 == 0)
        {
            var anios = meses / 12;
            return anios == 1
                ? "1 año"
                : $"{anios} años";
        }

        return $"{meses} meses";
    }


    private static DateOnly ObtenerHoyArgentina()
    {
        TimeZoneInfo zona;

        try
        {
            zona =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            zona =
                TimeZoneInfo.FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }

        var ahora =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                zona);

        return DateOnly.FromDateTime(
            ahora.DateTime);
    }


    protected enum SeccionConfiguracion
    {
        Resumen = 0,
        Usuarios = 1,
        Planes = 2,
        Reglas = 3,
        Institucion = 4
    }
}
