using System.Security.Claims;
using BebesNadadores.Application.Caja;
using BebesNadadores.Application.Caja.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using BebesNadadores.Domain.Common;
using System.Security.Claims;
namespace BebesNadadores.Web.Components.Pages.Finanzas;

public partial class Caja : ComponentBase
{
    // =========================================================
    // ESTADO
    // =========================================================

    protected CajaDetalleDto? _cajaHoy;

    protected List<CajaResumenDto> _historico =
        [];

    protected CajaDetalleDto? _detalleHistorico;

    protected CajaDetalleDto? _cajaParaCerrar;


    protected List<ConceptoCajaDto> _conceptos =
        [];

protected bool _drawerConceptos;

protected bool _modalConcepto;

protected bool _mostrarConceptosInactivos;

protected bool _procesandoConcepto;


protected Guid? _editandoConceptoId;


protected GuardarConceptoCajaDto _conceptoForm =
    CrearConceptoNuevo();


protected string? _errorConcepto;

protected string? _errorConceptoModal;

    protected AbrirCajaDto _abrirCaja =
        new();

    protected CerrarCajaDto _cerrarCaja =
        new();

    protected RegistrarMovimientoCajaDto _movimiento =
        new();


    /*
     * El Tipo ya NO viaja en RegistrarMovimientoCajaDto.
     * Lo usamos solamente para filtrar los conceptos
     * disponibles en el modal.
     */
    protected TipoMovimientoCaja _tipoMovimientoSeleccionado =
        TipoMovimientoCaja.Ingreso;


    protected bool _cargando =
        true;

    protected bool _procesando;


    protected bool _modalApertura;

    protected bool _modalMovimiento;

    protected bool _modalCierre;


    protected string? _error;

    protected string? _mensaje;


    protected DateOnly _hoy;


    // =========================================================
    // CALCULADOS
    // =========================================================

protected IReadOnlyList<ConceptoCajaDto>
    ConceptosIngresoVisibles =>
        _conceptos
            .Where(x =>
                x.Tipo ==
                    TipoMovimientoCaja.Ingreso
                &&
                (_mostrarConceptosInactivos ||
                 x.Activo))
            .OrderBy(x =>
                x.Orden)
            .ThenBy(x =>
                x.Nombre)
            .ToList();


protected IReadOnlyList<ConceptoCajaDto>
    ConceptosEgresoVisibles =>
        _conceptos
            .Where(x =>
                x.Tipo ==
                    TipoMovimientoCaja.Egreso
                &&
                (_mostrarConceptosInactivos ||
                 x.Activo))
            .OrderBy(x =>
                x.Orden)
            .ThenBy(x =>
                x.Nombre)
            .ToList();

    protected decimal DiferenciaCierre =>
        _cajaParaCerrar is null
            ? 0
            : _cerrarCaja.SaldoFinalDeclarado
              -
              _cajaParaCerrar.SaldoEsperado;


    protected IReadOnlyList<ConceptoCajaDto>
        ConceptosDisponibles =>
            _conceptos
                .Where(x =>
                    x.Activo
                    &&
                    x.Tipo ==
                        _tipoMovimientoSeleccionado)
                .OrderBy(x =>
                    x.Orden)
                .ThenBy(x =>
                    x.Nombre)
                .ToList();


    protected ConceptoCajaDto?
        ConceptoSeleccionado =>
            _movimiento.ConceptoCajaId ==
                Guid.Empty
                    ? null
                    : _conceptos
                        .FirstOrDefault(x =>
                            x.Id ==
                                _movimiento.ConceptoCajaId);


    // =========================================================
    // INIT
    // =========================================================

    protected override async Task OnInitializedAsync()
    {
        _hoy =
            ObtenerFechaArgentina();


        await CargarTodoAsync();
    }


