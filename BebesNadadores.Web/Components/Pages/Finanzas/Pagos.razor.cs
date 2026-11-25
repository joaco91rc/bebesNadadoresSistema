using BebesNadadores.Web.Authorization;
using System.Globalization;
using System.Security.Claims;
using BebesNadadores.Application.Anulaciones;
using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Application.Caja;
using BebesNadadores.Application.Caja.Dtos;
using BebesNadadores.Application.Cuotas;
using BebesNadadores.Application.Pagos;
using BebesNadadores.Application.Pagos.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Microsoft.JSInterop;

namespace BebesNadadores.Web.Components.Pages.Finanzas;

public partial class Pagos : ComponentBase
{
    [Inject] private IGeneradorCuotasService GeneradorCuotas { get; set; } = default!;
    [Inject] private IBonificacionCuotaService Bonificaciones { get; set; } = default!;

    private int _cantidadAdelanto = 1;
    private bool _modalAdelanto;
    private bool _modalBonificar;
    private bool _guardandoAdelanto;
    private bool _guardandoBonificacion;
    private bool _modalConfirmacionBonificacion;
    private DateOnly _periodoAdelanto = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private Guid? _cuotaBonificarId;
    private decimal _importeBonificar;
    private string _motivoBonificar = string.Empty;
    private string? _errorOperacion;

    private void AbrirAdelanto()
    {
        if (_cuenta is null) return;
        var hoy = FechaHoyArgentina();
        _periodoAdelanto = new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(1);
        _errorOperacion = null;
        _cantidadAdelanto = 1;
        _modalAdelanto = true;
    }

    private void CambiarPeriodoAdelanto(ChangeEventArgs e)
    {
        if (DateOnly.TryParseExact($"{e.Value}-01", "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var periodo))
            _periodoAdelanto = periodo;
    }

    private async Task GenerarAdelantoAsync()
    {
        if (_cuenta is null || _guardandoAdelanto) return;
        _guardandoAdelanto = true;
        _errorOperacion = null;
        try
        {
            await PermisosRevision.ExigirAsync(PoliticasSistema.GestionCaja);
            var resultado = await GeneradorCuotas.GenerarPeriodosAsync(_periodoAdelanto, _cantidadAdelanto,
                _cuenta.BebeId, await ObtenerUsuarioIdAsync());
            _modalAdelanto = false;
            await RecargarDespuesDePagoAsync();
            _mensaje = $"Cuotas generadas: {resultado.CuotasGeneradas}. Ya existentes: {resultado.CuotasYaExistentes}.";
            if (resultado.Advertencias.Count > 0) { _error = string.Join(" | ", resultado.Advertencias); return; }
            var inicio = new DateOnly(_periodoAdelanto.Year, _periodoAdelanto.Month, 1);
            var cuota = _cuenta?.Cuotas.Where(x => x.Periodo >= inicio && x.Periodo < inicio.AddMonths(_cantidadAdelanto)
                && x.Estado != EstadoCuota.Anulada && x.Saldo > 0).OrderBy(x => x.Periodo).FirstOrDefault();
            if (cuota is not null)
            {
                await AbrirRegistrarPagoAsync();
                _pagoForm.CuotaIdObjetivo = cuota.Id;
                _pagoForm.Importe = cuota.Saldo;
                await ActualizarPreviewAsync();
            }
        }
        catch (Exception ex) { _errorOperacion = Errores.Mensaje(ex); }
        finally { _guardandoAdelanto = false; }
    }

    private void AbrirBonificacion(CuotaCuentaCorrienteDto? cuota = null)
    {
        if (_cuenta is null) return;
        _modalConfirmacionBonificacion = false;
        var pendiente = cuota ?? _cuenta.Cuotas
            .Where(x => x.Estado != EstadoCuota.Anulada && x.Saldo > 0)
            .OrderBy(x => x.Periodo).FirstOrDefault();
        _cuotaBonificarId = pendiente?.Id;
        _importeBonificar = pendiente?.Saldo ?? 0m;
        _motivoBonificar = string.Empty;
        _errorOperacion = null;
        _modalBonificar = true;
    }

    private void BonificacionSeleccionada(ChangeEventArgs e)
    {
        _cuotaBonificarId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        _importeBonificar = _cuenta?.Cuotas.FirstOrDefault(x => x.Id == _cuotaBonificarId)?.Saldo ?? 0;
    }

    private void BonificacionImporteCambio(ChangeEventArgs e)
    {
        var texto = e.Value?.ToString();
        if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.InvariantCulture, out _importeBonificar))
            if (!decimal.TryParse(texto, NumberStyles.Number, CultureInfo.GetCultureInfo("es-AR"), out _importeBonificar))
                _importeBonificar = 0;
    }

    private async Task CuotaObjetivoCambioAsync(ChangeEventArgs e)
    {
        _pagoForm.CuotaIdObjetivo = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null;
        var cuota = _cuenta?.Cuotas.FirstOrDefault(x => x.Id == _pagoForm.CuotaIdObjetivo);
        if (cuota is not null) _pagoForm.Importe = cuota.Saldo;
        await ActualizarPreviewAsync();
    }

