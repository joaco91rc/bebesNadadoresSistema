using System.Security.Claims;
using BebesNadadores.Application.AptosMedicos;
using BebesNadadores.Application.AptosMedicos.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using BebesNadadores.Application.Configuracion;
using MudBlazor;

namespace BebesNadadores.Web.Components.Pages.AptoMedico;

public partial class AptosMedicos : ComponentBase
{
    [Inject]
    protected IAptoMedicoService AptoMedicoService { get; set; } = null!;

    [Inject]
    protected AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = null!;

    [Inject]
protected IConfiguracionSistemaService ConfiguracionService { get; set; }   = null!;

    // =========================================================
    // ESTADO GENERAL
    // =========================================================
private int _mesesVigenciaApto = 1;
    protected bool _cargando = true;
    protected bool _vistaControlGeneral;

    protected string? _error;
    protected string? _mensaje;

    protected AptoMedicoResumenDto? _resumen;

    protected DateOnly _fechaAgenda;

    protected List<FranjaMedicaDto> _agenda = [];
    protected List<AptoMedicoBebeDto> _bebes = [];

    // =========================================================
    // FILTROS
    // =========================================================

    protected string _busqueda = string.Empty;
    protected SituacionAptoMedico? _filtroSituacion;
    protected Guid? _filtroNivelId;

    // =========================================================
    // MODAL
    // =========================================================

    protected AptoMedicoBebeDto? _bebeSeleccionado;

    protected List<AptoMedicoHistorialDto> _historial = [];

    protected bool _cargandoHistorial;
    protected bool _guardando;

    protected string? _errorModal;

    protected RevisionFormModel _form = new();

    // =========================================================
    // LISTADOS DERIVADOS
    // =========================================================

    protected List<AptoMedicoBebeDto> BebesFiltrados
    {
        get
        {
            IEnumerable<AptoMedicoBebeDto> query = _bebes;

            if (_filtroSituacion.HasValue)
            {
                query = query.Where(x => x.Situacion == _filtroSituacion.Value);
            }

            if (_filtroNivelId.HasValue)
            {
                query = query.Where(x => x.NivelId == _filtroNivelId.Value);
            }

            if (!string.IsNullOrWhiteSpace(_busqueda))
            {
                var texto = _busqueda.Trim();

                query = query.Where(x =>
                    x.Bebe.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || (
                        x.Nivel is not null
                        && x.Nivel.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    )
                );
            }

            return query
                .OrderBy(x => ObtenerOrdenSituacion(x.Situacion))
                .ThenBy(x => x.VigenciaHasta ?? DateOnly.MinValue)
                .ThenBy(x => x.Bebe)
                .ToList();
        }
    }

    protected IEnumerable<(Guid Id, string Nombre)> NivelesDisponibles =>
        _bebes
            .Where(x => x.NivelId.HasValue && !string.IsNullOrWhiteSpace(x.Nivel))
            .Select(x => (x.NivelId!.Value, x.Nivel!))
            .Distinct()
            .OrderBy(x => x.Item2);

    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    protected override async Task OnInitializedAsync()
    {
        _fechaAgenda = ObtenerAhoraArgentina().Fecha;

        await CargarTodoAsync();
    }

    // =========================================================
    // CARGA
    // =========================================================

    protected async Task CargarTodoAsync()
    {
        try
        {
            _cargando = true;
            _error = null;
            var configuracion =
    await ConfiguracionService.ObtenerAsync();

_mesesVigenciaApto =
    configuracion.MesesVigenciaApto;

            await Task.WhenAll(CargarResumenAsync(), CargarBebesAsync(), CargarAgendaAsync());
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _cargando = false;
        }
    }

    private async Task CargarResumenAsync()
    {
        _resumen = await AptoMedicoService.ObtenerResumenAsync();
    }

    private async Task CargarBebesAsync()
    {
        _bebes = (await AptoMedicoService.ListarBebesAsync()).ToList();
    }

    private async Task CargarAgendaAsync()
    {
        var ahora = ObtenerAhoraArgentina();

        TimeOnly horaReferencia;

        if (_fechaAgenda == ahora.Fecha)
        {
            horaReferencia = ahora.Hora;
        }
        else if (_fechaAgenda < ahora.Fecha)
        {
            /*
             * Para fechas anteriores mostramos
             * los últimos horarios del día.
             */
            horaReferencia = new TimeOnly(23, 59, 59);
        }
        else
        {
            /*
             * Para fechas futuras mostramos
             * los primeros horarios del día.
             */
            horaReferencia = TimeOnly.MinValue;
        }

        _agenda = (
            await AptoMedicoService.ObtenerAgendaMedicaAsync(_fechaAgenda, horaReferencia)
        ).ToList();
    }

