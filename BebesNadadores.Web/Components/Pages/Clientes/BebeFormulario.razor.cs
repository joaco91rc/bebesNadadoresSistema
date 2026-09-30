using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Inscripciones.Dtos;
using BebesNadadores.Application.Niveles;
using BebesNadadores.Application.Niveles.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Clientes;

public partial class BebeFormulario : ComponentBase
{
    // =========================================================
    // SERVICIOS
    // =========================================================

    [Inject]
    protected IBebeService BebeService { get; set; } = null!;

    [Inject]
    protected INivelService NivelService { get; set; } = null!;

    [Inject]
    protected IInscripcionConsultaService InscripcionConsultaService
    {
        get;
        set;
    } = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = null!;


    // =========================================================
    // PARÁMETROS
    // =========================================================

    [Parameter]
    public Guid? BebeId { get; set; }


    // =========================================================
    // ESTADO
    // =========================================================

    protected GuardarBebeDto _form =
        CrearFormularioNuevo();


    protected List<NivelDto> _niveles = [];


    protected List<PlanDisponibleDto> _planes = [];


    protected List<TurnoDisponibleDto>
        _turnosDisponibles = [];


    protected bool _cargando =
        true;


    protected bool _guardando;


    protected bool _cargandoTurnos;


    protected string? _error;


    // =========================================================
    // PROPIEDADES CALCULADAS
    // =========================================================

    protected bool _editando =>
        BebeId.HasValue;


    protected PlanDisponibleDto?
        PlanSeleccionado =>
            !_form.PlanId.HasValue
                ? null
                : _planes.FirstOrDefault(x =>
                    x.Id ==
                    _form.PlanId.Value);

protected NivelDto? NivelSeleccionado =>
    !_form.NivelActualId.HasValue
        ? null
        : _niveles.FirstOrDefault(x =>
            x.Id == _form.NivelActualId.Value);


protected string NombreBebeParaHorario =>
    string.IsNullOrWhiteSpace(_form.Nombre)
        ? "el bebé"
        : _form.Nombre.Trim();

private async Task FechaInicioCambiadaAsync(
    DateOnly fecha)
{
    _form.FechaInicioInscripcion =
        fecha;

    await FechaInicioCambioAsync();
}
protected bool PlanTieneMultiplesClases =>
    PlanSeleccionado is not null
    &&
    PlanSeleccionado.VecesPorSemana > 1;
    protected bool CantidadTurnosCorrecta =>
        PlanSeleccionado is not null
        &&
        _form.TurnosIds.Count ==
            PlanSeleccionado.VecesPorSemana;


    // =========================================================
    // CICLO DE VIDA
    // =========================================================

    protected override async Task OnParametersSetAsync()
    {
        await CargarPantallaAsync();
    }


    // =========================================================
    // CARGA INICIAL
    // =========================================================

