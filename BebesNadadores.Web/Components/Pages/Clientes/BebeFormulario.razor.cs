using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Inscripciones.Dtos;
using BebesNadadores.Application.Niveles;
using BebesNadadores.Application.Niveles.Dtos;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Application.Turnera.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Clientes;

public partial class BebeFormulario : ComponentBase
{
    // =========================================================
    // SERVICIOS
    // =========================================================
    [Inject]
    protected ICuentaBancariaService CuentaBancariaService { get; set; } = null!;

    [Inject]
    protected ITurneraService TurneraService { get; set; } = null!;

    [Inject]
    protected IBebeService BebeService { get; set; } = null!;

    [Inject]
    protected INivelService NivelService { get; set; } = null!;

    [Inject]
    protected IInscripcionConsultaService InscripcionConsultaService { get; set; } = null!;

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

    protected GuardarBebeDto _form = CrearFormularioNuevo();

    protected List<NivelDto> _niveles = [];

    protected List<CuentaBancariaDto> _cuentasBancarias = [];

    protected List<PlanDisponibleDto> _planes = [];

    protected List<TurnoDisponibleDto> _turnosDisponibles = [];

    protected bool _cargando = true;

    protected bool _guardando;

    protected bool _cargandoTurnos;

    protected string? _error;

    // =========================================================
    // PROPIEDADES CALCULADAS
    // =========================================================

    protected bool _editando => BebeId.HasValue;

    protected PlanDisponibleDto? PlanSeleccionado =>
        !_form.PlanId.HasValue ? null : _planes.FirstOrDefault(x => x.Id == _form.PlanId.Value);

    protected NivelDto? NivelSeleccionado =>
        !_form.NivelActualId.HasValue
            ? null
            : _niveles.FirstOrDefault(x => x.Id == _form.NivelActualId.Value);

    private List<SesionClaseDto> _sesionesIniciales = [];

    private bool _cargandoSesionesIniciales;

    private bool _usarPrimeraClaseEspecial;
    protected string NombreBebeParaHorario =>
        string.IsNullOrWhiteSpace(_form.Nombre) ? "el bebé" : _form.Nombre.Trim();

    /*
     * Fecha que eligió la administrativa en el calendario. Si es del
     * 15 en adelante, BnInicioMitadMes ofrece arrancar ese día (media
     * cuota + recupero) o el 1° del mes siguiente.
     */
    private DateOnly? _fechaElegidaInicio;

    protected DateOnly FechaElegidaInicio =>
        _fechaElegidaInicio ?? _form.FechaInicioInscripcion;

    private async Task FechaInicioCambiadaAsync(DateOnly fecha)
    {
        _fechaElegidaInicio = fecha;

        _form.FechaInicioInscripcion = fecha;

        await FechaInicioCambioAsync();
    }

    private async Task ElegirInicioAsync(DateOnly fecha)
    {
        _form.FechaInicioInscripcion = fecha;

        await FechaInicioCambioAsync();
    }

    protected bool PlanTieneMultiplesClases =>
        PlanSeleccionado is not null && PlanSeleccionado.VecesPorSemana > 1;
    protected bool CantidadTurnosCorrecta =>
        PlanSeleccionado is not null && _form.TurnosIds.Count == PlanSeleccionado.VecesPorSemana;

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
            _cargando = true;

            _error = null;

            _turnosDisponibles.Clear();

            _niveles = (await NivelService.ListarAsync())
                .Where(x => x.Activo)
                .OrderBy(x => x.Orden)
                .ToList();

            if (_editando)
            {
                await CargarBebeAsync(BebeId!.Value);

                return;
            }
            _cuentasBancarias = (await CuentaBancariaService.ListarAsync())
                .Where(x => x.Activa)
                .OrderBy(x => x.Banco)
                .ThenBy(x => x.Nombre)
                .ToList();

            _form = CrearFormularioNuevo();

            await CargarPlanesAsync(_form.FechaInicioInscripcion);
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