    protected async Task RefrescarAsync()
    {
        _mensaje = null;

        await CargarTodoAsync();
    }

    // =========================================================
    // VISTAS
    // =========================================================

    protected void MostrarAgenda()
    {
        _vistaControlGeneral = false;
    }

    protected void MostrarControlGeneral()
    {
        _vistaControlGeneral = true;
    }

    protected void AbrirControlConFiltro(SituacionAptoMedico situacion)
    {
        _vistaControlGeneral = true;

        _filtroSituacion = situacion;
    }

    // =========================================================
    // NAVEGACIÓN FECHA
    // =========================================================

    protected async Task DiaAnteriorAsync()
    {
        _fechaAgenda = _fechaAgenda.AddDays(-1);

        await RecargarSoloAgendaAsync();
    }

    protected async Task DiaSiguienteAsync()
    {
        _fechaAgenda = _fechaAgenda.AddDays(1);

        await RecargarSoloAgendaAsync();
    }

    protected async Task IrAHoyAsync()
    {
        _fechaAgenda = ObtenerAhoraArgentina().Fecha;

        await RecargarSoloAgendaAsync();
    }

    private async Task RecargarSoloAgendaAsync()
    {
        try
        {
            _cargando = true;
            _error = null;

            await CargarAgendaAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _cargando = false;
        }
    }

    // =========================================================
    // FILTROS
    // =========================================================

    protected void CambiarFiltroSituacion(ChangeEventArgs e)
    {
        var valor = e.Value?.ToString();

        if (
            string.IsNullOrWhiteSpace(valor)
            || !int.TryParse(valor, out var numero)
            || !Enum.IsDefined(typeof(SituacionAptoMedico), numero)
        )
        {
            _filtroSituacion = null;

            return;
        }

        _filtroSituacion = (SituacionAptoMedico)numero;
    }

    protected void CambiarFiltroNivel(ChangeEventArgs e)
    {
        var valor = e.Value?.ToString();

        if (string.IsNullOrWhiteSpace(valor) || !Guid.TryParse(valor, out var nivelId))
        {
            _filtroNivelId = null;

            return;
        }

        _filtroNivelId = nivelId;
    }

    protected void LimpiarFiltros()
    {
        _filtroSituacion = null;

        _filtroNivelId = null;

        _busqueda = string.Empty;
    }

    protected string ObtenerValorFiltroSituacion()
    {
        return _filtroSituacion.HasValue ? ((int)_filtroSituacion.Value).ToString() : string.Empty;
    }

    // =========================================================
    // ABRIR REVISIÓN
    // =========================================================

    protected async Task AbrirRevisionAsync(
    AptoMedicoBebeDto bebe)
{
    _bebeSeleccionado = bebe;

    _errorModal = null;

    _historial = [];

    var hoy =
        ObtenerAhoraArgentina().Fecha;

    _form =
        new RevisionFormModel
        {
            FechaRevision =
                hoy,

            VigenciaHasta =
                hoy.AddMonths(1),

            Resultado =
                null,

            Observaciones =
                string.Empty,
        };

    await CargarHistorialAsync(
        bebe.BebeId);
}

    private async Task CargarHistorialAsync(Guid bebeId)
    {
        try
        {
            _cargandoHistorial = true;

            _historial = (await AptoMedicoService.ObtenerHistorialAsync(bebeId)).ToList();
        }
        catch (Exception ex)
        {
            _errorModal = ex.Message;
        }
        finally
        {
            _cargandoHistorial = false;
        }
    }

    protected void CerrarRevision()
    {
        if (_guardando)
        {
            return;
        }

        _bebeSeleccionado = null;

        _historial = [];

        _errorModal = null;
    }

    // =========================================================
    // GUARDAR REVISIÓN
    // =========================================================

