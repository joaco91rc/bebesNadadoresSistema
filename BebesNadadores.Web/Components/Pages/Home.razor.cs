using BebesNadadores.Application.Asistencia.Dtos;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Application.Dashboard.Dtos;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.JSInterop;

namespace BebesNadadores.Web.Components.Pages;

public partial class Home
{

    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    private void IrAClientes() =>
        Navigation.NavigateTo("/clientes");

    private void IrATurnos() =>
        Navigation.NavigateTo("/turnos");

    private void IrAListaEspera() =>
        Navigation.NavigateTo("/lista-espera");

    private void IrACaja() =>
        Navigation.NavigateTo("/caja");

    private void IrAPagos() =>
        Navigation.NavigateTo("/pagos");

    private void IrAAptosMedicos() =>
        Navigation.NavigateTo("/aptos-medicos");

    private void AbrirAsistencia(Guid sesionClaseId) =>
        Navigation.NavigateTo(
            $"/asistencia?sesionId={sesionClaseId}");


    private string _nombreCompleto = "Usuario";


    private DateTime _ahora = ObtenerAhoraArgentina();

    private Timer? _relojTimer;

    private AsistenciaSesionDto? _claseActual;
    private Guid? _usuarioId;

    private List<CumpleaniosBebeDto> _cumpleanios = [];
    private DashboardDto _dashboard = new();
    private int CantidadPresentes =>
        _claseActual?.Presentes
        ?? 0;


    private int CantidadAusentesConAviso =>
        _claseActual?.AusentesConAviso
        ?? 0;


    private int CantidadAusentes =>
        _claseActual?.Ausentes
        ?? 0;


    private int CantidadPendientes =>
        _claseActual?.Pendientes
        ?? 0;


    private int CantidadMarcados =>
        CantidadPresentes
        + CantidadAusentesConAviso
        + CantidadAusentes;


    private int PorcentajeMarcado =>
        _claseActual is null
        ||
        _claseActual.TotalEsperados == 0
            ? 0
            : (int)Math.Round(
                CantidadMarcados
                * 100d
                /
                _claseActual.TotalEsperados);

    private void AbrirAsistenciaCompleta()
    {
        if (_claseActual is null)
        {
            Navigation.NavigateTo(
                "/asistencia");

            return;
        }


        Navigation.NavigateTo(
            $"/asistencia?sesionId={_claseActual.SesionClaseId}");
    }
    protected override async Task OnInitializedAsync()
    {

   
        var authenticationState =
            await AuthenticationStateProvider
                .GetAuthenticationStateAsync();

        var principal =
            authenticationState.User;


        if (principal.Identity?.IsAuthenticated == true &&
            principal.IsInRole(
                RolesSistema.Medica))
        {
            Navigation.NavigateTo(
                "/aptos-medicos",
                replace: true);

            return;
        }


        if (principal.Identity?.IsAuthenticated == true)
        {
            // Creamos un scope independiente para que Home tenga
            // su propio UserManager + AppDbContext.
            using var scope =
                ScopeFactory.CreateScope();

            var userManager =
                scope.ServiceProvider
                    .GetRequiredService<UserManager<AppUser>>();

            var usuario =
                await userManager.GetUserAsync(principal);


            if (usuario is not null)
            {
                _nombreCompleto =
                    !string.IsNullOrWhiteSpace(
                        usuario.NombreCompleto)

                        ? usuario.NombreCompleto

                        : usuario.Email ?? "Usuario";
                _usuarioId = usuario.Id;
            }
        }

        _dashboard =
            await DashboardService
                .ObtenerAsync();


        _cumpleanios =
            (await BebeService
                .ObtenerCumpleaniosMesAsync(
                    _ahora.Year,
                    _ahora.Month))
            .ToList();

        await CargarClaseActualAsync();
    }



    protected override Task OnAfterRenderAsync(
        bool firstRender)
    {
        if (firstRender)
        {
            _relojTimer =
                new Timer(
                    _ =>
                    {
                        _ = InvokeAsync(
                            () =>
                            {
                                _ahora = ObtenerAhoraArgentina();

                                StateHasChanged();
                            });
                    },
                    null,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(30));
        }

        return Task.CompletedTask;
    }



    private async Task CargarClaseActualAsync()
    {
        var ahoraArgentina =
            ObtenerAhoraArgentina();

        var hoy =
            DateOnly.FromDateTime(
                ahoraArgentina);

        var hora =
            TimeOnly.FromDateTime(
                ahoraArgentina);


        var clases =
            await AsistenciaService
                .ListarSesionesDiaAsync(
                    hoy);


        var actual =
            clases.FirstOrDefault(x =>
                x.EstadoSesion !=
                    EstadoSesionClase.Cancelada
                &&
                x.HoraInicio <= hora
                &&
                x.HoraFin >= hora);


        if (actual is null)
        {
            _claseActual =
                null;

            return;
        }


        _claseActual =
            await AsistenciaService
                .ObtenerSesionAsync(
                    actual.SesionClaseId);
    }