    private void CambiarHospitalizacion(bool valor)
    {
        _form.PermanecioHospitalMasDe5Dias = valor;

        if (!valor)
        {
            _form.DetalleHospitalizacion = null;
        }
    }

    private void CambiarTratamiento(bool valor)
    {
        _form.RecibeTratamiento = valor;

        if (!valor)
        {
            _form.DetalleTratamiento = null;
        }
    }

    private async Task CargarSesionesInicialesAsync()
    {
        _sesionesIniciales.Clear();

        if (!_form.NivelActualId.HasValue)
        {
            return;
        }

        try
        {
            _cargandoSesionesIniciales = true;

            var desde = _form.FechaInicioInscripcion;

            /*
             * Mostramos aproximadamente las próximas
             * seis semanas para elegir la primera clase.
             */
            var hasta = desde.AddDays(45);

            _sesionesIniciales = (await TurneraService.ObtenerAgendaAsync(desde, hasta))
                .Where(x =>
                    x.NivelId == _form.NivelActualId.Value
                    && x.Estado == EstadoSesionClase.Programada
                    && x.Vacantes > 0
                )
                .OrderBy(x => x.Fecha)
                .ThenBy(x => x.HoraInicio)
                .ToList();
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _cargandoSesionesIniciales = false;
        }
    }

    private async Task CambiarModalidadHorarioAsync(bool definirHorario)
    {
        _error = null;

        _form.DefinirHorarioHabitualAhora = definirHorario;

        /*
         * Cambiar de modalidad invalida las selecciones
         * anteriores.
         */
        _form.TurnosIds.Clear();

        _form.SesionInicialId = null;

        _usarPrimeraClaseEspecial = false;

        if (!definirHorario)
        {
            await CargarSesionesInicialesAsync();
        }
    }

    private async Task CambiarPrimeraClaseEspecialAsync(ChangeEventArgs e)
    {
        _usarPrimeraClaseEspecial = (bool)(e.Value ?? false);

        _form.SesionInicialId = null;

        if (_usarPrimeraClaseEspecial)
        {
            await CargarSesionesInicialesAsync();
        }
    }

    private void SeleccionarSesionInicial(SesionClaseDto sesion)
    {
        if (sesion.Vacantes <= 0)
        {
            _error = "La clase seleccionada ya no posee cupos.";

            return;
        }

        _form.SesionInicialId = sesion.Id;

        _error = null;
    }

    // =========================================================
    // FORMULARIO NUEVO
    // =========================================================

    private static GuardarBebeDto CrearFormularioNuevo()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var form = new GuardarBebeDto
        {
            FechaNacimiento = hoy,

            Sexo = Sexo.NoInformado,

            NivelActualId = null,

            PlanId = null,

            FechaInicioInscripcion = hoy,

            MedioPagoPreferido = MedioPago.Transferencia,

            MedioPagoInicial = MedioPago.Transferencia,

            CuentaBancariaId = null,

            DefinirHorarioHabitualAhora = true,

            SesionInicialId = null,

            TurnosIds = [],
        };

        form.Responsables.Add(
            new GuardarResponsableBebeDto
            {
                EsContactoPrincipal = true,

                AutorizaComunicaciones = true,
            }
        );