    protected async Task GuardarRevisionAsync()
    {
        if (_bebeSeleccionado is null)
        {
            return;
        }

        if (!_form.Resultado.HasValue)
        {
            _errorModal = "Seleccioná el resultado de la revisación.";

            return;
        }

        if (_form.VigenciaHasta < _form.FechaRevision)
        {
            _errorModal = "La vigencia no puede finalizar antes de la fecha de revisión.";

            return;
        }

        try
        {
            _guardando = true;

            _errorModal = null;

            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();

            var usuario = authState.User;

            var medicoUsuarioIdTexto = usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(medicoUsuarioIdTexto, out var medicoUsuarioId))
            {
                throw new InvalidOperationException(
                    "No se pudo identificar al usuario que realiza la revisión."
                );
            }

            var bebeNombre = _bebeSeleccionado.Bebe;

            await AptoMedicoService.RegistrarAsync(
                new GuardarAptoMedicoDto
                {
                    BebeId = _bebeSeleccionado.BebeId,

                    FechaRevision = _form.FechaRevision,

                    VigenciaHasta = _form.VigenciaHasta,

                    Resultado = _form.Resultado.Value,

                    Observaciones = _form.Observaciones,
                },
                medicoUsuarioId
            );

            _bebeSeleccionado = null;

            _historial = [];

            _mensaje = $"Revisación médica registrada para {bebeNombre}.";

            await CargarTodoAsync();
        }
        catch (Exception ex)
        {
            _errorModal = ex.Message;
        }
        finally
        {
            _guardando = false;
        }
    }

    // =========================================================
    // FECHAS FORMULARIO
    // =========================================================

private Task CambiarFechaRevisionAsync(
    DateOnly fecha)
{
    _form.FechaRevision =
        fecha;

    // Vigencia mensual.
    _form.VigenciaHasta =
    fecha.AddMonths(
        _mesesVigenciaApto);

    return Task.CompletedTask;
}