    private void PasarABonificacion()
    {
        var seleccionada = _cuenta?.Cuotas.FirstOrDefault(x => x.Id == _pagoForm.CuotaIdObjetivo);
        _modalPago = false;
        _preview = null;
        AbrirBonificacion(seleccionada);
    }

    private void SolicitarConfirmacionBonificacionAsync()
    {
        if (!_cuotaBonificarId.HasValue || _guardandoBonificacion) return;
        var cuota = _cuenta?.Cuotas.FirstOrDefault(x => x.Id == _cuotaBonificarId.Value);
        if (cuota is null || _importeBonificar <= 0 || _importeBonificar > cuota.Saldo)
        {
            _errorOperacion = "Ingresá un importe dentro del saldo pendiente de la cuota.";
            return;
        }

        _errorOperacion = null;
        _modalConfirmacionBonificacion = true;
    }

    private async Task ConfirmarBonificacionAsync()
    {
        if (!_cuotaBonificarId.HasValue || _guardandoBonificacion) return;
        _guardandoBonificacion = true;
        _errorOperacion = null;
        try
        {
            await PermisosRevision.ExigirAsync(PoliticasSistema.GestionCaja);
            var cuota = _cuenta?.Cuotas.FirstOrDefault(x => x.Id == _cuotaBonificarId.Value);
            if (cuota is null || _importeBonificar <= 0 || _importeBonificar > cuota.Saldo)
                throw new InvalidOperationException("Ingresá un importe dentro del saldo pendiente de la cuota.");

            await Bonificaciones.BonificarAsync(cuota.Id, _importeBonificar,
                _motivoBonificar, await ObtenerUsuarioIdAsync());
            _modalConfirmacionBonificacion = false;
            _modalBonificar = false;
            _mensaje = $"Bonificación de {FormatearMoneda(_importeBonificar)} registrada correctamente.";
            await RecargarDespuesDePagoAsync();
        }
        catch (Exception ex) { _errorOperacion = Errores.Mensaje(ex); }
        finally { _guardandoBonificacion = false; }
    }

    [Inject] private BebesNadadores.Web.Services.Comprobantes.EnlaceComprobantePagoService EnlacesComprobantes { get; set; } = default!;

    private bool _soloRevisar;
    private bool _confirmandoRevision;
    private IReadOnlyDictionary<Guid, BebesNadadores.Web.Services.Finanzas.IndicadorFinancieroDto> _estadosOperativos = new Dictionary<Guid, BebesNadadores.Web.Services.Finanzas.IndicadorFinancieroDto>();
    private BebesNadadores.Web.Services.Finanzas.IndicadorFinancieroDto? _estadoOperativo;
    private async Task CargarEstadosOperativosAsync()
    {
        if (!_soloRevisar) return;
        try { _estadosOperativos = await IndicadoresOperativos.ObtenerAsync(_bebes.Select(b => b.BebeId).Distinct().ToArray()); }
        catch (Exception ex) { _soloRevisar = false; _error = Errores.Mensaje(ex); }
    }
    private async Task ActualizarEstadoOperativoAsync(Guid id)
    {
        var estados = await IndicadoresOperativos.ObtenerAsync(new[] { id });
        _estadoOperativo = estados.GetValueOrDefault(id);
    }
    private async Task ConfirmarRevisionOperativaAsync()
    {
        if (_cuenta is null || _confirmandoRevision || _estadoOperativo?.Tipo != BebesNadadores.Web.Services.Finanzas.IndicadorFinancieroTipo.Revisar) return;
        var id = _cuenta.BebeId;
        _confirmandoRevision = true;
        try
        {
            await PermisosRevision.ExigirAsync(PoliticasSistema.GestionCaja);
            if (!await JS.InvokeAsync<bool>("confirm", new object?[] { "¿Confirmás que verificaste la cuenta y todos los períodos pendientes hasta hoy? Esto no modifica pagos ni cuotas; las deudas reales seguirán visibles." })) return;
            await IndicadoresOperativos.ConfirmarRevisionAsync(id, await ObtenerUsuarioIdAsync());
            await CargarCuentaAsync(id);
            await CargarEstadosOperativosAsync();
            _mensaje = "Revisión registrada. Se conservan los saldos de deuda reales.";
        }
        catch (Exception ex) { _error = Errores.Mensaje(ex); }
        finally { _confirmandoRevision = false; }
    }

    private BebesNadadores.Application.Bebes.Dtos.BebeLegajoDto? _fichaOperativa;
    private bool _fichaAsistencia;
    private async Task AbrirFichaOperativaAsync()
    {
        if (_cuenta is null) return;
        try
        {
            _fichaAsistencia = false;
            _fichaOperativa = await LegajosOperativos.ObtenerAsync(_cuenta.BebeId);
            if (_fichaOperativa is null) _error = "No se encontró la ficha.";
        }
        catch (Exception ex) { _error = Errores.Mensaje(ex); }
    }

    // =========================================================
    // DATOS
    // =========================================================

    protected List<BebePagoResumenDto> _bebes =
        [];

    protected CuentaCorrienteBebeDto? _cuenta;

    protected List<CuentaBancariaDto> _cuentasBancarias =
        [];

    protected CajaDetalleDto? _cajaHoy;


    // =========================================================
    // FORMULARIO PAGO
    // =========================================================

    protected RegistrarPagoDto _pagoForm =
        new();