        return form;
    }

    // =========================================================
    // EDICIÓN
    // =========================================================

    private async Task CargarBebeAsync(Guid bebeId)
    {
        var bebe = await BebeService.ObtenerAsync(bebeId);

        if (bebe is null)
        {
            throw new InvalidOperationException("El bebé indicado no existe.");
        }

        _form = new GuardarBebeDto
        {
            Nombre = bebe.Nombre,

            Apellido = bebe.Apellido,

            FechaNacimiento = bebe.FechaNacimiento,

            Sexo = bebe.Sexo,

            Dni = bebe.Dni,

            PermanecioHospitalMasDe5Dias = bebe.PermanecioHospitalMasDe5Dias,

            DetalleHospitalizacion = bebe.DetalleHospitalizacion,

            RecibeTratamiento = bebe.RecibeTratamiento,

            DetalleTratamiento = bebe.DetalleTratamiento,

            NivelActualId = bebe.NivelActualId,

            PlanId = null,

            FechaInicioInscripcion = DateOnly.FromDateTime(DateTime.Today),

            MedioPagoPreferido = MedioPago.Transferencia,

            TurnosIds = [],

            Responsables = bebe
                .Responsables.Select(x => new GuardarResponsableBebeDto
                {
                    ResponsableId = x.ResponsableId,

                    Nombre = x.Nombre,

                    Apellido = x.Apellido,

                    Dni = x.Dni,

                    Telefono = x.Telefono,

                    WhatsApp = x.WhatsApp,

                    Email = x.Email,

                    Domicilio = x.Domicilio,

                    Relacion = x.Relacion,

                    EsContactoPrincipal = x.EsContactoPrincipal,

                    AutorizaComunicaciones = x.AutorizaComunicaciones,
                })
                .ToList(),
        };

        if (_form.Responsables.Count == 0)
        {
            _form.Responsables.Add(
                new GuardarResponsableBebeDto
                {
                    EsContactoPrincipal = true,

                    AutorizaComunicaciones = true,
                }
            );
        }
    }

    // =========================================================
    // PLANES
    // =========================================================

    private async Task CargarPlanesAsync(DateOnly fecha)
    {
        _planes = (await InscripcionConsultaService.ListarPlanesAsync(fecha))
            .OrderBy(x => x.VecesPorSemana)
            .ToList();

        // =========================================================
        // EL PLAN ACTUAL YA NO EXISTE / NO ESTÁ ACTIVO
        // =========================================================

        if (_form.PlanId.HasValue && !_planes.Any(x => x.Id == _form.PlanId.Value))
        {
            _form.PlanId = null;

            _form.TurnosIds.Clear();
        }

        // =========================================================
        // SI HAY UN ÚNICO PLAN, SELECCIONAR AUTOMÁTICAMENTE
        // =========================================================

        if (_planes.Count == 1 && !_form.PlanId.HasValue)
        {
            _form.PlanId = _planes[0].Id;
        }
    }

    // =========================================================
    // NIVEL
    // =========================================================

    private async Task NivelCambioAsync()
    {
        _error = null;

        _form.TurnosIds.Clear();

        _form.SesionInicialId = null;

        if (_editando)
        {
            return;
        }

        await CargarTurnosDisponiblesAsync();

        if (!_form.DefinirHorarioHabitualAhora || _usarPrimeraClaseEspecial)
        {
            await CargarSesionesInicialesAsync();
        }
    }

    // =========================================================
    // PLAN
    // =========================================================

    protected void PlanCambio()
    {
        _error = null;

        _form.TurnosIds.Clear();
    }

    // =========================================================
    // FECHA INICIO
    // =========================================================

    private async Task FechaInicioCambioAsync()
    {
        _error = null;

        _form.TurnosIds.Clear();

        _form.SesionInicialId = null;

        await CargarPlanesAsync(_form.FechaInicioInscripcion);

        await CargarTurnosDisponiblesAsync();

        if (!_form.DefinirHorarioHabitualAhora || _usarPrimeraClaseEspecial)
        {
            await CargarSesionesInicialesAsync();
        }
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
            _cargandoTurnos = true;

            _error = null;

            _turnosDisponibles = (
                await InscripcionConsultaService.ListarTurnosDisponiblesAsync(
                    _form.NivelActualId.Value,
                    _form.FechaInicioInscripcion
                )
            )
                .OrderBy(x => x.DiaSemana)
                .ThenBy(x => x.HoraInicio)
                .ToList();
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
        finally
        {
            _cargandoTurnos = false;
        }
    }

    // =========================================================
    // SELECCIONAR TURNO
    // =========================================================

    protected void AlternarTurno(TurnoDisponibleDto turno)
    {
        _error = null;

        var seleccionado = _form.TurnosIds.Contains(turno.TurnoId);

        if (seleccionado)
        {
            _form.TurnosIds.Remove(turno.TurnoId);

            return;
        }

        if (PlanSeleccionado is null)
        {
            _error = "Primero debe seleccionar un plan.";

            return;
        }

        if (turno.Disponibles <= 0)
        {
            _error = "El horario seleccionado ya no tiene cupo.";

            return;
        }

        var otroMismoDia = _turnosDisponibles.Any(x =>
            _form.TurnosIds.Contains(x.TurnoId) && x.DiaSemana == turno.DiaSemana
        );

        if (otroMismoDia)
        {
            _error =
                $"Ya seleccionó un horario para el día "
                + $"{ObtenerDiaSemanaTexto(turno.DiaSemana)}.";

            return;
        }

        if (_form.TurnosIds.Count >= PlanSeleccionado.VecesPorSemana)
        {
            _error =
                $"El plan {PlanSeleccionado.Nombre} permite "
                + $"{PlanSeleccionado.VecesPorSemana} "
                + $"{(PlanSeleccionado.VecesPorSemana == 1
                    ? "turno semanal"
                    : "turnos semanales")}.";

            return;
        }

        _form.TurnosIds.Add(turno.TurnoId);
    }

    // =========================================================
    // RESPONSABLES
    // =========================================================

    protected void AgregarResponsable()
    {
        _error = null;

        _form.Responsables.Add(
            new GuardarResponsableBebeDto
            {
                AutorizaComunicaciones = true,

                EsContactoPrincipal = _form.Responsables.Count == 0,
            }
        );
    }

    protected void QuitarResponsable(int index)
    {
        _error = null;

        if (index < 0 || index >= _form.Responsables.Count)
        {
            return;
        }

        if (_form.Responsables.Count == 1)
        {
            _error = "Debe existir al menos un responsable.";

            return;
        }

        var eraPrincipal = _form.Responsables[index].EsContactoPrincipal;

        _form.Responsables.RemoveAt(index);

        if (eraPrincipal && _form.Responsables.Count > 0)
        {
            _form.Responsables[0].EsContactoPrincipal = true;
        }
    }

    protected void CambiarPrincipal(int index, bool valor)
    {
        _error = null;

        if (index < 0 || index >= _form.Responsables.Count)
        {
            return;
        }

        if (!valor)
        {
            _form.Responsables[index].EsContactoPrincipal = true;

            return;
        }

        for (var i = 0; i < _form.Responsables.Count; i++)
        {
            _form.Responsables[i].EsContactoPrincipal = i == index;
        }
    }

    // =========================================================
    // GUARDAR
    // =========================================================

    protected async Task GuardarAsync()
    {
        // Evita doble alta (bebé + pago) por doble click.
        if (_guardando)
        {
            return;
        }

        _error = null;

        // =========================================================
        // DATOS DEL BEBÉ
        // =========================================================

        if (string.IsNullOrWhiteSpace(_form.Nombre))
        {
            _error = "Debe indicar el nombre del bebé.";

            return;
        }

        if (string.IsNullOrWhiteSpace(_form.Apellido))
        {
            _error = "Debe indicar el apellido del bebé.";

            return;
        }

        if (_form.FechaNacimiento > DateOnly.FromDateTime(DateTime.Today))
        {
            _error = "La fecha de nacimiento no puede ser futura.";

            return;
        }

        if (!_form.NivelActualId.HasValue)
        {
            _error = "Debe seleccionar el nivel del bebé.";

            return;
        }

        // =========================================================
        // RESPONSABLES
        // =========================================================

        if (_form.Responsables.Count == 0)
        {
            _error = "Debe indicar al menos un responsable.";

            return;
        }

        if (_form.Responsables.Any(x => string.IsNullOrWhiteSpace(x.Nombre)))
        {
            _error = "Debe indicar el nombre de todos los responsables.";

            return;
        }

        if (_form.Responsables.Any(x => string.IsNullOrWhiteSpace(x.Apellido)))
        {
            _error = "Debe indicar el apellido de todos los responsables.";

            return;
        }

        if (_form.Responsables.Count(x => x.EsContactoPrincipal) != 1)
        {
            _error = "Debe indicar exactamente un contacto principal.";

            return;
        }

        // =====================================================
        // ANTECEDENTES DE SALUD - SOLO ALTA
        // =====================================================

        if (!_editando)
        {
            if (
                _form.PermanecioHospitalMasDe5Dias == true
                && string.IsNullOrWhiteSpace(_form.DetalleHospitalizacion)
            )
            {
                _error = "Debe detallar la hospitalización del bebé.";

                return;
            }

            if (
                _form.RecibeTratamiento == true
                && string.IsNullOrWhiteSpace(_form.DetalleTratamiento)
            )
            {
                _error = "Debe detallar el tratamiento que recibe el bebé.";

                return;
            }
        }

        // =========================================================
        // INSCRIPCIÓN - SOLO ALTA
        // =========================================================

        if (!_editando)
        {
            // =====================================================
            // PLAN
            // =====================================================

            if (!_form.PlanId.HasValue)
            {
                _error = "Debe seleccionar un plan.";

                return;
            }

            if (PlanSeleccionado is null)
            {
                _error = "El plan seleccionado no es válido.";

                return;
            }

            if (_form.FechaInicioInscripcion < _form.FechaNacimiento)
            {
                _error =
                    "La fecha de inicio de la inscripción "
                    + "no puede ser anterior al nacimiento.";

                return;
            }

            // =====================================================
            // MODALIDAD DE HORARIO
            // =====================================================

            if (_form.DefinirHorarioHabitualAhora)
            {
                /*
                 * Si ya tiene horario habitual,
                 * tiene que elegir exactamente la cantidad
                 * de días indicada por el plan.
                 */
                if (_form.TurnosIds.Count != PlanSeleccionado.VecesPorSemana)
                {
                    _error =
                        $"Debe seleccionar exactamente "
                        + $"{PlanSeleccionado.VecesPorSemana} horario(s).";

                    return;
                }

                /*
                 * Puede tener horario habitual pero comenzar
                 * excepcionalmente en otra clase.
                 */
                if (_usarPrimeraClaseEspecial && !_form.SesionInicialId.HasValue)
                {
                    _error = "Debe seleccionar la primera clase excepcional.";

                    return;
                }
            }
            else
            {
                /*
                 * Si todavía no tiene horario habitual,
                 * la primera clase concreta es obligatoria.
                 */
                if (!_form.SesionInicialId.HasValue)
                {
                    _error = "Debe seleccionar la primera clase.";

                    return;
                }

                /*
                 * Seguridad adicional:
                 * en este camino no debe viajar ningún
                 * TurnoId habitual.
                 */
                _form.TurnosIds.Clear();
            }

            // =====================================================
            // TURNOS DUPLICADOS
            // =====================================================

            if (_form.TurnosIds.Distinct().Count() != _form.TurnosIds.Count)
            {
                _error = "No puede seleccionar el mismo horario más de una vez.";

                return;
            }

            // =====================================================
            // UN SOLO TURNO POR DÍA
            // =====================================================

            var seleccionados = _turnosDisponibles
                .Where(x => _form.TurnosIds.Contains(x.TurnoId))
                .ToList();

            var hayDiaRepetido = seleccionados.GroupBy(x => x.DiaSemana).Any(x => x.Count() > 1);

            if (hayDiaRepetido)
            {
                _error = "No puede seleccionar más de un horario el mismo día.";

                return;
            }

            // =====================================================
            // PAGO INICIAL
            // =====================================================

            if (_form.MedioPagoInicial.RequiereCuentaBancaria())
            {
                if (!_form.CuentaBancariaId.HasValue)
                {
                    _error =
                        "Debe seleccionar la cuenta que recibió el pago inicial.";

                    return;
                }
            }
            else
            {
                /*
                 * Evitamos mandar una cuenta bancaria que haya
                 * quedado seleccionada previamente si el usuario
                 * cambia el medio de pago.
                 */
                _form.CuentaBancariaId = null;
            }
        }

        // =========================================================
        // GUARDAR
        // =========================================================

        try
        {
            _guardando = true;

            if (_editando)
            {
                await BebeService.ActualizarAsync(BebeId!.Value, _form);
            }
            else
            {
                await BebeService.CrearAsync(_form);
            }

            Navigation.NavigateTo("/clientes");
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
    // VOLVER
    // =========================================================

    protected void Volver()
    {
        if (_guardando)
        {
            return;
        }

        Navigation.NavigateTo("/clientes");
    }

    protected static string RespuestaSaludCss(bool? actual, bool valor)
    {
        return actual == valor ? "health-answer selected" : "health-answer";
    }

    // =========================================================
    // HELPERS
    // =========================================================

    protected static string ObtenerSexoTexto(Sexo sexo)
    {
        return sexo switch
        {
            Sexo.NoInformado => "No informado",

            Sexo.Femenino => "Femenino",

            Sexo.Masculino => "Masculino",

            Sexo.Otro => "Otro",

            _ => sexo.ToString(),
        };
    }

    protected static string ObtenerRelacionTexto(RelacionResponsable relacion)
    {
        return relacion switch
        {
            RelacionResponsable.Madre => "Madre",

            RelacionResponsable.Padre => "Padre",

            RelacionResponsable.Tutor => "Tutor",

            RelacionResponsable.Otro => "Otro",

            _ => relacion.ToString(),
        };
    }

    protected static string ObtenerMedioPagoTexto(MedioPago medio)
    {
        return medio switch
        {
            MedioPago.Efectivo => "Efectivo",

            MedioPago.Transferencia => "Transferencia",

            MedioPago.Tarjeta => "Tarjeta",

            MedioPago.MercadoPago => "Mercado Pago",

            MedioPago.Otro => "Otro",

            _ => medio.ToString(),
        };
    }

    protected static string ObtenerDiaSemanaTexto(DiaSemana dia)
    {
        return dia switch
        {
            DiaSemana.Lunes => "Lunes",

            DiaSemana.Martes => "Martes",

            DiaSemana.Miercoles => "Miércoles",

            DiaSemana.Jueves => "Jueves",

            DiaSemana.Viernes => "Viernes",

            DiaSemana.Sabado => "Sábado",

            DiaSemana.Domingo => "Domingo",

            _ => dia.ToString(),
        };
    }

    protected static string FormatearPrecio(decimal valor)
    {
        return valor.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("es-AR"));
    }

    protected bool AplicaDescuento => _form.TipoDescuento.HasValue;

    protected void CambiarDescuento(ChangeEventArgs e)
    {
        var aplicar = e.Value is bool valor && valor;

        if (aplicar)
        {
            _form.TipoDescuento = TipoDescuento.MontoFijo;

            _form.ValorDescuento = 0;

            _form.MotivoDescuento = string.Empty;
        }
        else
        {
            _form.TipoDescuento = null;

            _form.ValorDescuento = null;

            _form.MotivoDescuento = null;
        }
    }

    protected decimal CalcularDescuento(decimal precio)
    {
        if (!_form.TipoDescuento.HasValue || !_form.ValorDescuento.HasValue)
        {
            return 0;
        }

        var valor = Math.Max(0, _form.ValorDescuento.Value);

        return _form.TipoDescuento.Value switch
        {
            TipoDescuento.Porcentaje => Math.Min(precio, precio * valor / 100m),

            TipoDescuento.MontoFijo => Math.Min(precio, valor),

            _ => 0,
        };
    }

    protected decimal CalcularTotal(decimal precio)
    {
        return Math.Max(0, precio - CalcularDescuento(precio));
    }
}
