using System.Globalization;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Application.Recuperos.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace BebesNadadores.Web.Components.Pages.Recuperos;

public partial class Recuperos : ComponentBase
{
    // =========================================================
    // SERVICIOS
    // =========================================================

    [Inject]
    protected IRecuperosService RecuperosService { get; set; } = null!;

    [Inject]
    protected NavigationManager Navigation { get; set; } = null!;

    // =========================================================
    // ESTADO
    // =========================================================

    protected DateOnly _inicioSemana;

    protected RecuperosPanelDto? _panel;

    protected bool _cargando = true;

    protected string? _error;

    protected string? _mensaje;

    protected string _busqueda = string.Empty;

    protected Guid? _nivelFiltroId;

    protected Guid? _recuperacionSeleccionadaId;

    protected bool _respetarDiaPreferido = true;

    protected bool _cargandoCompatibles;

    protected List<VacanteRecuperoDto> _clasesCompatibles = [];

    protected RecuperacionActivaDto? RecuperoSeleccionado =>
        _panel?.Recuperos.FirstOrDefault(x => x.RecuperacionId == _recuperacionSeleccionadaId);

    protected IEnumerable<(Guid Id, string Nombre)> NivelesDisponibles =>
        _panel?.Vacantes.Select(x => (x.NivelId, x.Nivel)).Distinct().OrderBy(x => x.Nivel)
        ?? Enumerable.Empty<(Guid, string)>();

    // =========================================================
    // MODAL CANDIDATOS
    // =========================================================

    protected VacanteRecuperoDto? _sesionSeleccionada;

    protected List<CandidatoRecuperoDto> _candidatos = [];

    protected bool _cargandoCandidatos;

    protected Guid? _reservandoId;

    protected Guid? _liberandoId;

    protected string? _errorModal;

    // =========================================================
    // FECHA ACTUAL
    // =========================================================

    protected DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    // =========================================================
    // VACANTES VISIBLES
    // =========================================================

    protected List<VacanteRecuperoDto> VacantesVisibles
    {
        get
        {
            IEnumerable<VacanteRecuperoDto> query;

            /*
             * Si seleccionamos un recupero,
             * el backend ya devolvió únicamente
             * sus clases compatibles.
             */
            if (_recuperacionSeleccionadaId.HasValue)
            {
                query = _clasesCompatibles;
            }
            else
            {
                if (_panel is null)
                {
                    return [];
                }

                query = _panel.Vacantes;
            }

            query = query.Where(x => x.Fecha >= Hoy);

            /*
             * El filtro manual de nivel funciona
             * tanto en modo general como por bebé.
             */
            if (_nivelFiltroId.HasValue)
            {
                query = query.Where(x => x.NivelId == _nivelFiltroId.Value);
            }

            return query.OrderBy(x => x.Fecha).ThenBy(x => x.HoraInicio).ToList();
        }
    }

    protected void CambiarNivelFiltro(ChangeEventArgs e)
    {
        var valor = e.Value?.ToString();

        if (string.IsNullOrWhiteSpace(valor) || !Guid.TryParse(valor, out var nivelId))
        {
            _nivelFiltroId = null;

            return;
        }

        _nivelFiltroId = nivelId;
    }

    protected async Task SeleccionarRecuperoAsync(ChangeEventArgs e)
    {
        var valor = e.Value?.ToString();

        if (string.IsNullOrWhiteSpace(valor) || !Guid.TryParse(valor, out var recuperacionId))
        {
            LimpiarRecuperoSeleccionado();

            return;
        }

        _recuperacionSeleccionadaId = recuperacionId;

        var recupero = RecuperoSeleccionado;

        /*
         * Al seleccionar un bebé llevamos automáticamente
         * el filtro al nivel correspondiente.
         */
        _nivelFiltroId = recupero?.NivelOrigenId;

        _respetarDiaPreferido = true;

        await CargarClasesCompatiblesAsync();
    }