 protected async Task CargarTodoAsync(
    bool preservarMensajes = false)
{
    try
    {
        _cargando =
            true;

        if (!preservarMensajes)
        {
            LimpiarMensajes();
        }


        await CargarConceptosAsync();


        _cajaHoy =
            await CajaService
                .ObtenerHoyAsync();


        await CargarHistoricoAsync();
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

   private async Task CargarConceptosAsync()
{
    await ConceptoCajaService
        .AsegurarConceptosSistemaAsync();


    var datos =
        await ConceptoCajaService
            .ListarAsync(
                incluirInactivos: true);


    _conceptos =
        datos.ToList();
}


    protected async Task CargarHistoricoAsync()
    {
        try
        {
            var datos =
                await CajaService
                    .ListarHistoricoAsync(
                        cantidad: 60);


            _historico =
                datos
                    .OrderByDescending(x =>
                        x.FechaOperativa)
                    .ToList();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    // =========================================================
    // APERTURA
    // =========================================================

    protected void AbrirModalApertura()
    {
        LimpiarMensajes();


        _abrirCaja =
            new AbrirCajaDto
            {
                SaldoInicial =
                    0
            };


        _modalApertura =
            true;
    }


    protected void CerrarModalApertura()
    {
        if (_procesando)
            return;


        _modalApertura =
            false;
    }


 protected async Task AbrirCajaAsync()
{
    try
    {
        _procesando =
            true;

        LimpiarMensajes();


        // =====================================================
        // USUARIO ACTUAL
        // =====================================================

        var authState =
            await AuthenticationStateProvider
                .GetAuthenticationStateAsync();

        var usuarioIdTexto =
            authState.User
                .FindFirst(
                    ClaimTypes.NameIdentifier)?
                .Value;


        if (!Guid.TryParse(
            usuarioIdTexto,
            out var usuarioId))
        {
            throw new InvalidOperationException(
                "No se pudo identificar al usuario que abre la caja.");
        }


        // =====================================================
        // ABRIR CAJA
        // =====================================================

        await CajaService
            .AbrirAsync(
                _abrirCaja,
                usuarioId);


        _modalApertura =
            false;

        _mensaje =
            "La caja fue abierta correctamente.";


        await CargarTodoAsync(
            preservarMensajes: true);
    }
    catch (Exception ex)
    {
        /*
         * Cerramos el modal para que el mensaje de error
         * que está en la pantalla principal quede visible.
         */
        _modalApertura =
            false;

        _error =
            Errores.Mensaje(ex);


        /*
         * Si otro usuario ya había abierto la caja,
         * refrescamos el estado de la pantalla sin borrar
         * el mensaje que acabamos de mostrar.
         */
        await CargarTodoAsync(
            preservarMensajes: true);
    }
    finally
    {
        _procesando =
            false;
    }
}


    // =========================================================
    // MOVIMIENTO MANUAL
    // =========================================================

    protected void AbrirMovimiento(
        TipoMovimientoCaja tipo)
    {
        if (_cajaHoy is null ||
            _cajaHoy.Estado !=
                EstadoCaja.Abierta)
        {
            return;
        }


        LimpiarMensajes();


        _tipoMovimientoSeleccionado =
            tipo;


        var primerConcepto =
            _conceptos
                .Where(x =>
                    x.Activo
                    &&
                    x.Tipo ==
                        tipo)
                .OrderBy(x =>
                    x.Orden)
                .ThenBy(x =>
                    x.Nombre)
                .FirstOrDefault();


        _movimiento =
            new RegistrarMovimientoCajaDto
            {
                ConceptoCajaId =
                    primerConcepto?.Id
                    ??
                    Guid.Empty
            };


        _modalMovimiento =
            true;
    }


    protected void CerrarModalMovimiento()
    {
        if (_procesando)
            return;


        _modalMovimiento =
            false;
    }


    protected async Task GuardarMovimientoAsync()
    {
        if (_cajaHoy is null)
            return;


        if (_movimiento.ConceptoCajaId ==
            Guid.Empty)
        {
            _error =
                "Debe seleccionar un concepto.";

            return;
        }


        if (_movimiento.Importe <= 0)
        {
            _error =
                "El importe debe ser mayor a cero.";

            return;
        }


        try
        {
            _procesando =
                true;

            _error =
                null;


            await CajaService
                .RegistrarMovimientoManualAsync(
                    _cajaHoy.Id,
                    _movimiento);


            _modalMovimiento =
                false;


            _mensaje =
                _tipoMovimientoSeleccionado ==
                    TipoMovimientoCaja.Ingreso

                    ? "Ingreso registrado correctamente."
                    : "Egreso registrado correctamente.";


            await RecargarCajaAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _procesando =
                false;
        }
    }


    // =========================================================
    // CIERRE
    // =========================================================

    protected void AbrirCerrarCaja(
        CajaDetalleDto caja)
    {
        LimpiarMensajes();


        _cajaParaCerrar =
            caja;


        _cerrarCaja =
            new CerrarCajaDto
            {
                SaldoFinalDeclarado =
                    caja.SaldoEsperado
            };


        _modalCierre =
            true;
    }


    protected void CerrarModalCierre()
    {
        if (_procesando)
            return;


        _modalCierre =
            false;

        _cajaParaCerrar =
            null;
    }


    protected async Task CerrarCajaAsync()
    {
        if (_cajaParaCerrar is null)
            return;


        if (_cerrarCaja.SaldoFinalDeclarado < 0)
        {
            _error =
                "El efectivo contado no puede ser negativo.";

            return;
        }


        try
        {
            _procesando =
                true;

            _error =
                null;


            var usuarioId =
                await ObtenerUsuarioIdAsync();


            var resultado =
                await CajaService
                    .CerrarAsync(
                        _cajaParaCerrar.Id,
                        _cerrarCaja,
                        usuarioId);


            _modalCierre =
                false;

            _cajaParaCerrar =
                null;


            _mensaje =
                resultado.Diferencia == 0
                    ? "Caja cerrada. El efectivo coincide con el sistema."
                    : $"Caja cerrada con una diferencia de " +
                      $"{FormatearDiferencia(resultado.Diferencia)}.";


            await RecargarCajaAsync();


            if (_detalleHistorico is not null)
            {
                _detalleHistorico =
                    await CajaService
                        .ObtenerAsync(
                            _detalleHistorico.Id);
            }
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _procesando =
                false;
        }
    }


    // =========================================================
    // HISTÓRICO
    // =========================================================

    protected async Task AbrirDetalleHistoricoAsync(
        Guid cajaId)
    {
        try
        {
            _error =
                null;


            _detalleHistorico =
                await CajaService
                    .ObtenerAsync(
                        cajaId);


            if (_detalleHistorico is null)
            {
                _error =
                    "La caja seleccionada no existe.";
            }
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    protected void CerrarDetalleHistorico()
    {
        _detalleHistorico =
            null;
    }


    protected void CerrarCajaHistorica()
    {
        if (_detalleHistorico is null)
            return;


        var caja =
            _detalleHistorico;


        _detalleHistorico =
            null;


        AbrirCerrarCaja(
            caja);
    }


// =========================================================
// CONCEPTOS DE CAJA
// =========================================================

protected void AbrirConceptosCaja()
{
    _errorConcepto =
        null;

    _errorConceptoModal =
        null;

    _drawerConceptos =
        true;
}


protected void CerrarConceptosCaja()
{
    if (_procesandoConcepto)
        return;


    _drawerConceptos =
        false;
}


// =========================================================
// NUEVO CONCEPTO
// =========================================================

protected void NuevoConceptoCaja()
{
    _editandoConceptoId =
        null;


    _conceptoForm =
        CrearConceptoNuevo();


    _errorConceptoModal =
        null;


    _modalConcepto =
        true;
}


// =========================================================
// EDITAR CONCEPTO
// =========================================================

protected void EditarConceptoCaja(
    ConceptoCajaDto concepto)
{
    _editandoConceptoId =
        concepto.Id;


    _conceptoForm =
        new GuardarConceptoCajaDto
        {
            Nombre =
                concepto.Nombre,

            Tipo =
                concepto.Tipo,

            Activo =
                concepto.Activo,

            Orden =
                concepto.Orden
        };


    _errorConceptoModal =
        null;


    _modalConcepto =
        true;
}


// =========================================================
// CERRAR MODAL
// =========================================================

protected void CerrarModalConcepto()
{
    if (_procesandoConcepto)
        return;


    _modalConcepto =
        false;

    _editandoConceptoId =
        null;

    _errorConceptoModal =
        null;
}


// =========================================================
// GUARDAR
// =========================================================

protected async Task GuardarConceptoCajaAsync()
{
    if (string.IsNullOrWhiteSpace(
        _conceptoForm.Nombre))
    {
        _errorConceptoModal =
            "Debe indicar el nombre del concepto.";

        return;
    }


    if (_conceptoForm.Orden < 0)
    {
        _errorConceptoModal =
            "El orden no puede ser negativo.";

        return;
    }


    try
    {
        _procesandoConcepto =
            true;

        _errorConceptoModal =
            null;

        _errorConcepto =
            null;


        if (_editandoConceptoId.HasValue)
        {
            await ConceptoCajaService
                .ActualizarAsync(
                    _editandoConceptoId.Value,
                    _conceptoForm);


            _mensaje =
                "Concepto actualizado correctamente.";
        }
        else
        {
            await ConceptoCajaService
                .CrearAsync(
                    _conceptoForm);


            _mensaje =
                "Concepto creado correctamente.";
        }


        await CargarConceptosAsync();


        _modalConcepto =
            false;

        _editandoConceptoId =
            null;
    }
    catch (Exception ex)
    {
        _errorConceptoModal =
            Errores.Mensaje(ex);
    }
    finally
    {
        _procesandoConcepto =
            false;
    }
}


protected static bool EsConceptoSistema(
    ConceptoCajaDto concepto)
{
    return concepto.Id ==
        ConceptosCajaSistema
            .CuotaNatatorioMensualId;
}

// =========================================================
// ACTIVAR / DESACTIVAR
// =========================================================


protected async Task CambiarEstadoConceptoAsync(
    ConceptoCajaDto concepto)
{
    try
    {
        _procesandoConcepto =
            true;

        _errorConcepto =
            null;


        await ConceptoCajaService
            .CambiarEstadoAsync(
                concepto.Id,
                !concepto.Activo);


        await CargarConceptosAsync();


        _mensaje =
            concepto.Activo
                ? "Concepto desactivado correctamente."
                : "Concepto activado correctamente.";
    }
    catch (Exception ex)
    {
        _errorConcepto =
            Errores.Mensaje(ex);
    }
    finally
    {
        _procesandoConcepto =
            false;
    }
}


// =========================================================
// FORMULARIO NUEVO
// =========================================================

private static GuardarConceptoCajaDto
    CrearConceptoNuevo()
{
    return new GuardarConceptoCajaDto
    {
        Nombre =
            string.Empty,

        Tipo =
            TipoMovimientoCaja.Egreso,

        Activo =
            true,

        Orden =
            0
    };
}

protected void CrearConceptoDesdeMovimiento()
{
    _modalMovimiento =
        false;


    _editandoConceptoId =
        null;


    _conceptoForm =
        new GuardarConceptoCajaDto
        {
            Tipo =
                _tipoMovimientoSeleccionado,

            Activo =
                true,

            Orden =
                0
        };


    _errorConceptoModal =
        null;

    _drawerConceptos =
        true;

    _modalConcepto =
        true;
}

    // =========================================================
    // RECARGA
    // =========================================================

    // =========================================================
    // REABRIR CAJA DE HOY
    // =========================================================

    protected bool _mostrarReapertura;


    /*
     * Solo la caja de HOY y solo si está cerrada.
     * (El servicio vuelve a validar la fecha.)
     */
    protected bool PuedeReabrirCajaHoy =>
        _cajaHoy is not null
        && _cajaHoy.Estado == EstadoCaja.Cerrada
        && _cajaHoy.FechaOperativa == _hoy;


    protected void AbrirReapertura()
    {
        _mostrarReapertura = true;
    }


    protected void CerrarReapertura()
    {
        _mostrarReapertura = false;
    }


    protected async Task ConfirmarReaperturaAsync(
        string motivo)
    {
        if (_cajaHoy is null)
            return;

        var usuarioId =
            await ObtenerUsuarioIdAsync();

        await CajaService.ReabrirAsync(
            _cajaHoy.Id,
            motivo,
            usuarioId);

        _mensaje = "La caja de hoy fue reabierta.";
        _error = null;

        await RecargarCajaAsync();
    }


    // =========================================================
    // ANULAR MOVIMIENTO MANUAL
    // =========================================================

    protected MovimientoCajaDto? _movimientoAAnular;


    /*
     * Solo movimientos cargados a mano, de la caja de hoy abierta.
     * Los cobros se anulan desde Pagos y los pagos a proveedor
     * desde la pantalla del proveedor.
     */
    protected bool PuedeAnularMovimiento(
        MovimientoCajaDto movimiento)
    {
        return _cajaHoy is not null
            && _cajaHoy.Estado == EstadoCaja.Abierta
            && !movimiento.Anulado
            && !movimiento.EsAnulacion
            && !movimiento.PagoId.HasValue
            && !movimiento.PagoProveedorId.HasValue;
    }


    protected void AbrirAnularMovimiento(
        MovimientoCajaDto movimiento)
    {
        _movimientoAAnular = movimiento;
    }


    protected void CerrarAnulacionMovimiento()
    {
        _movimientoAAnular = null;
    }


    protected async Task ConfirmarAnulacionMovimientoAsync(
        string motivo)
    {
        if (_movimientoAAnular is null)
            return;

        var usuarioId =
            await ObtenerUsuarioIdAsync();

        await AnulacionService.AnularMovimientoCajaAsync(
            _movimientoAAnular.Id,
            motivo,
            usuarioId);

        _mensaje = "Movimiento anulado correctamente.";
        _error = null;

        await RecargarCajaAsync();
    }


    private async Task RecargarCajaAsync()
    {
        _cajaHoy =
            await CajaService
                .ObtenerHoyAsync();


        await CargarHistoricoAsync();
    }


    // =========================================================
    // USUARIO
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
    // TEXTO
    // =========================================================

    protected static string
        ObtenerEstadoTexto(
            EstadoCaja estado)
    {
        return estado switch
        {
            EstadoCaja.Abierta =>
                "Abierta",

            EstadoCaja.Cerrada =>
                "Cerrada",

            _ =>
                estado.ToString()
        };
    }


    protected static string
        ObtenerClaseEstado(
            EstadoCaja estado)
    {
        return estado ==
            EstadoCaja.Abierta
                ? "open"
                : "closed";
    }


    protected static string
        ObtenerTipoMovimientoTexto(
            TipoMovimientoCaja tipo)
    {
        return tipo switch
        {
            TipoMovimientoCaja.Ingreso =>
                "Ingreso",

            TipoMovimientoCaja.Egreso =>
                "Egreso",

            _ =>
                tipo.ToString()
        };
    }


    protected static string
        ObtenerClaseDiferencia(
            decimal? diferencia)
    {
        if (!diferencia.HasValue ||
            diferencia.Value == 0)
        {
            return "difference-zero";
        }


        return diferencia.Value > 0
            ? "difference-positive"
            : "difference-negative";
    }


    protected static string
        FormatearDiferencia(
            decimal? diferencia)
    {
        if (!diferencia.HasValue)
            return "—";


        if (diferencia.Value == 0)
            return "$ 0,00";


        var signo =
            diferencia.Value > 0
                ? "+"
                : "-";


        return signo +
               FormatearMoneda(
                   Math.Abs(
                       diferencia.Value));
    }


    protected static string
        FormatearMoneda(
            decimal valor)
    {
        return valor.ToString(
            "C2",
            System.Globalization.CultureInfo
                .GetCultureInfo("es-AR"));
    }


    protected static string
        FormatearFechaOperativa(
            DateOnly fecha)
    {
        return fecha.ToString(
            "dd/MM/yyyy");
    }


    protected static string
        FormatearHora(
            DateTimeOffset fechaUtc)
    {
        var zona =
            ObtenerZonaHorariaArgentina();


        var argentina =
            TimeZoneInfo.ConvertTime(
                fechaUtc,
                zona);


        return argentina.ToString(
            "HH:mm");
    }


    // =========================================================
    // FECHA ARGENTINA
    // =========================================================

    private static DateOnly
        ObtenerFechaArgentina()
    {
        var argentina =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                ObtenerZonaHorariaArgentina());


        return DateOnly.FromDateTime(
            argentina.DateTime);
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


    // =========================================================
    // GENERAL
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo("/");
    }


    private void LimpiarMensajes()
    {
        _error =
            null;

        _mensaje =
            null;
    }
}