    protected PreviewPagoDto? _preview;


    // =========================================================
    // ESTADO
    // =========================================================

    protected string _buscar =
        string.Empty;

    protected Guid? _bebeSeleccionadoId;


    protected bool _cargando =
        true;

    protected bool _cargandoCuenta;

    protected bool _modalPago;

    protected bool _procesandoPago;

    protected bool _cargandoPreview;


    protected string? _error;

    protected string? _mensaje;

    protected string? _errorPago;

    protected string _vistaCuenta =
    "resumen";

    // En lugar de descargar el PDF o abrir el menú nativo de Windows,
    // generamos un enlace público con vencimiento de 30 días.
    protected async Task CompartirComprobanteAsync(PagoCuentaCorrienteDto pago)
    {
        if (pago.Anulado) return;

        try
        {
            _error = null;
            _mensaje = null;

            var enlace = await EnlacesComprobantes.CrearEnlaceDescriptivoAsync(pago.Id, Navigation.BaseUri);
            var mensaje = $"Buen día, te compartimos el comprobante del pago abonado.\n\n" +
                          $"📄 Descargalo desde este enlace (válido por 30 días):\n{enlace}\n\n" +
                          "Muchas gracias. Bebés Nadadores 💙";

            await using var modulo = await JS.InvokeAsync<IJSObjectReference>(
                "import", "/js/comprobantes-pagos.js");

            var abierto = await modulo.InvokeAsync<bool>(
                "abrirWhatsAppComprobante", mensaje);

            _mensaje = abierto
                ? "Se abrió WhatsApp con el enlace de descarga. Revisá el destinatario y enviá el mensaje."
                : "El navegador bloqueó la pestaña. El mensaje quedó copiado; habilitá ventanas emergentes para este sitio.";
        }
        catch (JSException)
        {
            _error = "No se pudo abrir WhatsApp. Probá habilitando las ventanas emergentes.";
        }
        catch (InvalidOperationException)
        {
            _error = "No fue posible preparar el enlace del comprobante.";
        }
    }

