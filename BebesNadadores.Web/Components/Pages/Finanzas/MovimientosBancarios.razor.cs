using System.Globalization;
using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Finanzas;

public partial class MovimientosBancarios
    : ComponentBase
{
    [Parameter]
    public Guid CuentaId { get; set; }


    // =========================================================
    // DATOS
    // =========================================================

    protected ResumenMovimientosBancariosDto? _datos;

    protected MovimientoBancarioDetalleDto? _detalle;


    // =========================================================
    // FILTROS
    // =========================================================

    protected string _buscar =
        string.Empty;


    protected DateOnly? _desde =
        new DateOnly(
            DateTime.Today.Year,
            DateTime.Today.Month,
            1);


    protected DateOnly? _hasta =
        DateOnly.FromDateTime(
            DateTime.Today);


    protected TipoMovimientoBancario? _tipo;

    protected OrigenMovimientoBancario? _origen;


    // =========================================================
    // ESTADO
    // =========================================================

    protected bool _cargando =
        true;

    protected string? _error;


    // =========================================================
    // INFORMACIÓN DE CUENTA
    // =========================================================

    protected string NombreCuenta =>
        _datos?
            .Movimientos
            .FirstOrDefault()
            ?.Cuenta
        ??
        string.Empty;


    protected string NombreBanco =>
        _datos?
            .Movimientos
            .FirstOrDefault()
            ?.Banco
        ??
        string.Empty;


    // =========================================================
    // INIT
    // =========================================================

    protected override async Task
        OnParametersSetAsync()
    {
        await CargarAsync();
    }


    // =========================================================
    // CARGAR
    // =========================================================

    protected async Task CargarAsync()
    {
        try
        {
            _cargando =
                true;

            _error =
                null;


            _datos =
                await MovimientoBancarioService
                    .ListarFiltradoAsync(
                        CrearFiltro());
        }
        catch (Exception ex)
        {
            _error =
                ex.Message;

            _datos =
                null;
        }
        finally
        {
            _cargando =
                false;
        }
    }


    // =========================================================
    // FILTROS
    // =========================================================

    protected async Task AplicarFiltrosAsync()
    {
        await CargarAsync();
    }


    protected async Task LimpiarFiltrosAsync()
    {
        _buscar =
            string.Empty;

        _tipo =
            null;

        _origen =
            null;


        /*
         * Volvemos al mes actual.
         */
        _desde =
            new DateOnly(
                DateTime.Today.Year,
                DateTime.Today.Month,
                1);


        _hasta =
            DateOnly.FromDateTime(
                DateTime.Today);


        await CargarAsync();
    }


    private MovimientosBancariosFiltroDto
        CrearFiltro()
    {
        return new MovimientosBancariosFiltroDto
        {
            CuentaBancariaId =
                CuentaId,

            Desde =
                _desde,

            Hasta =
                _hasta,

            Tipo =
                _tipo,

            Origen =
                _origen,

            Buscar =
                string.IsNullOrWhiteSpace(
                    _buscar)

                    ? null
                    : _buscar.Trim()
        };
    }


    // =========================================================
    // DETALLE
    // =========================================================

    protected async Task AbrirDetalleAsync(
        Guid movimientoId)
    {
        try
        {
            _error =
                null;


            _detalle =
                await MovimientoBancarioService
                    .ObtenerDetalleAsync(
                        movimientoId);


            if (_detalle is null)
            {
                _error =
                    "El movimiento indicado no existe.";
            }
        }
        catch (Exception ex)
        {
            _error =
                ex.Message;
        }
    }


    protected void CerrarDetalle()
    {
        _detalle =
            null;
    }


    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo(
            "/bancos");
    }


    // =========================================================
    // TEXTOS
    // =========================================================

    protected static string ObtenerOrigenTexto(
        OrigenMovimientoBancario origen)
    {
        return origen switch
        {
            OrigenMovimientoBancario.PagoBebe =>
                "Pago de bebé",

            OrigenMovimientoBancario.PagoProveedor =>
                "Pago a proveedor",

            OrigenMovimientoBancario.AjusteManual =>
                "Ajuste manual",

            OrigenMovimientoBancario.Otro =>
                "Otro",

            _ =>
                origen.ToString()
        };
    }


    protected static string ObtenerClaseOrigen(
        OrigenMovimientoBancario origen)
    {
        return origen switch
        {
            OrigenMovimientoBancario.PagoBebe =>
                "baby-payment",

            OrigenMovimientoBancario.PagoProveedor =>
                "provider-payment",

            OrigenMovimientoBancario.AjusteManual =>
                "adjustment",

            _ =>
                "other"
        };
    }


    protected static string ObtenerMedioPagoTexto(
        MedioPago medio)
    {
        return medio switch
        {
            MedioPago.Efectivo =>
                "Efectivo",

            MedioPago.Transferencia =>
                "Transferencia",

            MedioPago.Tarjeta =>
                "Tarjeta",

            MedioPago.MercadoPago =>
                "Mercado Pago",

            MedioPago.Otro =>
                "Otro",

            _ =>
                medio.ToString()
        };
    }


    protected static string ObtenerClaseNeto(
        decimal neto)
    {
        if (neto > 0)
            return "positive";

        if (neto < 0)
            return "negative";

        return string.Empty;
    }


    // =========================================================
    // FORMATO
    // =========================================================

    protected static string FormatearMoneda(
        decimal importe)
    {
        return importe.ToString(
            "C2",
            CultureInfo.GetCultureInfo(
                "es-AR"));
    }


    protected static string FormatearPeriodo(
        DateOnly periodo)
    {
        return periodo.ToString(
            "MM/yyyy");
    }


    protected static string FormatearFecha(
        DateTimeOffset fechaUtc)
    {
        return ConvertirArgentina(
                fechaUtc)
            .ToString(
                "dd/MM/yyyy");
    }


    protected static string FormatearHora(
        DateTimeOffset fechaUtc)
    {
        return ConvertirArgentina(
                fechaUtc)
            .ToString(
                "HH:mm");
    }


    protected static string FormatearFechaHora(
        DateTimeOffset fechaUtc)
    {
        return ConvertirArgentina(
                fechaUtc)
            .ToString(
                "dd/MM/yyyy HH:mm");
    }


    private static DateTimeOffset ConvertirArgentina(
        DateTimeOffset fechaUtc)
    {
        return TimeZoneInfo.ConvertTime(
            fechaUtc,
            ObtenerZonaHorariaArgentina());
    }


    private static TimeZoneInfo
        ObtenerZonaHorariaArgentina()
    {
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
    }
}