    protected async Task CargarClasesCompatiblesAsync()
    {
        if (!_recuperacionSeleccionadaId.HasValue)
        {
            _clasesCompatibles = [];

            return;
        }

        try
        {
            _cargandoCompatibles = true;

            _error = null;

            _clasesCompatibles = (
                await RecuperosService.ListarClasesCompatiblesAsync(
                    _recuperacionSeleccionadaId.Value,
                    _inicioSemana,
                    _inicioSemana.AddDays(6),
                    _respetarDiaPreferido
                )
            ).ToList();
        }
        catch (Exception ex)
        {
            _error = ex.Message;

            _clasesCompatibles = [];
        }
        finally
        {
            _cargandoCompatibles = false;
        }
    }

    protected async Task CambiarRestriccionDia()
    {
        _respetarDiaPreferido = !_respetarDiaPreferido;

        await CargarClasesCompatiblesAsync();
    }

    protected void LimpiarRecuperoSeleccionado()
    {
        _recuperacionSeleccionadaId = null;

        _clasesCompatibles = [];

        _respetarDiaPreferido = true;

        _nivelFiltroId = null;
    }

    protected int LugaresDisponiblesVisibles => VacantesVisibles.Sum(x => x.VacantesParaRecupero);

    // =========================================================
    // RECUPEROS FILTRADOS
    // =========================================================

    protected List<RecuperacionActivaDto> RecuperosFiltrados
    {
        get
        {
            if (_panel is null)
            {
                return [];
            }

            IEnumerable<RecuperacionActivaDto> query = _panel.Recuperos;

            if (!string.IsNullOrWhiteSpace(_busqueda))
            {
                var texto = _busqueda.Trim();

                query = query.Where(x =>
                    x.Bebe.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || x.NivelOrigen.Contains(texto, StringComparison.OrdinalIgnoreCase)
                );
            }

            return query
                .OrderBy(x => x.Estado == EstadoRecuperacion.Reservada ? 1 : 0)
                .ThenBy(x => x.FechaVencimiento ?? DateOnly.MaxValue)
                .ThenBy(x => x.Bebe)
                .ToList();
        }
    }

    // =========================================================
    // TEXTO SEMANA
    // =========================================================

    protected string TextoSemana
    {
        get
        {
            var fin = _inicioSemana.AddDays(6);

            if (_inicioSemana.Month == fin.Month)
            {
                return $"{_inicioSemana.Day:00} — {fin.Day:00} "
                    + $"{ObtenerNombreMes(fin.Month)} {fin.Year}";
            }

            return $"{_inicioSemana.Day:00} "
                + $"{ObtenerNombreMes(_inicioSemana.Month)}"
                + $" — {fin.Day:00} "
                + $"{ObtenerNombreMes(fin.Month)} {fin.Year}";
        }
    }

    // =========================================================
    // INICIALIZACIÓN
    // =========================================================

    protected override async Task OnInitializedAsync()
    {
        _inicioSemana = ObtenerLunes(Hoy);

        await CargarAsync();
    }

    // =========================================================
    // CARGAR
    // =========================================================