    protected string ObtenerNombreResponsableParaWhatsApp()
    {
        var responsable = _cuenta?.Responsables?
            .FirstOrDefault(x => x.EsPrincipal)?.NombreCompleto
            ?? _cuenta?.Responsables?.FirstOrDefault()?.NombreCompleto;

        if (!string.IsNullOrWhiteSpace(responsable))
            return responsable.Trim();

        return "familia";
    }


private CancellationTokenSource? _busquedaCts;



protected bool _buscando;

// =========================================================
// CUENTA CORRIENTE UNIFICADA
// =========================================================

protected IReadOnlyList<MovimientoCuentaViewModel>
    MovimientosCuenta =>
        ConstruirMovimientosCuenta();


private IReadOnlyList<MovimientoCuentaViewModel>
    ConstruirMovimientosCuenta()
{
    if (_cuenta is null)
    {
        return [];
    }


    var movimientos =
        new List<MovimientoCuentaViewModel>();


    // =====================================================
    // CUOTAS = DÉBITOS
    // =====================================================

    foreach (
        var cuota in
        _cuenta.Cuotas
            .Where(x =>
                x.Estado !=
                    EstadoCuota.Anulada))
    {
        movimientos.Add(
            new MovimientoCuentaViewModel
            {
                Fecha =
                    cuota.Periodo
                        .ToDateTime(
                            TimeOnly.MinValue),

                Orden =
                    0,

                FechaTexto =
                    cuota.Periodo
                        .ToString(
                            "dd/MM/yyyy"),

                Tipo =
                    "Cuota",

                Concepto =
                    $"Cuota {FormatearPeriodo(cuota.Periodo)}",

                Detalle =
                    !string.IsNullOrWhiteSpace(
                        cuota.Descripcion)

                        ? cuota.Descripcion

                        : $"Vencimiento " +
                          $"{cuota.FechaVencimiento:dd/MM/yyyy}",

                Debito =
                    cuota.Importe,

                Credito =
                    0
            });
    }


    // =====================================================
    // PAGOS = CRÉDITOS
    // =====================================================

    foreach (var pago in _cuenta.Pagos)
    {
        var fechaArgentina =
            ConvertirFechaArgentina(
                pago.FechaUtc);


        var detalle =
            ObtenerDetallePagoCuenta(
                pago);

        var esReservaVacante = pago.Observaciones?.StartsWith(
            "Reserva vacante ciclo", StringComparison.OrdinalIgnoreCase) == true;


        movimientos.Add(
            new MovimientoCuentaViewModel
            {
                Fecha =
                    fechaArgentina,

                Orden =
                    1,

                FechaTexto =
                    fechaArgentina
                        .ToString(
                            "dd/MM/yyyy HH:mm"),

                Tipo =
                    "Pago",

                Concepto = esReservaVacante
                    ? "Reserva de vacante" + (pago.Anulado ? " (anulada)" : "")
                    : $"Pago - " + $"{ObtenerMedioPagoTexto(pago.MedioPago)}" +
                      (pago.Anulado ? " (anulado)" : ""),

                Detalle =
                    detalle,

                Debito =
                    0,

                Credito =
                    pago.Importe,

                MedioPago =
                    pago.MedioPago
            });


        /*
         * Pago anulado: se muestra el pago original y, en la fecha
         * de la anulación, el contra-movimiento (débito) que lo
         * revierte. Así el saldo queda bien y se ve el historial.
         */
        if (pago.Anulado)
        {
            var fechaAnulacion =
                ConvertirFechaArgentina(
                    pago.AnuladoUtc ?? pago.FechaUtc);

            movimientos.Add(
                new MovimientoCuentaViewModel
                {
                    Fecha =
                        fechaAnulacion,

                    Orden =
                        2,

                    FechaTexto =
                        fechaAnulacion
                            .ToString(
                                "dd/MM/yyyy HH:mm"),

                    Tipo =
                        "Anulación",

                    Concepto =
                        $"Anulación de pago - " +
                        $"{ObtenerMedioPagoTexto(pago.MedioPago)}",

                    Detalle =
                        pago.MotivoAnulacion,

                    Debito =
                        pago.Importe,

                    Credito =
                        0,

                    MedioPago =
                        pago.MedioPago
                });
        }
    }


    // =====================================================
    // SALDO ACUMULADO
    // =====================================================

    var ordenados =
        movimientos
            .OrderBy(x =>
                x.Fecha)

            .ThenBy(x =>
                x.Orden)

            .ToList();


    decimal saldo =
        0;


    foreach (var movimiento in ordenados)
    {
        saldo +=
            movimiento.Debito;

        saldo -=
            movimiento.Credito;


        movimiento.Saldo =
            saldo;
    }


    /*
     * Calculamos el saldo cronológicamente
     * y después mostramos lo más reciente arriba.
     */
    return ordenados
        .OrderByDescending(x =>
            x.Fecha)

        .ThenByDescending(x =>
            x.Orden)

        .ToList();
}


private static string? ObtenerDetallePagoCuenta(
    PagoCuentaCorrienteDto pago)
{
    var partes =
        new List<string>();


    if (!string.IsNullOrWhiteSpace(
        pago.CuentaBancaria))
    {
        partes.Add(
            pago.CuentaBancaria);
    }


    if (!string.IsNullOrWhiteSpace(
        pago.ReferenciaExterna))
    {
        partes.Add(
            $"Ref. {pago.ReferenciaExterna}");
    }


    if (!string.IsNullOrWhiteSpace(
        pago.Observaciones))
    {
        partes.Add(
            pago.Observaciones);
    }


    return partes.Count == 0
        ? null
        : string.Join(
            " · ",
            partes);
}


private static DateTime ConvertirFechaArgentina(
    DateTimeOffset fechaUtc)
{
    return TimeZoneInfo
        .ConvertTime(
            fechaUtc,
            ObtenerZonaHorariaArgentina())
        .DateTime;
}


protected static string ObtenerClaseSaldoCuenta(
    decimal saldo)
{
    if (saldo > 0)
        return "debt";

    if (saldo < 0)
        return "credit";

    return "zero";
}


protected static string ObtenerTextoSaldoCuenta(
    decimal saldo)
{
    if (saldo > 0)
    {
        return FormatearMoneda(
            saldo);
    }


    if (saldo < 0)
    {
        return
            $"{FormatearMoneda(Math.Abs(saldo))} a favor";
    }


    return FormatearMoneda(
        0);
}


protected sealed class MovimientoCuentaViewModel
{
    public DateTime Fecha { get; set; }

    public int Orden { get; set; }

    public string FechaTexto { get; set; } =
        string.Empty;

    public string Tipo { get; set; } =
        string.Empty;

    public string Concepto { get; set; } =
        string.Empty;

    public string? Detalle { get; set; }

    public decimal Debito { get; set; }

    public decimal Credito { get; set; }

    public decimal Saldo { get; set; }

    public MedioPago? MedioPago { get; set; }
}

    // =========================================================
    // ENUMS
    // =========================================================

    protected IReadOnlyList<MedioPago> MediosPago { get; } =
        Enum.GetValues<MedioPago>();


    // =========================================================
    // CALCULADOS
    // =========================================================

   protected List<BebePagoResumenDto> BebesFiltrados =>
    _bebes
        .Where(x => !_soloRevisar || _estadosOperativos.GetValueOrDefault(x.BebeId)?.Tipo == BebesNadadores.Web.Services.Finanzas.IndicadorFinancieroTipo.Revisar)
        .OrderByDescending(x =>
            x.Deuda > 0)

        .ThenBy(x =>
            x.NombreCompleto)

        .ToList();


    protected bool PuedeRegistrarPago
    {
        get
        {
            if (_procesandoPago)
                return false;


            if (_pagoForm.Importe <= 0)
                return false;


            if (_pagoForm.MedioPago ==
                    MedioPago.Efectivo
                &&
                (_cajaHoy is null ||
                 _cajaHoy.Estado !=
                    EstadoCaja.Abierta))
            {
                return false;
            }


            if (
                RequiereCuentaBancaria(
                    _pagoForm.MedioPago)
                &&
                !_pagoForm.CuentaBancariaId
                    .HasValue)
            {
                return false;
            }


            return true;
        }
    }


    // =========================================================
    // INIT
    // =========================================================

    /*
     * /pagos?bebe={id}: abre directo la cuenta de ese bebé
     * (lo usa el buscador de la barra superior).
     */
    [SupplyParameterFromQuery(Name = "bebe")]
    public Guid? BebeQuery { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _bebeSeleccionadoId ??=
            BebeQuery;

        await CargarTodoAsync();
    }