    // =========================================================
    // WHATSAPP - CUMPLEAÑOS
    // =========================================================

    private async Task SaludarCumpleaniosAsync(
        CumpleaniosBebeDto cumple)
    {
        try
        {
            var bebe =
                await BebeService
                    .ObtenerAsync(
                        cumple.BebeId);


            if (bebe is null)
            {
                return;
            }


            var responsable =
                bebe.Responsables
                    .FirstOrDefault(x =>
                        x.EsContactoPrincipal)
                ??
                bebe.Responsables
                    .FirstOrDefault();


            if (responsable is null)
            {
                return;
            }


            if (!responsable.AutorizaComunicaciones)
            {
                return;
            }


            if (string.IsNullOrWhiteSpace(
                responsable.WhatsApp))
            {
                return;
            }


            var mensaje =
                $"¡Hola {responsable.Nombre}! 👋\n\n" +
                $"Desde Bebés Nadadores queremos desearle a " +
                $"{bebe.Nombre} un muy feliz cumpleaños 🎂🥳🏊\n\n" +
                $"¡Esperamos que pase un día hermoso!\n\n" +
                $"Bebés Nadadores";


            var url = WhatsAppService.CrearUrl(responsable.WhatsApp, mensaje);

            await JS.InvokeVoidAsync(
     "abrirWhatsAppBebesNadadores",
     url);
        }
        catch
        {
            /*
             * Después, si querés, mostramos
             * un mensaje visual en el dashboard.
             */
        }
    }
    private async Task MarcarAsistencia(
        AsistenciaAlumnoDto alumno,
        EstadoAsistencia estado)
    {
        if (_claseActual is null)
            return;


        try
        {
            var nuevoEstado =
                alumno.Estado == estado
                    ? EstadoAsistencia.Pendiente
                    : estado;


            await AsistenciaService
                .MarcarAsync(
                    new RegistrarAsistenciaDto
                    {
                        SesionClaseId =
                            _claseActual.SesionClaseId,

                        BebeId =
                            alumno.BebeId,

                        Estado =
                            nuevoEstado,

                        Observaciones =
                            alumno.Observaciones,

                        UsuarioId =
                            _usuarioId
                    });


            _claseActual =
                await AsistenciaService
                    .ObtenerSesionAsync(
                        _claseActual.SesionClaseId);


            await InvokeAsync(
                StateHasChanged);
        }
        catch
        {
            /*
             * Después podemos mostrar snackbar acá.
             * No dejamos romper el circuito Blazor.
             */
        }
    }


    private static string ObtenerIniciales(
        string nombre)
    {
        var partes =
            nombre.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length == 0)
            return "?";

        if (partes.Length == 1)
            return partes[0][0]
                .ToString()
                .ToUpperInvariant();

        return string.Concat(
            partes[0][0],
            partes[^1][0])
            .ToUpperInvariant();
    }









    private List<CumpleaniosBebeDto>
        CumpleaniosDelMes =>
            _cumpleanios;


    private List<CumpleaniosBebeDto>
        CumpleaniosHoy =>
            _cumpleanios
                .Where(x =>
                    x.EsHoy)
                .ToList();


    private static string ObtenerMesAbreviado(
        DateOnly fecha)
    {
        var cultura =
            new System.Globalization.CultureInfo("es-AR");

        return fecha
            .ToDateTime(TimeOnly.MinValue)
            .ToString("MMM", cultura)
            .TrimEnd('.')
            .ToUpperInvariant();
    }

    private static string ObtenerNombreMes(
        DateTime fecha)
    {
        var cultura =
            new System.Globalization.CultureInfo("es-AR");

        return cultura
            .DateTimeFormat
            .GetMonthName(fecha.Month)
            .ToLowerInvariant();
    }
    public void Dispose()
    {
        _relojTimer?.Dispose();
    }
    private static string FormatoMoneda(
        decimal valor)
    {
        return $"$ {valor.ToString(
            "N0",
            new System.Globalization.CultureInfo("es-AR"))}";
    }


    private static DateTime ObtenerAhoraArgentina()
    {
        var zona =
            ObtenerZonaHorariaArgentina();

        return TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                zona)
            .DateTime;
    }


    private static TimeZoneInfo ObtenerZonaHorariaArgentina()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "America/Argentina/Buenos_Aires");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Argentina Standard Time");
        }
    }
}
