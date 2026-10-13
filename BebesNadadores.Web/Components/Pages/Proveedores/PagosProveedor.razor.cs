using System.Globalization;
using System.Security.Claims;
using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Application.Proveedores;
using BebesNadadores.Application.Proveedores.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace BebesNadadores.Web.Components.Pages.Proveedores;

public partial class PagosProveedor
    : ComponentBase
{
    // =========================================================
    // INYECCIONES
    // =========================================================

    [Inject]
    protected IProveedorService ProveedorService { get; set; }
        = null!;

    [Inject]
    protected IPagoProveedorService PagoProveedorService { get; set; }
        = null!;

    [Inject]
    protected ICuentaBancariaService CuentaBancariaService { get; set; }
        = null!;

    [Inject]
    protected AuthenticationStateProvider AuthenticationStateProvider { get; set; }
        = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; }
        = null!;


    // =========================================================
    // PARÁMETRO
    // =========================================================

    [Parameter]
    public Guid ProveedorId { get; set; }


    // =========================================================
    // ESTADO
    // =========================================================

    protected ProveedorDto? _proveedor;

    protected List<PagoProveedorDto> _pagos =
        [];

    protected List<CuentaBancariaDto> _cuentasBancarias =
        [];


    protected RegistrarPagoProveedorDto _form =
        new();


    protected bool _cargando =
        true;

    protected bool _mostrarPago;

    protected bool _guardando;

    protected string? _error;

    protected string? _errorModal;

    protected string? _mensaje;


    // =========================================================
    // CALCULADOS
    // =========================================================

    protected decimal TotalPagado =>
        _pagos.Sum(x =>
            x.Importe);


    protected decimal TotalEfectivo =>
        _pagos
            .Where(x =>
                x.MedioPago ==
                    MedioPago.Efectivo)
            .Sum(x =>
                x.Importe);


    protected decimal TotalBancario =>
        _pagos
            .Where(x =>
                x.MedioPago !=
                    MedioPago.Efectivo)
            .Sum(x =>
                x.Importe);


    protected bool RequiereCuentaBancaria =>
        _form.MedioPago !=
            MedioPago.Efectivo;


    // =========================================================
    // INIT
    // =========================================================

    protected override async Task
        OnParametersSetAsync()
    {
        await CargarAsync();
    }


    private async Task CargarAsync()
    {
        try
        {
            _cargando =
                true;

            _error =
                null;


            _proveedor =
                await ProveedorService
                    .ObtenerAsync(
                        ProveedorId);


            if (_proveedor is null)
            {
                return;
            }


            var pagosTask =
                PagoProveedorService
                    .ListarAsync(
                        ProveedorId);


            var cuentasTask =
                CuentaBancariaService
                    .ListarAsync(
                        incluirInactivas: false);


            await Task.WhenAll(
                pagosTask,
                cuentasTask);


            _pagos =
                (await pagosTask)
                    .ToList();


            _cuentasBancarias =
                (await cuentasTask)
                    .Where(x =>
                        x.Activa)
                    .OrderBy(x =>
                        x.Banco)
                    .ThenBy(x =>
                        x.Nombre)
                    .ToList();
        }
        catch (Exception ex)
        {
            _error =
                ex.Message;
        }
        finally
        {
            _cargando =
                false;
        }
    }


    // =========================================================
    // NUEVO PAGO
    // =========================================================

    protected void AbrirPago()
    {
        if (_proveedor is null ||
            !_proveedor.Activo)
        {
            return;
        }


        _form =
            new RegistrarPagoProveedorDto
            {
                ProveedorId =
                    ProveedorId,

                MedioPago =
                    MedioPago.Transferencia,

                CuentaBancariaId =
                    _cuentasBancarias
                        .FirstOrDefault()
                        ?.Id
            };


        _errorModal =
            null;

        _mensaje =
            null;

        _mostrarPago =
            true;
    }


    protected void MedioPagoCambio(
        ChangeEventArgs e)
    {
        if (Enum.TryParse<MedioPago>(
            e.Value?.ToString(),
            out var medio))
        {
            _form.MedioPago =
                medio;
        }


        if (_form.MedioPago ==
            MedioPago.Efectivo)
        {
            _form.CuentaBancariaId =
                null;
        }
        else if (!_form.CuentaBancariaId.HasValue)
        {
            _form.CuentaBancariaId =
                _cuentasBancarias
                    .FirstOrDefault()
                    ?.Id;
        }
    }


    protected async Task RegistrarPagoAsync()
    {
        try
        {
            _guardando =
                true;

            _errorModal =
                null;


            if (_form.Importe <= 0)
            {
                _errorModal =
                    "Ingresá un importe mayor a cero.";

                return;
            }


            if (string.IsNullOrWhiteSpace(
                _form.Concepto))
            {
                _errorModal =
                    "Ingresá el concepto del pago.";

                return;
            }


            if (
                RequiereCuentaBancaria
                &&
                !_form.CuentaBancariaId.HasValue)
            {
                _errorModal =
                    "Seleccioná la cuenta desde la que se realizó el pago.";

                return;
            }


            var auth =
                await AuthenticationStateProvider
                    .GetAuthenticationStateAsync();


            var usuarioIdTexto =
                auth.User.FindFirstValue(
                    ClaimTypes.NameIdentifier);


            if (
                !Guid.TryParse(
                    usuarioIdTexto,
                    out var usuarioId))
            {
                throw new InvalidOperationException(
                    "No se pudo determinar el usuario autenticado.");
            }


            await PagoProveedorService
                .RegistrarAsync(
                    _form,
                    usuarioId);


            _mostrarPago =
                false;


            _mensaje =
                "El pago al proveedor fue registrado correctamente.";


            await RecargarPagosAsync();
        }
        catch (Exception ex)
        {
            _errorModal =
                ex.Message;
        }
        finally
        {
            _guardando =
                false;
        }
    }


    private async Task RecargarPagosAsync()
    {
        var datos =
            await PagoProveedorService
                .ListarAsync(
                    ProveedorId);


        _pagos =
            datos.ToList();


        /*
         * Volvemos a consultar el proveedor porque
         * TotalPagado y UltimoPago también cambiaron.
         */
        _proveedor =
            await ProveedorService
                .ObtenerAsync(
                    ProveedorId);
    }


    protected void CerrarPago()
    {
        if (_guardando)
            return;


        _mostrarPago =
            false;

        _errorModal =
            null;
    }


    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo(
            "/proveedores");
    }


    // =========================================================
    // HELPERS
    // =========================================================

    protected static string FormatearMoneda(
        decimal importe)
    {
        return importe.ToString(
            "C2",
            CultureInfo.GetCultureInfo(
                "es-AR"));
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


    private static DateTimeOffset
        ConvertirArgentina(
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