    protected override async Task OnParametersSetAsync()
    {
        // Otra búsqueda estando ya en Pagos: cambia el bebé.
        if (!_cargando &&
            BebeQuery.HasValue &&
            BebeQuery != _bebeSeleccionadoId)
        {
            await SeleccionarBebeAsync(
                BebeQuery.Value);
        }
    }


    protected async Task CargarTodoAsync()
    {
        try
        {
            _cargando =
                true;

            _error =
                null;


            var bebes =
                await PagoService
                    .ListarBebesAsync();


            _bebes =
                bebes.ToList();
            await CargarEstadosOperativosAsync();

            _cuentasBancarias =
                (await CuentaBancariaService
                    .ListarAsync(
                        incluirInactivas: false))
                    .Where(x =>
                        x.Activa)
                    .OrderBy(x =>
                        x.Banco)
                    .ThenBy(x =>
                        x.Nombre)
                    .ToList();


            _cajaHoy =
                await CajaService
                    .ObtenerHoyAsync();


            if (_bebeSeleccionadoId.HasValue)
            {
                await CargarCuentaAsync(
                    _bebeSeleccionadoId.Value);
            }
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


    // =========================================================
    // SELECCIÓN
    // =========================================================

    protected async Task SeleccionarBebeAsync(
        Guid bebeId)
    {
        if (_bebeSeleccionadoId ==
            bebeId)
        {
            return;
        }


        _bebeSeleccionadoId =
            bebeId;


        await CargarCuentaAsync(
            bebeId);
    }


    private async Task CargarCuentaAsync(
        Guid bebeId)
    {
        try
        {
            _cargandoCuenta =
                true;
            _estadoOperativo = null;

            _error =
                null;


            _cuenta =
                await PagoService
                    .ObtenerCuentaCorrienteAsync(
                        bebeId);


            if (_cuenta is null)
            {
                _error =
                    "No se encontró la cuenta corriente seleccionada.";
            }
            else await ActualizarEstadoOperativoAsync(bebeId);
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _cargandoCuenta =
                false;
        }
    }


    protected bool EsSeleccionado(
        Guid bebeId)
    {
        return _bebeSeleccionadoId ==
            bebeId;
    }


    protected void LimpiarBusqueda()
    {
        _buscar =
            string.Empty;
    }


    // =========================================================
    // ABRIR PAGO
    // =========================================================

    protected async Task AbrirRegistrarPagoAsync()
    {
        if (_cuenta is null)
            return;


        _errorPago =
            null;

        _preview =
            null;


        _cajaHoy =
            await CajaService
                .ObtenerHoyAsync();


        _pagoForm =
            new RegistrarPagoDto
            {
                BebeId =
                    _cuenta.BebeId,

                ResponsableId =
                    _cuenta.ResponsablePrincipalId,

                MedioPago =
                    MedioPago.Efectivo,

                /*
                 * Se completa con lo que debe (las cuotas ya tienen
                 * el descuento aplicado). La administrativa lo puede
                 * cambiar si paga otro importe.
                 */
                Importe =
                    ImporteSugerido.Importe,

                CuentaBancariaId =
                    null
            };


        _modalPago =
            true;


        // Muestra enseguida cómo se va a imputar.
        await ActualizarPreviewAsync();
    }


    /*
     * Importe que se propone al abrir "Registrar pago":
     * - la deuda (cuotas vencidas o del mes, con descuento), o
     * - si está al día, la próxima cuota pendiente (pago adelantado).
     */
    protected (decimal Importe, string? Texto) ImporteSugerido
    {
        get
        {
            if (_cuenta is null)
                return (0, null);

            if (_cuenta.Deuda > 0)
                return (_cuenta.Deuda, "deuda total");

            var proxima =
                _cuenta.Cuotas
                    .Where(x =>
                        x.Estado != EstadoCuota.Anulada
                        && x.Saldo > 0)
                    .OrderBy(x => x.Periodo)
                    .FirstOrDefault();

            return proxima is null
                ? (0, null)
                : (proxima.Saldo, $"cuota {FormatearPeriodo(proxima.Periodo)} (adelantada)");
        }
    }


    protected void CerrarModalPago()
    {
        if (_procesandoPago)
            return;


        _modalPago =
            false;

        _preview =
            null;

        _errorPago =
            null;
    }


    // =========================================================
    // IMPORTE
    // =========================================================

    protected async Task ImporteCambioAsync(
        ChangeEventArgs e)
    {
        var texto =
            e.Value?.ToString();


        if (
            decimal.TryParse(
                texto,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var importe)
            ||
            decimal.TryParse(
                texto,
                NumberStyles.Any,
                CultureInfo.GetCultureInfo("es-AR"),
                out importe))
        {
            _pagoForm.Importe =
                importe;
        }
        else
        {
            _pagoForm.Importe =
                0;
        }


        await ActualizarPreviewAsync();
    }


    protected async Task UsarImporteSugeridoAsync()
    {
        if (_cuenta is null)
            return;


        _pagoForm.Importe =
            ImporteSugerido.Importe;


        await ActualizarPreviewAsync();
    }


    private async Task ActualizarPreviewAsync()
    {
        if (
            _pagoForm.Importe <= 0
            ||
            _pagoForm.BebeId ==
                Guid.Empty)
        {
            _preview =
                null;

            return;
        }


        try
        {
            _cargandoPreview =
                true;

            _errorPago =
                null;


            _preview =
                await PagoService
                    .PrevisualizarAsync(
                        _pagoForm);
        }
        catch (Exception ex)
        {
            _errorPago =
                Errores.Mensaje(ex);

            _preview =
                null;
        }
        finally
        {
            _cargandoPreview =
                false;
        }
    }



// =========================================================
// BÚSQUEDA
// =========================================================

protected async Task BuscarCambioAsync(
    ChangeEventArgs e)
{
    _buscar =
        e.Value?.ToString()
        ??
        string.Empty;


    _busquedaCts?.Cancel();
    _busquedaCts?.Dispose();


    _busquedaCts =
        new CancellationTokenSource();


    var ct =
        _busquedaCts.Token;


    try
    {
        /*
         * Evita consultar PostgreSQL
         * por cada tecla instantáneamente.
         */
        await Task.Delay(
            300,
            ct);


        _buscando =
            true;


        var datos =
            await PagoService
                .ListarBebesAsync(
                    string.IsNullOrWhiteSpace(_buscar)
                        ? null
                        : _buscar.Trim(),
                    ct);


        _bebes =
            datos.ToList();
        await CargarEstadosOperativosAsync();
    }
    catch (OperationCanceledException)
    {
        // Se escribió otra tecla.
    }
    catch (Exception ex)
    {
        _error =
            Errores.Mensaje(ex);
    }
    finally
    {
        if (!ct.IsCancellationRequested)
        {
            _buscando =
                false;
        }
    }
}


protected async Task LimpiarBusquedaAsync()
{
    _busquedaCts?.Cancel();
    _busquedaCts?.Dispose();

    _busquedaCts =
        null;


    _buscar =
        string.Empty;


    try
    {
        _buscando =
            true;


        var datos =
            await PagoService
                .ListarBebesAsync();


        _bebes =
            datos.ToList();
        await CargarEstadosOperativosAsync();
    }
    catch (Exception ex)
    {
        _error =
            Errores.Mensaje(ex);
    }
    finally
    {
        _buscando =
            false;
    }
}
    // =========================================================
    // MEDIO DE PAGO
    // =========================================================

    protected async Task MedioPagoCambioAsync(
        ChangeEventArgs e)
    {
        if (
            Enum.TryParse<MedioPago>(
                e.Value?.ToString(),
                out var medio))
        {
            _pagoForm.MedioPago =
                medio;
        }


        _errorPago =
            null;


        if (_pagoForm.MedioPago ==
            MedioPago.Efectivo)
        {
            _pagoForm.CuentaBancariaId =
                null;


            _cajaHoy =
                await CajaService
                    .ObtenerHoyAsync();
        }
        else if (
            RequiereCuentaBancaria(
                _pagoForm.MedioPago))
        {
            if (
                !_pagoForm.CuentaBancariaId.HasValue
                &&
                _cuentasBancarias.Count == 1)
            {
                _pagoForm.CuentaBancariaId =
                    _cuentasBancarias[0].Id;
            }
        }
        else
        {
            _pagoForm.CuentaBancariaId =
                null;
        }
    }


    protected static bool RequiereCuentaBancaria(
        MedioPago medio)
    {
        return medio is
            MedioPago.Transferencia
            or MedioPago.Tarjeta
            or MedioPago.MercadoPago;
    }


    // =========================================================
    // REGISTRAR
    // =========================================================

    protected async Task RegistrarPagoAsync()
    {
        // Evita pagos duplicados por doble click en conexiones lentas.
        if (_cuenta is null || _procesandoPago)
            return;


        if (_pagoForm.Importe <= 0)
        {
            _errorPago =
                "El importe debe ser mayor a cero.";

            return;
        }


        if (_pagoForm.MedioPago ==
            MedioPago.Efectivo)
        {
            _cajaHoy =
                await CajaService
                    .ObtenerHoyAsync();


            if (_cajaHoy is null ||
                _cajaHoy.Estado !=
                    EstadoCaja.Abierta)
            {
                _errorPago =
                    "No hay una caja abierta para el día de hoy.";

                return;
            }
        }


        if (
            RequiereCuentaBancaria(
                _pagoForm.MedioPago)
            &&
            !_pagoForm.CuentaBancariaId
                .HasValue)
        {
            _errorPago =
                "Debe seleccionar la cuenta que recibió el pago.";

            return;
        }


        try
        {
            _procesandoPago =
                true;

            _errorPago =
                null;

            _error =
                null;


            var usuarioId =
                await ObtenerUsuarioIdAsync();


            var resultado =
                await PagoService
                    .RegistrarAsync(
                        _pagoForm,
                        usuarioId);


            _modalPago =
                false;

            _preview =
                null;


            _mensaje =
                resultado.SaldoAFavor > 0

                    ? $"Pago registrado correctamente. " +
                      $"Quedó un saldo a favor de " +
                      $"{FormatearMoneda(resultado.SaldoAFavor)}."

                    : "Pago registrado correctamente.";


            await RecargarDespuesDePagoAsync();
        }
        catch (Exception ex)
        {
            _errorPago =
                Errores.Mensaje(ex);
        }
        finally
        {
            _procesandoPago =
                false;
        }
    }


    // =========================================================
    // ANULACIONES
    // =========================================================

    private enum AccionAnulacion
    {
        AnularPago,
        AnularCuota,
        ReactivarCuota
    }


    /*
     * Una sola ventana (BnAnulacionModal) para las tres acciones con
     * motivo de esta pantalla: anular pago, anular cuota y
     * reactivar cuota.
     */
    private sealed record AnulacionPendiente(
        AccionAnulacion Accion,
        Guid Id,
        string Eyebrow,
        string Titulo,
        string TextoConfirmar,
        string Detalle,
        decimal Importe,
        string Aviso)
    {
        public bool EsReactivacion =>
            Accion == AccionAnulacion.ReactivarCuota;
    }


    private AnulacionPendiente? _anulacion;


    protected void AbrirAnularPago(
        PagoCuentaCorrienteDto pago)
    {
        _anulacion =
            new AnulacionPendiente(
                Accion: AccionAnulacion.AnularPago,
                Id: pago.Id,
                Eyebrow: "ANULAR PAGO · REVIERTE EL DINERO",
                Titulo: "Anular pago",
                TextoConfirmar: "Anular pago",
                Detalle:
                    $"{ObtenerMedioPagoTexto(pago.MedioPago)} · " +
                    $"{FormatearFechaHora(pago.FechaUtc)}",
                Importe: pago.Importe,
                Aviso: ObtenerAvisoAnulacionPago(pago));
    }


    protected void AbrirAnularCuota(
        CuotaCuentaCorrienteDto cuota)
    {
        _anulacion =
            new AnulacionPendiente(
                Accion: AccionAnulacion.AnularCuota,
                Id: cuota.Id,
                Eyebrow: "ANULAR CUOTA · NO MUEVE DINERO",
                Titulo: $"Anular cuota {FormatearPeriodo(cuota.Periodo)}",
                TextoConfirmar: "Anular cuota",
                Detalle: $"Cuota {FormatearPeriodo(cuota.Periodo)}",
                Importe: cuota.Importe,
                Aviso:
                    (cuota.ImportePagado > 0
                        ? "La cuota deja de cobrarse y no vuelve a generarse. Lo ya pagado " +
                          $"({FormatearMoneda(cuota.ImportePagado)}) queda como saldo a favor " +
                          "y se aplica a otras cuotas pendientes. "
                        : "La cuota deja de cobrarse y no vuelve a generarse. ") +
                    "No registra ningún movimiento en caja ni en banco. " +
                    "Si lo que querés es devolver o corregir un cobro, anulá el PAGO " +
                    "desde el Historial de pagos.");
    }


    protected void AbrirReactivarCuota(
        CuotaCuentaCorrienteDto cuota)
    {
        _anulacion =
            new AnulacionPendiente(
                Accion: AccionAnulacion.ReactivarCuota,
                Id: cuota.Id,
                Eyebrow: "REACTIVAR CUOTA · NO MUEVE DINERO",
                Titulo: $"Reactivar cuota {FormatearPeriodo(cuota.Periodo)}",
                TextoConfirmar: "Reactivar cuota",
                Detalle:
                    $"Anulada: {cuota.MotivoAnulacion ?? "sin motivo registrado"}",
                Importe: cuota.Importe,
                Aviso:
                    "La cuota vuelve a cobrarse: queda Pendiente o Vencida según su " +
                    "vencimiento. Si el bebé tiene saldo a favor, se aplica solo. " +
                    "No registra movimientos en caja ni en banco. " +
                    "Queda registrado quién la reactivó, cuándo y por qué.");
    }


    /*
     * Dice exactamente dónde se registra la reversa, para que no
     * se confunda con anular una cuota.
     */
    private string ObtenerAvisoAnulacionPago(
        PagoCuentaCorrienteDto pago)
    {
        var importe =
            FormatearMoneda(pago.Importe);

        var destino =
            pago.MedioPago == MedioPago.Efectivo
                ? $"Se registra un egreso de {importe} en la caja de hoy (tiene que estar abierta)."
                : pago.CuentaBancariaId.HasValue
                    ? $"Se registra un egreso de {importe} en {pago.CuentaBancaria ?? "la cuenta bancaria"}."
                    : "Este medio de pago no tiene caja ni cuenta asociada: no se registra movimiento de dinero.";

        return "Las cuotas que cancelaba vuelven a quedar pendientes. " + destino;
    }


    protected void CerrarAnulacion()
    {
        _anulacion = null;
    }


    protected async Task ConfirmarAnulacionAsync(
        string motivo)
    {
        if (_anulacion is null)
            return;

        var usuarioId =
            await ObtenerUsuarioIdAsync();

        switch (_anulacion.Accion)
        {
            case AccionAnulacion.AnularPago:
                await AnulacionService.AnularPagoAsync(
                    _anulacion.Id,
                    motivo,
                    usuarioId);

                _mensaje = "Pago anulado correctamente.";
                break;

            case AccionAnulacion.AnularCuota:
                await AnulacionService.AnularCuotaAsync(
                    _anulacion.Id,
                    motivo,
                    usuarioId);

                _mensaje = "Cuota anulada correctamente.";
                break;

            case AccionAnulacion.ReactivarCuota:
                await AnulacionService.ReactivarCuotaAsync(
                    _anulacion.Id,
                    motivo,
                    usuarioId);

                _mensaje = "Cuota reactivada correctamente.";
                break;
        }

        _error = null;

        await RecargarDespuesDePagoAsync();
    }


    private async Task RecargarDespuesDePagoAsync()
    {
        if (!_bebeSeleccionadoId.HasValue)
            return;


        var bebeId =
            _bebeSeleccionadoId.Value;


        var bebes =
            await PagoService
                .ListarBebesAsync();


        _bebes =
            bebes.ToList();


        _cuenta =
            await PagoService
                .ObtenerCuentaCorrienteAsync(
                    bebeId);


        await ActualizarEstadoOperativoAsync(bebeId);
        await CargarEstadosOperativosAsync();
        _cajaHoy =
            await CajaService
                .ObtenerHoyAsync();
    }


    // =========================================================
    // IDENTITY
    // =========================================================

    private async Task<Guid>
        ObtenerUsuarioIdAsync()
    {
        var estado =
            await AuthenticationStateProvider
                .GetAuthenticationStateAsync();


        var valor =
            estado.User
                .FindFirstValue(
                    ClaimTypes.NameIdentifier);


        if (!Guid.TryParse(
            valor,
            out var usuarioId))
        {
            throw new InvalidOperationException(
                "No se pudo determinar el usuario autenticado.");
        }


        return usuarioId;
    }


    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    protected void IrACaja()
    {
        Navigation.NavigateTo(
            "/caja");
    }


    protected void Volver()
    {
        Navigation.NavigateTo(
            "/");
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
        var fecha =
            periodo.ToDateTime(
                TimeOnly.MinValue);


        var cultura =
            CultureInfo.GetCultureInfo(
                "es-AR");


        var mes =
            cultura.DateTimeFormat
                .GetMonthName(
                    fecha.Month);


        if (string.IsNullOrWhiteSpace(
            mes))
        {
            return periodo.ToString(
                "MM/yyyy");
        }


        return
            $"{char.ToUpper(mes[0], cultura)}" +
            $"{mes[1..]} {fecha.Year}";
    }


    protected static string FormatearFechaHora(
        DateTimeOffset fechaUtc)
    {
        var argentina =
            TimeZoneInfo.ConvertTime(
                fechaUtc,
                ObtenerZonaHorariaArgentina());


        return argentina.ToString(
            "dd/MM/yyyy HH:mm");
    }


    protected static string ObtenerIniciales(
        string nombre)
    {
        var partes =
            nombre.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);


        if (partes.Length == 0)
            return "?";


        if (partes.Length == 1)
        {
            return partes[0][0]
                .ToString()
                .ToUpperInvariant();
        }


        return string.Concat(
                partes[0][0],
                partes[^1][0])
            .ToUpperInvariant();
    }


    protected static string ObtenerEstadoCuotaTexto(
        EstadoCuota estado)
    {
        return estado switch
        {
            EstadoCuota.Pendiente =>
                "Pendiente",

            EstadoCuota.Parcial =>
                "Parcial",

            EstadoCuota.Pagada =>
                "Pagada",

            EstadoCuota.Vencida =>
                "Vencida",

            EstadoCuota.Anulada =>
                "Anulada",

            _ =>
                estado.ToString()
        };
    }


    protected static string ObtenerClaseCuota(
        EstadoCuota estado)
    {
        return estado switch
        {
            EstadoCuota.Pagada =>
                "paid",

            EstadoCuota.Parcial =>
                "partial",

            EstadoCuota.Vencida =>
                "overdue",

            EstadoCuota.Anulada =>
                "cancelled",

            _ =>
                "pending"
        };
    }


    protected static string ObtenerEstiloProgreso(
        CuotaCuentaCorrienteDto cuota)
    {
        if (cuota.Importe <= 0)
        {
            return cuota.ImporteBonificado > 0 ? "width:100%;" : "width:0%;";
        }


        var porcentaje =
            Math.Clamp(
                cuota.ImportePagado /
                cuota.Importe *
                100m,
                0,
                100);


        return
            $"width:{porcentaje.ToString(
                "0.##",
                CultureInfo.InvariantCulture)}%;";
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


    protected static string ObtenerIconoMedioPago(
        MedioPago medio)
    {
        return medio switch
        {
            MedioPago.Efectivo =>
                Icons.Material.Filled.Payments,

            MedioPago.Transferencia =>
                Icons.Material.Filled.AccountBalance,

            MedioPago.Tarjeta =>
                Icons.Material.Filled.CreditCard,

            MedioPago.MercadoPago =>
                Icons.Material.Filled.AccountBalanceWallet,

            _ =>
                Icons.Material.Filled.ReceiptLong
        };
    }


    protected static string ObtenerClaseMedioPago(
        MedioPago medio)
    {
        return medio switch
        {
            MedioPago.Efectivo =>
                "cash",

            MedioPago.Transferencia =>
                "bank",

            MedioPago.Tarjeta =>
                "card",

            MedioPago.MercadoPago =>
                "wallet",

            _ =>
                "other"
        };
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