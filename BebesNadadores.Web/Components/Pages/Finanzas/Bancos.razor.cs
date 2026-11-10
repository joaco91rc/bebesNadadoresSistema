using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Finanzas;

public partial class Bancos : ComponentBase
{
    // =========================================================
    // ESTADO GENERAL
    // =========================================================

    protected List<BancoDto> _bancos =
        [];

    protected List<CuentaBancariaDto> _cuentas =
        [];

    protected Dictionary<Guid, decimal> _saldos =
        [];

    // Número de ingresos por pagos de alumnos registrados durante el mes actual.
    protected Dictionary<Guid, int> _transferenciasMes = [];
    protected const int UmbralOperativoTransferencias = 45;
    protected int ContarTransferenciasMes(Guid cuentaId) =>
        _transferenciasMes.GetValueOrDefault(cuentaId);
    protected bool AlcanzoUmbral(Guid cuentaId) =>
        ContarTransferenciasMes(cuentaId) >= UmbralOperativoTransferencias;


    protected List<MovimientoBancarioDto> _movimientos =
        [];


    protected bool _cargando =
        true;

    protected bool _guardando;

    protected bool _cargandoMovimientos;


    protected string? _error;

    protected string? _mensaje;


    protected string _busqueda =
        string.Empty;

    protected bool _mostrarInactivas;


    // =========================================================
    // MODAL BANCO
    // =========================================================

    protected bool _modalBancoAbierto;

    protected Guid? _bancoEditandoId;

    protected GuardarBancoDto _formBanco =
        new();


    // =========================================================
    // MODAL CUENTA
    // =========================================================

    protected bool _modalCuentaAbierto;

    protected Guid? _cuentaEditandoId;

    protected GuardarCuentaBancariaDto _formCuenta =
        new();


    // =========================================================
    // MOVIMIENTOS
    // =========================================================

    protected CuentaBancariaDto? _cuentaDetalle;

    protected DateOnly _movDesde;

    protected DateOnly _movHasta;


    // =========================================================
    // CALCULADOS
    // =========================================================

    protected decimal SaldoTotal =>
        _cuentas
            .Where(x =>
                x.Activa)
            .Sum(x =>
                ObtenerSaldo(
                    x.Id));


    protected IEnumerable<CuentaBancariaDto>
        CuentasFiltradas
    {
        get
        {
            IEnumerable<CuentaBancariaDto> query =
                _cuentas;


            if (!_mostrarInactivas)
            {
                query =
                    query.Where(x =>
                        x.Activa);
            }


            if (!string.IsNullOrWhiteSpace(
                _busqueda))
            {
                var texto =
                    _busqueda
                        .Trim()
                        .ToLowerInvariant();


                query =
                    query.Where(x =>
                        x.Nombre
                            .ToLowerInvariant()
                            .Contains(texto)
                        ||
                        x.Banco
                            .ToLowerInvariant()
                            .Contains(texto)
                        ||
                        (
                            x.Alias is not null
                            &&
                            x.Alias
                                .ToLowerInvariant()
                                .Contains(texto)
                        )
                        ||
                        x.Titular
                            .ToLowerInvariant()
                            .Contains(texto));
            }


            return query
                .OrderByDescending(x =>
                    x.Activa)
                .ThenBy(x =>
                    x.Banco)
                .ThenBy(x =>
                    x.Nombre);
        }
    }


    protected decimal IngresosPeriodo =>
        _movimientos
            .Where(x =>
                x.Tipo ==
                    TipoMovimientoBancario.Ingreso)
            .Sum(x =>
                x.Importe);


    protected decimal EgresosPeriodo =>
        _movimientos
            .Where(x =>
                x.Tipo ==
                    TipoMovimientoBancario.Egreso)
            .Sum(x =>
                x.Importe);


    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    protected override async Task
        OnInitializedAsync()
    {
        await CargarAsync();
    }


    // =========================================================
    // CARGA
    // =========================================================