private Task CambiarVigenciaHastaAsync(
    DateOnly fecha)
{
    _form.VigenciaHasta =
        fecha;

    return Task.CompletedTask;
}
    // =========================================================
    // TEXTOS DE SITUACIÓN
    // =========================================================

    protected static string ObtenerNombreSituacion(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.SinApto => "Sin apto",

            SituacionAptoMedico.Vigente => "Vigente",

            SituacionAptoMedico.PorVencer => "Por vencer",

            SituacionAptoMedico.Vencido => "Vencido",

            SituacionAptoMedico.Observado => "Observado",

            SituacionAptoMedico.NoApto => "No apto",

            _ => situacion.ToString(),
        };
    }

    protected static string ObtenerTextoSituacion(AptoMedicoBebeDto bebe)
    {
        return bebe.Situacion switch
        {
            SituacionAptoMedico.SinApto => "SIN APTO",

            SituacionAptoMedico.Vigente when bebe.VigenciaHasta.HasValue =>
                $"APTO HASTA {bebe.VigenciaHasta.Value:dd/MM/yyyy}",

            SituacionAptoMedico.PorVencer when bebe.VigenciaHasta.HasValue =>
                $"VENCE {bebe.VigenciaHasta.Value:dd/MM/yyyy}",

            SituacionAptoMedico.Vencido when bebe.VigenciaHasta.HasValue =>
                $"VENCIDO {bebe.VigenciaHasta.Value:dd/MM/yyyy}",

            SituacionAptoMedico.Observado => "OBSERVADO",

            SituacionAptoMedico.NoApto => "NO APTO",

            _ => ObtenerNombreSituacion(bebe.Situacion).ToUpperInvariant(),
        };
    }

    protected static string ObtenerClaseSituacion(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.SinApto => "missing",

            SituacionAptoMedico.Vigente => "valid",

            SituacionAptoMedico.PorVencer => "expiring",

            SituacionAptoMedico.Vencido => "expired",

            SituacionAptoMedico.Observado => "observed",

            SituacionAptoMedico.NoApto => "unfit",

            _ => string.Empty,
        };
    }

    protected static string ObtenerIconoSituacion(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.SinApto => Icons.Material.Filled.HelpOutline,

            SituacionAptoMedico.Vigente => Icons.Material.Filled.Verified,

            SituacionAptoMedico.PorVencer => Icons.Material.Filled.Schedule,

            SituacionAptoMedico.Vencido => Icons.Material.Filled.EventBusy,

            SituacionAptoMedico.Observado => Icons.Material.Filled.Visibility,

            SituacionAptoMedico.NoApto => Icons.Material.Filled.Block,

            _ => Icons.Material.Filled.HealthAndSafety,
        };
    }

    protected static string ObtenerTextoBotonRevision(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.SinApto => "Realizar apto",

            SituacionAptoMedico.Vigente => "Nueva revisión",

            SituacionAptoMedico.PorVencer => "Renovar",

            SituacionAptoMedico.Vencido => "Renovar",

            SituacionAptoMedico.Observado => "Revisar",

            SituacionAptoMedico.NoApto => "Revisar",

            _ => "Revisar",
        };
    }

    protected static string ObtenerClaseBotonRevision(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.SinApto => "important",

            SituacionAptoMedico.Vencido => "important",

            SituacionAptoMedico.NoApto => "important",

            _ => string.Empty,
        };
    }

    // =========================================================
    // RESULTADOS
    // =========================================================

    protected static string ObtenerTextoResultado(ResultadoAptoMedico resultado)
    {
        return resultado switch
        {
            ResultadoAptoMedico.Apto => "APTO",

            ResultadoAptoMedico.Observado => "OBSERVADO",

            ResultadoAptoMedico.NoApto => "NO APTO",

            _ => resultado.ToString().ToUpperInvariant(),
        };
    }

    protected static string ObtenerClaseResultado(ResultadoAptoMedico resultado)
    {
        return resultado switch
        {
            ResultadoAptoMedico.Apto => "fit",

            ResultadoAptoMedico.Observado => "observed",

            ResultadoAptoMedico.NoApto => "unfit",

            _ => string.Empty,
        };
    }

    // =========================================================
    // EDAD
    // =========================================================

    protected static string ObtenerEdadTexto(DateOnly nacimiento)
    {
        var hoy = ObtenerAhoraArgentina().Fecha;

        var meses = (hoy.Year - nacimiento.Year) * 12 + hoy.Month - nacimiento.Month;

        if (hoy.Day < nacimiento.Day)
        {
            meses--;
        }

        meses = Math.Max(0, meses);

        if (meses < 24)
        {
            return meses == 1 ? "1 mes" : $"{meses} meses";
        }

        var anios = meses / 12;

        return anios == 1 ? "1 año" : $"{anios} años";
    }

    // =========================================================
    // FECHA
    // =========================================================

    protected static string ObtenerTituloFecha(DateOnly fecha)
    {
        var ahora = ObtenerAhoraArgentina();

        if (fecha == ahora.Fecha)
        {
            return $"Hoy · {fecha:dd/MM/yyyy}";
        }

        var dia = fecha.DayOfWeek switch
        {
            DayOfWeek.Monday => "Lunes",
            DayOfWeek.Tuesday => "Martes",
            DayOfWeek.Wednesday => "Miércoles",
            DayOfWeek.Thursday => "Jueves",
            DayOfWeek.Friday => "Viernes",
            DayOfWeek.Saturday => "Sábado",
            DayOfWeek.Sunday => "Domingo",
            _ => string.Empty,
        };

        return $"{dia} · {fecha:dd/MM/yyyy}";
    }

    // =========================================================
    // ORDEN
    // =========================================================

    private static int ObtenerOrdenSituacion(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.NoApto => 1,
            SituacionAptoMedico.SinApto => 2,
            SituacionAptoMedico.Vencido => 3,
            SituacionAptoMedico.Observado => 4,
            SituacionAptoMedico.PorVencer => 5,
            SituacionAptoMedico.Vigente => 6,
            _ => 99,
        };
    }

    // =========================================================
    // INICIALES
    // =========================================================

    protected static string ObtenerIniciales(string nombre)
    {
        var partes = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (partes.Length == 0)
        {
            return "?";
        }

        if (partes.Length == 1)
        {
            return partes[0][0].ToString().ToUpperInvariant();
        }

        return string.Concat(partes[0][0], partes[^1][0]).ToUpperInvariant();
    }

    // =========================================================
    // HORA ARGENTINA
    // =========================================================

    private static (DateOnly Fecha, TimeOnly Hora) ObtenerAhoraArgentina()
    {
        TimeZoneInfo zona;

        try
        {
            /* Linux / macOS */
            zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            /* Windows */
            zona = TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time");
        }

        var ahora = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zona);

        return (DateOnly.FromDateTime(ahora.DateTime), TimeOnly.FromDateTime(ahora.DateTime));
    }

    // =========================================================
    // VOLVER
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo("/");
    }

    // =========================================================
    // MODELO FORMULARIO
    // =========================================================

    protected sealed class RevisionFormModel
    {
        public DateOnly FechaRevision { get; set; }

        public DateOnly VigenciaHasta { get; set; }

        public ResultadoAptoMedico? Resultado { get; set; }

        public string? Observaciones { get; set; }
    }
}