    private async Task CargarPantallaAsync()
    {
        try
        {
            _cargando =
                true;


            _error =
                null;


            _turnosDisponibles.Clear();


            _niveles =
                (await NivelService
                    .ListarAsync())
                .Where(x =>
                    x.Activo)
                .OrderBy(x =>
                    x.Orden)
                .ToList();


            if (_editando)
            {
                await CargarBebeAsync(
                    BebeId!.Value);

                return;
            }


            _form =
                CrearFormularioNuevo();


            await CargarPlanesAsync(
                _form.FechaInicioInscripcion);
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
    // FORMULARIO NUEVO
    // =========================================================

    private static GuardarBebeDto
        CrearFormularioNuevo()
    {
        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        var form =
            new GuardarBebeDto
            {
                FechaNacimiento =
                    hoy,

                Sexo =
                    Sexo.NoInformado,

                NivelActualId =
                    null,

                PlanId =
                    null,

                FechaInicioInscripcion =
                    hoy,

                MedioPagoPreferido =
                    MedioPago.Transferencia,

                TurnosIds =
                    []
            };


        form.Responsables.Add(
            new GuardarResponsableBebeDto
            {
                EsContactoPrincipal =
                    true,

                AutorizaComunicaciones =
                    true
            });


        return form;
    }


    // =========================================================
    // EDICIÓN
    // =========================================================

    private async Task CargarBebeAsync(
        Guid bebeId)
    {
        var bebe =
            await BebeService
                .ObtenerAsync(
                    bebeId);


        if (bebe is null)
        {
            throw new InvalidOperationException(
                "El bebé indicado no existe.");
        }


        _form =
            new GuardarBebeDto
            {
                Nombre =
                    bebe.Nombre,

                Apellido =
                    bebe.Apellido,

                FechaNacimiento =
                    bebe.FechaNacimiento,

                Sexo =
                    bebe.Sexo,

                Dni =
                    bebe.Dni,

                NivelActualId =
                    bebe.NivelActualId,

                PlanId =
                    null,

                FechaInicioInscripcion =
                    DateOnly.FromDateTime(
                        DateTime.Today),

                MedioPagoPreferido =
                    MedioPago.Transferencia,

                TurnosIds =
                    [],

                Responsables =
                    bebe.Responsables
                        .Select(x =>
                            new GuardarResponsableBebeDto
                            {
                                ResponsableId =
                                    x.ResponsableId,

                                Nombre =
                                    x.Nombre,

                                Apellido =
                                    x.Apellido,

                                Dni =
                                    x.Dni,

                                Telefono =
                                    x.Telefono,

                                WhatsApp =
                                    x.WhatsApp,

                                Email =
                                    x.Email,

                                Domicilio =
                                    x.Domicilio,

                                Relacion =
                                    x.Relacion,

                                EsContactoPrincipal =
                                    x.EsContactoPrincipal,

                                AutorizaComunicaciones =
                                    x.AutorizaComunicaciones
                            })
                        .ToList()
            };


        if (_form.Responsables.Count == 0)
        {
            _form.Responsables.Add(
                new GuardarResponsableBebeDto
                {
                    EsContactoPrincipal =
                        true,

                    AutorizaComunicaciones =
                        true
                });
        }
    }


    // =========================================================
    // PLANES
    // =========================================================

   private async Task CargarPlanesAsync(
    DateOnly fecha)
{
    _planes =
        (await InscripcionConsultaService
            .ListarPlanesAsync(
                fecha))
        .OrderBy(x =>
            x.VecesPorSemana)
        .ToList();


    // =========================================================
    // EL PLAN ACTUAL YA NO EXISTE / NO ESTÁ ACTIVO
    // =========================================================

    if (
        _form.PlanId.HasValue
        &&
        !_planes.Any(x =>
            x.Id ==
            _form.PlanId.Value))
    {
        _form.PlanId =
            null;


        _form.TurnosIds.Clear();
    }


    // =========================================================
    // SI HAY UN ÚNICO PLAN, SELECCIONAR AUTOMÁTICAMENTE
    // =========================================================

    if (
        _planes.Count == 1
        &&
        !_form.PlanId.HasValue)
    {
        _form.PlanId =
            _planes[0].Id;
    }
}


    // =========================================================
    // NIVEL
    // =========================================================

    protected async Task NivelCambioAsync()
    {
        _error =
            null;


        _form.TurnosIds.Clear();


        if (_editando)
        {
            return;
        }


        await CargarTurnosDisponiblesAsync();
    }


    // =========================================================
    // PLAN
    // =========================================================

    protected void PlanCambio()
    {
        _error =
            null;


        _form.TurnosIds.Clear();
    }


    // =========================================================
    // FECHA INICIO
    // =========================================================

    protected async Task FechaInicioCambioAsync()
    {
        _error =
            null;


        _form.TurnosIds.Clear();


        await CargarPlanesAsync(
            _form.FechaInicioInscripcion);


        await CargarTurnosDisponiblesAsync();
    }


    // =========================================================
    // TURNOS
    // =========================================================

    private async Task CargarTurnosDisponiblesAsync()
    {
        _turnosDisponibles.Clear();


        if (!_form.NivelActualId.HasValue)
        {
            return;
        }


        try
        {
            _cargandoTurnos =
                true;


            _error =
                null;


            _turnosDisponibles =
                (await InscripcionConsultaService
                    .ListarTurnosDisponiblesAsync(
                        _form.NivelActualId.Value,
                        _form.FechaInicioInscripcion))
                .OrderBy(x =>
                    x.DiaSemana)
                .ThenBy(x =>
                    x.HoraInicio)
                .ToList();
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


    // =========================================================
    // SELECCIONAR TURNO
    // =========================================================

    protected void AlternarTurno(
        TurnoDisponibleDto turno)
    {
        _error =
            null;


        var seleccionado =
            _form.TurnosIds.Contains(
                turno.TurnoId);


        if (seleccionado)
        {
            _form.TurnosIds.Remove(
                turno.TurnoId);

            return;
        }


        if (PlanSeleccionado is null)
        {
            _error =
                "Primero debe seleccionar un plan.";

            return;
        }


        if (turno.Disponibles <= 0)
        {
            _error =
                "El horario seleccionado ya no tiene cupo.";

            return;
        }


        var otroMismoDia =
            _turnosDisponibles
                .Any(x =>
                    _form.TurnosIds.Contains(
                        x.TurnoId)
                    &&
                    x.DiaSemana ==
                        turno.DiaSemana);


        if (otroMismoDia)
        {
            _error =
                $"Ya seleccionó un horario para el día " +
                $"{ObtenerDiaSemanaTexto(turno.DiaSemana)}.";

            return;
        }


        if (_form.TurnosIds.Count >=
            PlanSeleccionado.VecesPorSemana)
        {
            _error =
                $"El plan {PlanSeleccionado.Nombre} permite " +
                $"{PlanSeleccionado.VecesPorSemana} " +
                $"{(PlanSeleccionado.VecesPorSemana == 1
                    ? "turno semanal"
                    : "turnos semanales")}.";

            return;
        }


        _form.TurnosIds.Add(
            turno.TurnoId);
    }


    // =========================================================
    // RESPONSABLES
    // =========================================================

    protected void AgregarResponsable()
    {
        _error =
            null;


        _form.Responsables.Add(
            new GuardarResponsableBebeDto
            {
                AutorizaComunicaciones =
                    true,

                EsContactoPrincipal =
                    _form.Responsables.Count == 0
            });
    }


    protected void QuitarResponsable(
        int index)
    {
        _error =
            null;


        if (
            index < 0
            ||
            index >=
                _form.Responsables.Count)
        {
            return;
        }


        if (_form.Responsables.Count == 1)
        {
            _error =
                "Debe existir al menos un responsable.";

            return;
        }


        var eraPrincipal =
            _form.Responsables[index]
                .EsContactoPrincipal;


        _form.Responsables.RemoveAt(
            index);


        if (
            eraPrincipal
            &&
            _form.Responsables.Count > 0)
        {
            _form.Responsables[0]
                .EsContactoPrincipal =
                    true;
        }
    }


    protected void CambiarPrincipal(
        int index,
        bool valor)
    {
        _error =
            null;


        if (
            index < 0
            ||
            index >=
                _form.Responsables.Count)
        {
            return;
        }


        if (!valor)
        {
            _form.Responsables[index]
                .EsContactoPrincipal =
                    true;

            return;
        }


        for (
            var i = 0;
            i < _form.Responsables.Count;
            i++)
        {
            _form.Responsables[i]
                .EsContactoPrincipal =
                    i == index;
        }
    }


    // =========================================================
    // GUARDAR
    // =========================================================

    protected async Task GuardarAsync()
    {
        _error =
            null;


        if (string.IsNullOrWhiteSpace(
            _form.Nombre))
        {
            _error =
                "Debe indicar el nombre del bebé.";

            return;
        }


        if (string.IsNullOrWhiteSpace(
            _form.Apellido))
        {
            _error =
                "Debe indicar el apellido del bebé.";

            return;
        }


        if (_form.FechaNacimiento >
            DateOnly.FromDateTime(
                DateTime.Today))
        {
            _error =
                "La fecha de nacimiento no puede ser futura.";

            return;
        }


        if (!_form.NivelActualId.HasValue)
        {
            _error =
                "Debe seleccionar el nivel del bebé.";

            return;
        }


        if (_form.Responsables.Count == 0)
        {
            _error =
                "Debe indicar al menos un responsable.";

            return;
        }


        if (_form.Responsables.Any(x =>
            string.IsNullOrWhiteSpace(
                x.Nombre)))
        {
            _error =
                "Debe indicar el nombre de todos los responsables.";

            return;
        }


        if (_form.Responsables.Any(x =>
            string.IsNullOrWhiteSpace(
                x.Apellido)))
        {
            _error =
                "Debe indicar el apellido de todos los responsables.";

            return;
        }


        if (
            _form.Responsables.Count(x =>
                x.EsContactoPrincipal)
            != 1)
        {
            _error =
                "Debe indicar exactamente un contacto principal.";

            return;
        }


        // =====================================================
        // INSCRIPCIÓN - SOLO ALTA
        // =====================================================

        if (!_editando)
        {
            if (!_form.PlanId.HasValue)
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


            if (_form.FechaInicioInscripcion <
                _form.FechaNacimiento)
            {
                _error =
                    "La fecha de inicio de la inscripción " +
                    "no puede ser anterior al nacimiento.";

                return;
            }


            if (_form.TurnosIds.Count !=
                PlanSeleccionado.VecesPorSemana)
            {
                _error =
                    $"Debe seleccionar exactamente " +
                    $"{PlanSeleccionado.VecesPorSemana} " +
                    $"{(PlanSeleccionado.VecesPorSemana == 1
                        ? "horario"
                        : "horarios")} " +
                    $"para el plan elegido.";

                return;
            }


            if (
                _form.TurnosIds
                    .Distinct()
                    .Count()
                !=
                _form.TurnosIds.Count)
            {
                _error =
                    "No puede seleccionar el mismo horario más de una vez.";

                return;
            }


            var seleccionados =
                _turnosDisponibles
                    .Where(x =>
                        _form.TurnosIds.Contains(
                            x.TurnoId))
                    .ToList();


            var hayDiaRepetido =
                seleccionados
                    .GroupBy(x =>
                        x.DiaSemana)
                    .Any(x =>
                        x.Count() > 1);


            if (hayDiaRepetido)
            {
                _error =
                    "No puede seleccionar más de un horario el mismo día.";

                return;
            }
        }


        try
        {
            _guardando =
                true;


            if (_editando)
            {
                await BebeService
                    .ActualizarAsync(
                        BebeId!.Value,
                        _form);
            }
            else
            {
                await BebeService
                    .CrearAsync(
                        _form);
            }


            Navigation.NavigateTo(
                "/clientes");
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
    // VOLVER
    // =========================================================

    protected void Volver()
    {
        if (_guardando)
        {
            return;
        }


        Navigation.NavigateTo(
            "/clientes");
    }


    // =========================================================
    // HELPERS
    // =========================================================

    protected static string ObtenerSexoTexto(
        Sexo sexo)
    {
        return sexo switch
        {
            Sexo.NoInformado =>
                "No informado",

            Sexo.Femenino =>
                "Femenino",

            Sexo.Masculino =>
                "Masculino",

            Sexo.Otro =>
                "Otro",

            _ =>
                sexo.ToString()
        };
    }


    protected static string ObtenerRelacionTexto(
        RelacionResponsable relacion)
    {
        return relacion switch
        {
            RelacionResponsable.Madre =>
                "Madre",

            RelacionResponsable.Padre =>
                "Padre",

            RelacionResponsable.Tutor =>
                "Tutor",

            RelacionResponsable.Otro =>
                "Otro",

            _ =>
                relacion.ToString()
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
        decimal valor)
    {
        return valor.ToString(
            "C0",
            System.Globalization.CultureInfo
                .GetCultureInfo("es-AR"));
    }

    protected bool AplicaDescuento =>
    _form.TipoDescuento.HasValue;


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
}