    protected async Task CargarAsync()
    {
        try
        {
            _cargando = true;

            _error = null;

            _panel = await RecuperosService.ObtenerPanelAsync(
                _inicioSemana,
                _inicioSemana.AddDays(6)
            );
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

    protected async Task RefrescarAsync()
    {
        _mensaje = null;

        await CargarAsync();
    }

    // =========================================================
    // SEMANAS
    // =========================================================

    protected async Task SemanaAnterior()
    {
        _inicioSemana = _inicioSemana.AddDays(-7);

        await CargarAsync();

        if (_recuperacionSeleccionadaId.HasValue)
        {
            await CargarClasesCompatiblesAsync();
        }
    }

    protected async Task SemanaSiguiente()
    {
        _inicioSemana = _inicioSemana.AddDays(7);

        await CargarAsync();

        if (_recuperacionSeleccionadaId.HasValue)
        {
            await CargarClasesCompatiblesAsync();
        }
    }

    protected async Task IrSemanaActual()
    {
        _inicioSemana = ObtenerLunes(Hoy);

        await CargarAsync();

        if (_recuperacionSeleccionadaId.HasValue)
        {
            await CargarClasesCompatiblesAsync();
        }
    }

    protected async Task ReservarSeleccionadoAsync(VacanteRecuperoDto vacante)
    {
        var recupero = RecuperoSeleccionado;

        if (recupero is null)
        {
            return;
        }

        try
        {
            _reservandoId = recupero.RecuperacionId;

            _error = null;

            _mensaje = null;

            await RecuperosService.ReservarAsync(recupero.RecuperacionId, vacante.SesionClaseId);

            _mensaje =
                $"Recupero de {recupero.Bebe} reservado para "
                + $"{vacante.Fecha:dd/MM/yyyy} a las "
                + $"{vacante.HoraInicio:HH:mm}.";

            /*
             * Limpiamos porque ese recupero acaba
             * de pasar de Disponible a Reservado.
             */
            _recuperacionSeleccionadaId = null;

            _clasesCompatibles = [];

            _respetarDiaPreferido = true;

            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _reservandoId = null;
        }
    }

    // =========================================================
    // CANDIDATOS
    // =========================================================

    protected async Task AbrirCandidatosAsync(VacanteRecuperoDto vacante)
    {
        _sesionSeleccionada = vacante;

        _errorModal = null;

        await CargarCandidatosAsync();
    }

    protected async Task CargarCandidatosAsync()
    {
        if (_sesionSeleccionada is null)
        {
            return;
        }

        try
        {
            _cargandoCandidatos = true;

            _errorModal = null;

            _candidatos = (
                await RecuperosService.ListarCandidatosAsync(_sesionSeleccionada.SesionClaseId)
            ).ToList();
        }
        catch (Exception ex)
        {
            _errorModal = ex.Message;

            _candidatos.Clear();
        }
        finally
        {
            _cargandoCandidatos = false;
        }
    }

    protected void CerrarCandidatos()
    {
        if (_reservandoId.HasValue)
        {
            return;
        }

        _sesionSeleccionada = null;

        _candidatos.Clear();

        _errorModal = null;
    }

    // =========================================================
    // RESERVAR
    // =========================================================

    protected async Task ReservarAsync(CandidatoRecuperoDto candidato)
    {
        if (_sesionSeleccionada is null)
        {
            return;
        }

        try
        {
            _reservandoId = candidato.RecuperacionId;

            _errorModal = null;

            var sesionId = _sesionSeleccionada.SesionClaseId;

            await RecuperosService.ReservarAsync(candidato.RecuperacionId, sesionId);

            _mensaje = $"Recupero reservado para {candidato.Bebe}.";

            await CargarAsync();

            /*
             * Buscamos nuevamente la sesión porque ahora
             * cambió la ocupación.
             */
            var actualizada = VacantesVisibles.FirstOrDefault(x => x.SesionClaseId == sesionId);

            /*
             * Si ya no quedan lugares, cerramos el modal.
             */
            if (actualizada is null || actualizada.VacantesParaRecupero <= 0)
            {
                _sesionSeleccionada = null;

                _candidatos.Clear();

                return;
            }

            _sesionSeleccionada = actualizada;

            await CargarCandidatosAsync();
        }
        catch (Exception ex)
        {
            _errorModal = ex.Message;
        }
        finally
        {
            _reservandoId = null;
        }
    }

    // =========================================================
    // LIBERAR RESERVA
    // =========================================================

    protected async Task LiberarReservaAsync(RecuperacionActivaDto recupero)
    {
        try
        {
            _liberandoId = recupero.RecuperacionId;

            _error = null;

            await RecuperosService.LiberarReservaAsync(recupero.RecuperacionId);

            _mensaje = $"Reserva liberada para {recupero.Bebe}.";

            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _liberandoId = null;
        }
    }

    // =========================================================
    // OCUPACIÓN
    // =========================================================

    protected static string ObtenerEstiloOcupacion(VacanteRecuperoDto vacante)
    {
        if (vacante.Cupo <= 0)
        {
            return "width:0%";
        }

        var ocupacion = vacante.Regulares - vacante.AusentesConAviso + vacante.RecuperosReservados;

        var porcentaje = Math.Clamp(ocupacion * 100d / vacante.Cupo, 0, 100);

        return $"width:{porcentaje:0}%";
    }

    // =========================================================
    // VENCIMIENTO
    // =========================================================

    protected string ObtenerTextoVencimiento(RecuperacionActivaDto recupero)
    {
        if (!recupero.FechaVencimiento.HasValue)
        {
            return "Sin vencimiento";
        }

        var dias = recupero.FechaVencimiento.Value.DayNumber - Hoy.DayNumber;

        if (dias < 0)
        {
            return "Vencido";
        }

        if (dias == 0)
        {
            return "Vence hoy";
        }

        if (dias == 1)
        {
            return "Vence mañana";
        }

        return $"Vence en {dias} días";
    }

    protected string ObtenerTextoVencimiento(CandidatoRecuperoDto candidato)
    {
        if (!candidato.FechaVencimiento.HasValue)
        {
            return "Sin vencimiento";
        }

        if (!candidato.DiasRestantes.HasValue)
        {
            return candidato.FechaVencimiento.Value.ToString("dd/MM/yyyy");
        }

        var dias = candidato.DiasRestantes.Value;

        if (dias <= 0)
        {
            return "Vence hoy";
        }

        if (dias == 1)
        {
            return "Vence mañana";
        }

        return $"Vence en {dias} días";
    }

    protected string ObtenerClaseVencimiento(RecuperacionActivaDto recupero)
    {
        if (!recupero.FechaVencimiento.HasValue)
        {
            return "expiration unlimited";
        }

        var dias = recupero.FechaVencimiento.Value.DayNumber - Hoy.DayNumber;

        if (dias <= 7)
        {
            return "expiration urgent";
        }

        return "expiration";
    }

    protected static string ObtenerClaseVencimiento(CandidatoRecuperoDto candidato)
    {
        if (!candidato.FechaVencimiento.HasValue)
        {
            return "expiration unlimited";
        }

        if (candidato.DiasRestantes.HasValue && candidato.DiasRestantes.Value <= 7)
        {
            return "expiration urgent";
        }

        return "expiration";
    }

    // =========================================================
    // TIPO RECUPERO
    // =========================================================

    protected static string ObtenerTipoRecuperoTexto(TipoRecuperacion tipo)
    {
        return tipo switch
        {
            TipoRecuperacion.AusenciaConAviso => "Ausencia con aviso",

            TipoRecuperacion.ClaseNoDictada => "Clase no dictada",

            _ => tipo.ToString(),
        };
    }

    // =========================================================
    // FECHAS
    // =========================================================

    protected static DateOnly ObtenerLunes(DateOnly fecha)
    {
        var diferencia = ((int)fecha.DayOfWeek + 6) % 7;

        return fecha.AddDays(-diferencia);
    }

    protected static string ObtenerNombreDia(DateOnly fecha)
    {
        return fecha.DayOfWeek switch
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
    }

    protected static string ObtenerMesCorto(int mes)
    {
        string[] meses =
        [
            "",
            "ENE",
            "FEB",
            "MAR",
            "ABR",
            "MAY",
            "JUN",
            "JUL",
            "AGO",
            "SEP",
            "OCT",
            "NOV",
            "DIC",
        ];

        return meses[mes];
    }

    protected static string ObtenerNombreMes(int mes)
    {
        string[] meses =
        [
            "",
            "enero",
            "febrero",
            "marzo",
            "abril",
            "mayo",
            "junio",
            "julio",
            "agosto",
            "septiembre",
            "octubre",
            "noviembre",
            "diciembre",
        ];

        return meses[mes];
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
    // VOLVER
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo("/");
    }
}
