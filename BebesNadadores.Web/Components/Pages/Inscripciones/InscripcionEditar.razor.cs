using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Inscripciones.Dtos;
using BebesNadadores.Application.Niveles;
using BebesNadadores.Application.Niveles.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Inscripciones;

public partial class InscripcionEditar : ComponentBase
{
    // =========================================================
    // SERVICIOS
    // =========================================================

    [Inject]
    protected IBebeLegajoService LegajoService { get; set; }
        = null!;

    [Inject]
    protected INivelService NivelService { get; set; }
        = null!;

    [Inject]
    protected IInscripcionConsultaService ConsultaService { get; set; }
        = null!;

    [Inject]
    protected IInscripcionService InscripcionService { get; set; }
        = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; }
        = null!;


    // =========================================================
    // PARÁMETRO
    // =========================================================

    [Parameter]
    public Guid BebeId { get; set; }


    // =========================================================
    // ESTADO
    // =========================================================

    protected readonly DateOnly _hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    protected BebeLegajoDto? _legajo;


    protected CambiarInscripcionDto _form =
        new();


    protected List<NivelDto> _niveles = [];


    protected List<PlanDisponibleDto> _planes = [];


    protected List<TurnoDisponibleDto>
        _turnosDisponibles = [];


    protected bool _cargando =
        true;


    protected bool _cargandoTurnos;


    protected bool _guardando;


    protected string? _error;

    protected string? _errorCarga;


    // =========================================================
    // CALCULADOS
    // =========================================================