    private async Task CargarAsync()
    {
        try
        {
            _cargando =
                true;

            _error =
                null;


            _bancos =
                (await BancoService
                    .ListarAsync(
                        incluirInactivos: true))
                .ToList();


            _cuentas =
                (await CuentaBancariaService
                    .ListarAsync(
                        incluirInactivas: true))
                .ToList();


            var tareasSaldo =
                _cuentas
                    .Select(
                        async cuenta =>
                            new
                            {
                                cuenta.Id,

                                Saldo =
                                    await MovimientoBancarioService
                                        .ObtenerSaldoAsync(
                                            cuenta.Id)
                            })
                    .ToList();


            var saldos =
                await Task.WhenAll(
                    tareasSaldo);


            _saldos =
                saldos.ToDictionary(
                    x => x.Id,
                    x => x.Saldo);

            // Consultamos el mes calendario local, no los últimos 30 días.
            var inicioMes = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            var finMes = inicioMes.AddMonths(1).AddDays(-1);
            var conteos = await Task.WhenAll(_cuentas.Select(async cuenta =>
            {
                var movimientos = await MovimientoBancarioService.ListarAsync(
                    cuenta.Id, inicioMes, finMes);
                return (cuenta.Id, Cantidad: movimientos.Count(x =>
                    x.Tipo == TipoMovimientoBancario.Ingreso &&
                    x.Origen == OrigenMovimientoBancario.PagoBebe));
            }));
            _transferenciasMes = conteos.ToDictionary(x => x.Id, x => x.Cantidad);
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
    // BANCO
    // =========================================================

    protected void AbrirNuevoBanco()
    {
        LimpiarMensajes();


        _bancoEditandoId =
            null;


        _formBanco =
            new GuardarBancoDto
            {
                Activo =
                    true
            };


        _modalBancoAbierto =
            true;
    }


    protected void EditarBanco(
        BancoDto banco)
    {
        LimpiarMensajes();


        _bancoEditandoId =
            banco.Id;


        _formBanco =
            new GuardarBancoDto
            {
                Nombre =
                    banco.Nombre,

                Activo =
                    banco.Activo
            };


        _modalBancoAbierto =
            true;
    }


    protected void CerrarModalBanco()
    {
        if (_guardando)
            return;


        _modalBancoAbierto =
            false;
    }


    protected async Task GuardarBancoAsync()
    {
        if (string.IsNullOrWhiteSpace(
            _formBanco.Nombre))
        {
            _error =
                "Debe indicar el nombre del banco.";

            return;
        }


        try
        {
            _guardando =
                true;

            _error =
                null;


            if (_bancoEditandoId.HasValue)
            {
                await BancoService
                    .ActualizarAsync(
                        _bancoEditandoId.Value,
                        _formBanco);


                _mensaje =
                    "Banco actualizado correctamente.";
            }
            else
            {
                await BancoService
                    .CrearAsync(
                        _formBanco);


                _mensaje =
                    "Banco creado correctamente.";
            }


            _modalBancoAbierto =
                false;


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _guardando =
                false;
        }
    }


    protected async Task CambiarEstadoBancoAsync(
        BancoDto banco)
    {
        try
        {
            LimpiarMensajes();


            await BancoService
                .CambiarEstadoAsync(
                    banco.Id,
                    !banco.Activo);


            _mensaje =
                banco.Activo
                    ? "Banco desactivado."
                    : "Banco activado.";


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    // =========================================================
    // CUENTA
    // =========================================================

    protected void AbrirNuevaCuenta()
    {
        LimpiarMensajes();


        var primerBanco =
            _bancos
                .Where(x =>
                    x.Activo)
                .OrderBy(x =>
                    x.Nombre)
                .FirstOrDefault();


        if (primerBanco is null)
        {
            _error =
                "Primero debe crear al menos un banco activo.";

            return;
        }


        _cuentaEditandoId =
            null;


        _formCuenta =
            new GuardarCuentaBancariaDto
            {
                BancoId =
                    primerBanco.Id,

                Tipo =
                    TipoCuentaBancaria.CajaAhorro,

                Moneda =
                    "ARS",

                SaldoInicial =
                    0,

                Activa =
                    true
            };


        _modalCuentaAbierto =
            true;
    }


    protected async Task EditarCuentaAsync(
        CuentaBancariaDto cuenta)
    {
        try
        {
            LimpiarMensajes();


            var existente =
                await CuentaBancariaService
                    .ObtenerAsync(
                        cuenta.Id);


            if (existente is null)
            {
                _error =
                    "La cuenta indicada ya no existe.";

                return;
            }


            _cuentaEditandoId =
                existente.Id;


            _formCuenta =
                new GuardarCuentaBancariaDto
                {
                    BancoId =
                        existente.BancoId,

                    Nombre =
                        existente.Nombre,

                    Tipo =
                        existente.Tipo,

                    NumeroCuenta =
                        existente.NumeroCuenta,

                    CbuCvu =
                        existente.CbuCvu,

                    Alias =
                        existente.Alias,

                    Titular =
                        existente.Titular,

                    Moneda =
                        existente.Moneda,

                    SaldoInicial =
                        existente.SaldoInicial,

                    Activa =
                        existente.Activa
                };


            _modalCuentaAbierto =
                true;
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    protected void CerrarModalCuenta()
    {
        if (_guardando)
            return;


        _modalCuentaAbierto =
            false;
    }


    protected async Task GuardarCuentaAsync()
    {
        if (_formCuenta.BancoId ==
            Guid.Empty)
        {
            _error =
                "Debe seleccionar un banco.";

            return;
        }


        if (string.IsNullOrWhiteSpace(
            _formCuenta.Nombre))
        {
            _error =
                "Debe indicar un nombre para la cuenta.";

            return;
        }


        if (string.IsNullOrWhiteSpace(
            _formCuenta.Titular))
        {
            _error =
                "Debe indicar el titular de la cuenta.";

            return;
        }


        if (_formCuenta.SaldoInicial < 0)
        {
            _error =
                "El saldo inicial no puede ser negativo.";

            return;
        }


        try
        {
            _guardando =
                true;

            _error =
                null;


            if (_cuentaEditandoId.HasValue)
            {
                await CuentaBancariaService
                    .ActualizarAsync(
                        _cuentaEditandoId.Value,
                        _formCuenta);


                _mensaje =
                    "Cuenta actualizada correctamente.";
            }
            else
            {
                await CuentaBancariaService
                    .CrearAsync(
                        _formCuenta);


                _mensaje =
                    "Cuenta creada correctamente.";
            }


            _modalCuentaAbierto =
                false;


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _guardando =
                false;
        }
    }


    protected async Task CambiarEstadoCuentaAsync(
        CuentaBancariaDto cuenta)
    {
        try
        {
            LimpiarMensajes();


            await CuentaBancariaService
                .CambiarEstadoAsync(
                    cuenta.Id,
                    !cuenta.Activa);


            _mensaje =
                cuenta.Activa
                    ? "Cuenta desactivada."
                    : "Cuenta activada.";


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
    }


    // =========================================================
    // MOVIMIENTOS
    // =========================================================

   


    protected async Task CargarMovimientosAsync()
    {
        if (_cuentaDetalle is null)
            return;


        if (_movHasta < _movDesde)
        {
            _error =
                "La fecha hasta no puede ser anterior a la fecha desde.";

            return;
        }


        try
        {
            _cargandoMovimientos =
                true;

            _error =
                null;


            _movimientos =
                (await MovimientoBancarioService
                    .ListarAsync(
                        _cuentaDetalle.Id,
                        _movDesde,
                        _movHasta))
                .OrderByDescending(x =>
                    x.FechaUtc)
                .ToList();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _cargandoMovimientos =
                false;
        }
    }


    protected void CerrarMovimientos()
    {
        _cuentaDetalle =
            null;

        _movimientos.Clear();
    }


    // =========================================================
    // HELPERS
    // =========================================================

    protected decimal ObtenerSaldo(
        Guid cuentaId)
    {
        return _saldos.TryGetValue(
            cuentaId,
            out var saldo)

            ? saldo
            : 0m;
    }


    protected static string
        ObtenerTipoCuentaTexto(
            TipoCuentaBancaria tipo)
    {
        return tipo switch
        {
            TipoCuentaBancaria.CajaAhorro =>
                "Caja de ahorro",

            TipoCuentaBancaria.CuentaCorriente =>
                "Cuenta corriente",

            TipoCuentaBancaria.BilleteraVirtual =>
                "Billetera virtual",

            TipoCuentaBancaria.Otra =>
                "Otra",

            _ =>
                tipo.ToString()
        };
    }


    protected static string
        ObtenerOrigenTexto(
            OrigenMovimientoBancario origen)
    {
        return origen switch
        {
            OrigenMovimientoBancario.PagoBebe =>
                "Pago de familia",

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


    protected static string FormatearMoneda(
        decimal valor)
    {
        return valor.ToString(
            "C2",
            System.Globalization.CultureInfo
                .GetCultureInfo("es-AR"));
    }


    protected static string FormatearFecha(
        DateTimeOffset fecha)
    {
        var argentina =
            fecha.ToOffset(
                TimeSpan.FromHours(-3));


        return argentina.ToString(
            "dd/MM/yyyy HH:mm");
    }


    protected void Volver()
    {
        Navigation.NavigateTo(
            "/");
    }


    private void LimpiarMensajes()
    {
        _error =
            null;

        _mensaje =
            null;
    }

    protected void VerMovimientos(
    Guid cuentaId)
{
    Navigation.NavigateTo(
        $"/bancos/{cuentaId}/movimientos");
}
}