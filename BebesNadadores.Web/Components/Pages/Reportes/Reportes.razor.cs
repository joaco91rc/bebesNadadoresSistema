using System.Globalization;

using BebesNadadores.Application.Reportes;
using BebesNadadores.Application.Reportes.Dtos;

using BebesNadadores.Web.Authorization;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Reportes;

public partial class Reportes : ComponentBase
{
    // =============================================================
    // SERVICIOS
    // =============================================================

    [Inject]
    protected IReporteService ReporteService { get; set; }
        = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; }
        = null!;

    [Inject]
    protected PermisosUsuario Permisos { get; set; }
        = null!;

    /*
     * SoloLectura ve los reportes pero no entra a la pantalla de
     * Pagos (que permite registrar cobros).
     */
    protected bool _puedeVerPagos;


    // =============================================================
    // ESTADO
    // =============================================================

    protected DateOnly _desde;

    protected DateOnly _hasta;

    protected ReporteGeneralDto? _reporte;

    protected bool _cargando;

    protected string? _error;

    protected TabReporte _tab =
        TabReporte.Resumen;


    private static readonly CultureInfo Cultura =
        CultureInfo.GetCultureInfo(
            "es-AR");


    // =============================================================
    // INICIALIZACIÓN
    // =============================================================

    protected override async Task OnInitializedAsync()
    {
        _puedeVerPagos =
            await Permisos.TieneAsync(
                PoliticasSistema.GestionCaja);

        var hoy =
            ObtenerHoyArgentina();

        _desde =
            new DateOnly(
                hoy.Year,
                hoy.Month,
                1);

        _hasta =
            hoy;

        await CargarAsync();
    }


    // =============================================================
    // CARGA
    // =============================================================

    protected async Task CargarAsync()
    {
        if (_hasta <
            _desde)
        {
            _error =
                "La fecha Hasta no puede ser anterior a Desde.";

            return;
        }

        try
        {
            _cargando =
                true;

            _error =
                null;

            _reporte =
                await ReporteService
                    .ObtenerAsync(
                        _desde,
                        _hasta);
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _cargando =
                false;
        }
    }


    // =============================================================
    // FECHAS
    // =============================================================

    protected Task CambiarDesdeAsync(
        DateOnly fecha)
    {
        _desde =
            fecha;

        if (_hasta <
            _desde)
        {
            _hasta =
                _desde;
        }

        return Task.CompletedTask;
    }


    protected Task CambiarHastaAsync(
        DateOnly fecha)
    {
        _hasta =
            fecha;

        if (_desde >
            _hasta)
        {
            _desde =
                _hasta;
        }

        return Task.CompletedTask;
    }


    protected async Task PeriodoMesActualAsync()
    {
        var hoy =
            ObtenerHoyArgentina();

        _desde =
            new DateOnly(
                hoy.Year,
                hoy.Month,
                1);

        _hasta =
            hoy;

        await CargarAsync();
    }


    protected async Task PeriodoMesAnteriorAsync()
    {
        var hoy =
            ObtenerHoyArgentina();

        var primerDiaMesActual =
            new DateOnly(
                hoy.Year,
                hoy.Month,
                1);

        var ultimoMesAnterior =
            primerDiaMesActual
                .AddDays(-1);

        _desde =
            new DateOnly(
                ultimoMesAnterior.Year,
                ultimoMesAnterior.Month,
                1);

        _hasta =
            ultimoMesAnterior;

        await CargarAsync();
    }


    protected async Task PeriodoUltimos30Async()
    {
        var hoy =
            ObtenerHoyArgentina();

        _desde =
            hoy.AddDays(-29);

        _hasta =
            hoy;

        await CargarAsync();
    }


    protected async Task PeriodoAnioActualAsync()
    {
        var hoy =
            ObtenerHoyArgentina();

        _desde =
            new DateOnly(
                hoy.Year,
                1,
                1);

        _hasta =
            hoy;

        await CargarAsync();
    }


    // =============================================================
    // TABS
    // =============================================================

    protected void SeleccionarTab(
        TabReporte tab)
    {
        _tab =
            tab;
    }


    protected string ClaseTab(
        TabReporte tab)
    {
        return _tab ==
               tab
            ? "active"
            : string.Empty;
    }


    // =============================================================
    // FORMATO
    // =============================================================

    protected static string FormatoMoneda(
        decimal importe)
    {
        return importe.ToString(
            "C0",
            Cultura);
    }


    protected static string FormatoPorcentaje(
        decimal valor)
    {
        return $"{valor:0.#}%";
    }


    protected static string AnchoBarra(
        decimal valor)
    {
        var normalizado =
            Math.Clamp(
                valor,
                0m,
                100m);

        return
            $"width: {normalizado.ToString("0.##", CultureInfo.InvariantCulture)}%;";
    }


    protected static string ClaseImporte(
        decimal importe)
    {
        if (importe >
            0)
        {
            return "positive";
        }

        if (importe <
            0)
        {
            return "negative";
        }

        return string.Empty;
    }


    protected static string FormatoFechaHora(
        DateTimeOffset fechaUtc)
    {
        TimeZoneInfo zona;

        try
        {
            zona =
                TimeZoneInfo
                    .FindSystemTimeZoneById(
                        "America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            zona =
                TimeZoneInfo
                    .FindSystemTimeZoneById(
                        "Argentina Standard Time");
        }

        var local =
            TimeZoneInfo.ConvertTime(
                fechaUtc,
                zona);

        return local.ToString(
            "dd/MM/yyyy HH:mm",
            Cultura);
    }


    protected static string NombreDia(
        DateOnly fecha)
    {
        return fecha.DayOfWeek switch
        {
            DayOfWeek.Monday =>
                "Lunes",

            DayOfWeek.Tuesday =>
                "Martes",

            DayOfWeek.Wednesday =>
                "Miércoles",

            DayOfWeek.Thursday =>
                "Jueves",

            DayOfWeek.Friday =>
                "Viernes",

            DayOfWeek.Saturday =>
                "Sábado",

            DayOfWeek.Sunday =>
                "Domingo",

            _ =>
                string.Empty
        };
    }


    // =============================================================
    // DESCARGAS
    // =============================================================

    protected void DescargarExcel()
    {
        DescargarArchivo(
            "excel");
    }


    protected void DescargarPdf()
    {
        DescargarArchivo(
            "pdf");
    }


    private void DescargarArchivo(
        string formato)
    {
        if (_hasta <
            _desde)
        {
            _error =
                "La fecha Hasta no puede ser anterior a Desde.";

            return;
        }

        var desde =
            _desde.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

        var hasta =
            _hasta.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture);

        /*
         * La descarga se hace por un endpoint HTTP independiente
         * del circuito SignalR de Blazor.
         *
         * Esto es mejor para archivos medianos/grandes y evita
         * transportar todo el PDF/XLSX por el WebSocket.
         */
        Navigation.NavigateTo(
            $"/reportes/descargar/{formato}" +
            $"?desde={desde}&hasta={hasta}",
            forceLoad: true);
    }


    // =============================================================
    // NAVEGACIÓN
    // =============================================================

    protected void AbrirPagos(
        Guid bebeId)
    {
        if (!_puedeVerPagos)
            return;

        /*
         * Si tu pantalla de pagos usa otra ruta para abrir
         * un bebé puntual, cambiá solamente esta URL.
         */
        Navigation.NavigateTo(
            $"/pagos?bebeId={bebeId}");
    }


    protected void IrAAptosMedicos()
    {
        Navigation.NavigateTo(
            "/aptos-medicos");
    }


    protected void Volver()
    {
        Navigation.NavigateTo(
            "/");
    }


    // =============================================================
    // HORA ARGENTINA
    // =============================================================

    private static DateOnly ObtenerHoyArgentina()
    {
        TimeZoneInfo zona;

        try
        {
            zona =
                TimeZoneInfo
                    .FindSystemTimeZoneById(
                        "America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            zona =
                TimeZoneInfo
                    .FindSystemTimeZoneById(
                        "Argentina Standard Time");
        }

        var ahora =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                zona);

        return DateOnly.FromDateTime(
            ahora.DateTime);
    }


    // =============================================================
    // TAB
    // =============================================================

    protected enum TabReporte
    {
        Resumen,
        Asistencia,
        Ocupacion,
        Recuperos,
        Finanzas,
        Caja,
        Bancos,
        Salud
    }
}