protected bool EsPrimeraInscripcion =>
    _legajo?.InscripcionActual is null;
    protected NivelDto? NivelSeleccionado =>
        _niveles.FirstOrDefault(x =>
            x.Id == _form.NivelId);


    protected PlanDisponibleDto?
        PlanSeleccionado =>
            _planes.FirstOrDefault(x =>
                x.Id == _form.PlanId);


    protected bool CantidadCorrecta =>
        PlanSeleccionado is not null
        &&
        _form.TurnosIds.Count ==
            PlanSeleccionado.VecesPorSemana;


    protected bool AplicaDescuento =>
        _form.TipoDescuento.HasValue;


    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    protected override async Task OnParametersSetAsync()
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

        _errorCarga =
            null;


        // =====================================================
        // LEGAJO
        // =====================================================

        _legajo =
            await LegajoService
                .ObtenerAsync(
                    BebeId);


        if (_legajo is null)
        {
            _errorCarga =
                "El bebé indicado no existe.";

            return;
        }


        // =====================================================
        // NIVELES
        // =====================================================

        _niveles =
            (await NivelService
                .ListarAsync())

            .Where(x =>
                x.Activo)

            .OrderBy(x =>
                x.Orden)

            .ToList();


        // =====================================================
        // PLANES
        // =====================================================

        _planes =
            (await ConsultaService
                .ListarPlanesAsync(
                    _hoy))

            .OrderBy(x =>
                x.VecesPorSemana)

            .ToList();


        // =====================================================
        // PRIMERA INSCRIPCIÓN
        // =====================================================

        if (_legajo.InscripcionActual is null)
        {
            var nivelActualValido =
                _legajo.NivelActualId.HasValue
                &&
                _niveles.Any(x =>
                    x.Id ==
                        _legajo.NivelActualId.Value);


            _form =
                new CambiarInscripcionDto
                {
                    NivelId =
                        nivelActualValido
                            ? _legajo.NivelActualId!.Value
                            : Guid.Empty,

                    /*
                     * Si Bebés Nadadores tiene un único plan,
                     * lo seleccionamos automáticamente.
                     */
                    PlanId =
                        _planes.Count == 1
                            ? _planes[0].Id
                            : Guid.Empty,

                    AplicarDesde =
                        _hoy,

                    MedioPagoPreferido =
                        MedioPago.Transferencia,

                    TipoDescuento =
                        null,

                    ValorDescuento =
                        null,

                    MotivoDescuento =
                        null,

                    TurnosIds =
                        []
                };


            if (_form.NivelId !=
                Guid.Empty)
            {
                await CargarTurnosAsync();
            }


            return;
        }


        // =====================================================
        // INSCRIPCIÓN EXISTENTE
        // =====================================================

        var actual =
            _legajo.InscripcionActual;


        var nivelExiste =
            _legajo.NivelActualId.HasValue
            &&
            _niveles.Any(x =>
                x.Id ==
                    _legajo.NivelActualId.Value);


        var planExiste =
            _planes.Any(x =>
                x.Id ==
                    actual.PlanId);


        _form =
            new CambiarInscripcionDto
            {
                NivelId =
                    nivelExiste
                        ? _legajo.NivelActualId!.Value
                        : Guid.Empty,

                PlanId =
                    planExiste
                        ? actual.PlanId
                        : Guid.Empty,

                AplicarDesde =
                    _hoy,

                MedioPagoPreferido =
                    actual.MedioPagoPreferido,

                TipoDescuento =
                    actual.TipoDescuento,

                ValorDescuento =
                    actual.ValorDescuento,

                MotivoDescuento =
                    actual.MotivoDescuento,

                TurnosIds =
                    actual
                        .Turnos
                        .Select(x =>
                            x.TurnoId)
                        .ToList()
            };


        await CargarTurnosAsync();
    }
    catch (Exception ex)
    {
        _errorCarga =
            ex.Message;
    }
    finally
    {
        _cargando =
            false;
    }
}


    // =========================================================
    // NIVEL
    // =========================================================

    protected async Task NivelCambioAsync()
    {
        _form.TurnosIds.Clear();

        await CargarTurnosAsync();
    }


    // =========================================================
    // PLAN
    // =========================================================

    protected void PlanCambio()
    {
        _form.TurnosIds.Clear();
    }


    // =========================================================
    // TURNOS
    // =========================================================

    private async Task CargarTurnosAsync()
    {
        _turnosDisponibles.Clear();


        if (_form.NivelId ==
            Guid.Empty)
        {
            return;
        }


        try
        {
            _cargandoTurnos =
                true;


            _turnosDisponibles =
                (await ConsultaService
                    .ListarTurnosDisponiblesAsync(
                        _form.NivelId,
                       _form.AplicarDesde))
                .OrderBy(x =>
                    x.DiaSemana)
                .ThenBy(x =>
                    x.HoraInicio)
                .ToList();


            /*
             * Si algún horario actual quedó lleno,
             * igualmente aparece en la grilla porque
             * el bebé ya ocupa ese cupo.
             */
        }
        catch (Exception ex)
        {
            _error =
                ex.Message;
        }
        finally
        {
            _cargandoTurnos =
                false;
        }
    }


    protected void AlternarTurno(
        TurnoDisponibleDto turno)
    {
        _error =
            null;


        if (_form.TurnosIds.Contains(
            turno.TurnoId))
        {
            _form.TurnosIds.Remove(
                turno.TurnoId);

            return;
        }


        if (PlanSeleccionado is null)
        {
            _error =
                "Debe seleccionar un plan.";

            return;
        }


        if (
            turno.Disponibles <= 0
            &&
            !EsTurnoActual(
                turno.TurnoId))
        {
            _error =
                "El horario seleccionado no tiene cupo.";

            return;
        }


        var mismoDia =
            _turnosDisponibles.Any(x =>
                _form.TurnosIds.Contains(
                    x.TurnoId)
                &&
                x.DiaSemana ==
                    turno.DiaSemana);


        if (mismoDia)
        {
            _error =
                "Ya seleccionó otro horario para ese día.";

            return;
        }


        if (_form.TurnosIds.Count >=
            PlanSeleccionado.VecesPorSemana)
        {
            _error =
                $"El plan permite " +
                $"{PlanSeleccionado.VecesPorSemana} turno(s).";

            return;
        }


        _form.TurnosIds.Add(
            turno.TurnoId);
    }


    protected bool EsTurnoActual(
        Guid turnoId)
    {
        return _legajo?
            .InscripcionActual?
            .Turnos
            .Any(x =>
                x.TurnoId ==
                    turnoId)
            == true;
    }


    // =========================================================
    // DESCUENTO
    // =========================================================

    protected void CambiarDescuento(
        ChangeEventArgs e)
    {
        var aplicar =
            e.Value is bool valor
            &&
            valor;


        if (aplicar)
        {
            _form.TipoDescuento =
                TipoDescuento.MontoFijo;

            _form.ValorDescuento =
                0;

            _form.MotivoDescuento =
                string.Empty;
        }
        else
        {
            _form.TipoDescuento =
                null;

            _form.ValorDescuento =
                null;

            _form.MotivoDescuento =
                null;
        }
    }


    protected decimal CalcularDescuento(
        decimal precio)
    {
        if (
            !_form.TipoDescuento.HasValue
            ||
            !_form.ValorDescuento.HasValue)
        {
            return 0;
        }


        var valor =
            Math.Max(
                0,
                _form.ValorDescuento.Value);


        return _form.TipoDescuento.Value switch
        {
            TipoDescuento.Porcentaje =>
                Math.Min(
                    precio,
                    precio * valor / 100m),

            TipoDescuento.MontoFijo =>
                Math.Min(
                    precio,
                    valor),

            _ =>
                0
        };
    }


    protected decimal CalcularTotal(
        decimal precio)
    {
        return Math.Max(
            0,
            precio -
            CalcularDescuento(precio));
    }


    // =========================================================
    // GUARDAR
    // =========================================================

    protected async Task GuardarAsync()
    {
        _error =
            null;


        if (_form.NivelId ==
            Guid.Empty)
        {
            _error =
                "Debe seleccionar un nivel.";

            return;
        }


        if (_form.PlanId ==
            Guid.Empty)
        {
            _error =
                "Debe seleccionar un plan.";

            return;
        }


        if (PlanSeleccionado is null)
        {
            _error =
                "El plan seleccionado no es válido.";

            return;
        }


        if (_form.TurnosIds.Count !=
            PlanSeleccionado.VecesPorSemana)
        {
            _error =
                $"Debe seleccionar exactamente " +
                $"{PlanSeleccionado.VecesPorSemana} horario(s).";

            return;
        }


        if (
            _form.TipoDescuento.HasValue
            &&
            (
                !_form.ValorDescuento.HasValue
                ||
                _form.ValorDescuento.Value <= 0
            ))
        {
            _error =
                "Debe indicar el valor del descuento.";

            return;
        }


        try
        {
            _guardando =
                true;


            if (EsPrimeraInscripcion)
{
    await InscripcionService
        .CrearPrimeraAsync(
            BebeId,
            _form);
}
else
{
    await InscripcionService
        .CambiarAsync(
            BebeId,
            _form);
}


            Navigation.NavigateTo(
                $"/clientes/{BebeId}");
        }
        catch (Exception ex)
        {
            _error =
                ex.Message;
        }
        finally
        {
            _guardando =
                false;
        }
    }


    // =========================================================
    // NAVEGACIÓN
    // =========================================================

    protected void Volver()
    {
        if (_guardando)
            return;


        Navigation.NavigateTo(
            $"/clientes/{BebeId}");
    }


    // =========================================================
    // HELPERS
    // =========================================================

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


    protected static string ObtenerDiaSemanaTexto(
        DiaSemana dia)
    {
        return dia switch
        {
            DiaSemana.Lunes =>
                "Lunes",

            DiaSemana.Martes =>
                "Martes",

            DiaSemana.Miercoles =>
                "Miércoles",

            DiaSemana.Jueves =>
                "Jueves",

            DiaSemana.Viernes =>
                "Viernes",

            DiaSemana.Sabado =>
                "Sábado",

            DiaSemana.Domingo =>
                "Domingo",

            _ =>
                dia.ToString()
        };
    }


    protected static string FormatearPrecio(
        decimal importe)
    {
        return importe.ToString(
            "C0",
            System.Globalization.CultureInfo
                .GetCultureInfo("es-AR"));
    }
}