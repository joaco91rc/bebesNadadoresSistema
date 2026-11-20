using System.Globalization;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Application.Recuperos.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using BebesNadadores.Application.Comunicaciones;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.JSInterop;


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

    [Inject]
    protected IWhatsAppService WhatsAppService { get; set; } = null!;

    [Inject]
    protected IJSRuntime JS { get; set; } = null!;

    [Inject]
    protected IDbContextFactory<AppDbContext> DbFactory { get; set; } = null!;

    // =========================================================
    // ESTADO
    // =========================================================

    protected DateOnly _inicioSemana;

    protected RecuperosPanelDto? _panel;

    protected bool _cargando = true;

    protected string? _error;

    protected string? _mensaje;

    protected string _busqueda = string.Empty;

    // El Autocomplete reemplaza el input y el combo: búsqueda en todos los
    // recuperos disponibles, no limitada a la semana visible.
    protected sealed record RecuperoAlumnoOpcion(Guid Id, string Nombre, int Cantidad);

    protected RecuperoAlumnoOpcion? _alumnoSeleccionado;

    protected Task<IEnumerable<RecuperoAlumnoOpcion>> BuscarAlumnosRecuperosAsync(
        string? texto, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var alumnos = (_panel?.Recuperos ?? [])
            .Where(x => x.Estado == EstadoRecuperacion.Disponible)
            .GroupBy(x => x.BebeId)
            .Select(g => new RecuperoAlumnoOpcion(g.Key, g.First().Bebe, g.Count()))
            .Where(x => string.IsNullOrWhiteSpace(texto)
                || x.Nombre.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Nombre)
            .Take(50)
            .ToList();
        return Task.FromResult<IEnumerable<RecuperoAlumnoOpcion>>(alumnos);
    }

    protected string TextoAlumnoRecuperos(RecuperoAlumnoOpcion? alumno) => alumno is null
        ? string.Empty
        : $"{alumno.Nombre} ({alumno.Cantidad} recupero{(alumno.Cantidad == 1 ? "" : "s")})";

    protected async Task SeleccionarAlumnoAutocompleteAsync(RecuperoAlumnoOpcion? alumno)
    {
        _alumnoSeleccionado = alumno;
        if (alumno is null)
        {
            LimpiarRecuperoSeleccionado();
            return;
        }

        _bebeSeleccionadoId = alumno.Id;
        var primero = RecuperosDelBebe.FirstOrDefault();
        await ElegirRecuperoAsync(primero?.RecuperacionId ?? Guid.Empty);
    }

    protected Guid? _nivelFiltroId;

    protected Guid? _recuperacionSeleccionadaId;
    protected Guid? _bebeSeleccionadoId;

    protected List<RecuperacionActivaDto> RecuperosDelBebe =>
        _panel?.Recuperos.Where(x => x.BebeId == _bebeSeleccionadoId
            && x.Estado == EstadoRecuperacion.Disponible)
            .OrderBy(x => x.FechaVencimiento ?? DateOnly.MaxValue).ThenBy(x => x.FechaOrigen).ToList() ?? [];

    protected bool _respetarDiaPreferido = false;
    protected bool _buscarHastaVencimiento = true;
    private long _versionBusqueda;
    protected string _diaFiltro = "";
    protected string _horaFiltro = "";
    protected bool BusquedaAmpliada => _recuperacionSeleccionadaId.HasValue && _buscarHastaVencimiento;
    protected DateOnly FinBusqueda => RecuperoSeleccionado?.FechaVencimiento ?? Hoy.AddDays(60);
    protected IEnumerable<VacanteRecuperoDto> VacantesParaFiltros =>
        _recuperacionSeleccionadaId.HasValue ? _clasesCompatibles : (_panel?.Vacantes ?? []);
    protected IEnumerable<TimeOnly> HorariosDisponibles => VacantesParaFiltros.Select(x => x.HoraInicio).Distinct().OrderBy(x => x);
    protected async Task CambiarAlcanceAsync()
    {
        _buscarHastaVencimiento = !_buscarHastaVencimiento;
        _inicioSemana = ObtenerLunes(Hoy);
        await CargarAsync();
        await CargarClasesCompatiblesAsync();
    }

    protected bool _cargandoCompatibles;

    protected List<VacanteRecuperoDto> _clasesCompatibles = [];

    protected RecuperacionActivaDto? RecuperoSeleccionado =>
        _panel?.Recuperos.FirstOrDefault(x => x.RecuperacionId == _recuperacionSeleccionadaId);

    protected IEnumerable<(Guid Id, string Nombre)> NivelesDisponibles =>
        VacantesParaFiltros.Select(x => (x.NivelId, x.Nivel)).Distinct().OrderBy(x => x.Nivel);

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

    protected DateOnly Hoy => BebesNadadores.Domain.Common.FechaArgentina.Hoy;

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

            if (int.TryParse(_diaFiltro, out var dia))
                query = query.Where(x => (int)x.Fecha.DayOfWeek == dia);
            if (TimeOnly.TryParseExact(_horaFiltro, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
                query = query.Where(x => x.HoraInicio == hora);
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

    protected async Task ElegirRecuperoAsync(Guid recuperacionId)
    {
        _recuperacionSeleccionadaId = recuperacionId == Guid.Empty ? null : recuperacionId;
        _nivelFiltroId = null;
        _respetarDiaPreferido = false;
        _buscarHastaVencimiento = true;
        _diaFiltro = "";
        _horaFiltro = "";
        _inicioSemana = ObtenerLunes(Hoy);
        await CargarAsync();
        await CargarClasesCompatiblesAsync();
    }

    protected async Task CargarClasesCompatiblesAsync()
    {
        var version = ++_versionBusqueda;
        _clasesCompatibles = [];
        _cargandoCompatibles = false;
        if (!_recuperacionSeleccionadaId.HasValue || RecuperoSeleccionado is null)
        {
            _clasesCompatibles = [];
            return;
        }
        var desde = BusquedaAmpliada ? Hoy : _inicioSemana;
        var hasta = BusquedaAmpliada ? FinBusqueda : _inicioSemana.AddDays(6);
        if (hasta < desde)
        {
            _clasesCompatibles = [];
            return;
        }

        try
        {
            _cargandoCompatibles = true;

            _error = null;

            var resultado = (
                await RecuperosService.ListarClasesCompatiblesAsync(
                    _recuperacionSeleccionadaId.Value,
                    desde,
                    hasta,
                    _respetarDiaPreferido
                )
            ).ToList();
            if (version == _versionBusqueda)
                _clasesCompatibles = resultado;
        }
        catch (Exception ex)
        {
            if (version == _versionBusqueda)
            {
                _error = Errores.Mensaje(ex);
                _clasesCompatibles = [];
            }
        }
        finally
        {
            if (version == _versionBusqueda)
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
        ++_versionBusqueda;
        _cargandoCompatibles = false;
        _recuperacionSeleccionadaId = null;
        _bebeSeleccionadoId = null;
        _alumnoSeleccionado = null;

        _clasesCompatibles = [];

        _respetarDiaPreferido = false;

        _nivelFiltroId = null;
        _diaFiltro = "";
        _horaFiltro = "";
        _buscarHastaVencimiento = true;
    }

    /// <summary>
    /// Abre WhatsApp con un recordatorio redactado. El envío real lo confirma
    /// la administrativa: no hay mensajes automáticos ni envío masivo.
    /// </summary>
    protected async Task RecordarRecuperosWhatsAppAsync(Guid bebeId)
    {
        try
        {
            _error = null;
            var disponibles = _panel?.Recuperos
                .Where(x => x.BebeId == bebeId && x.Estado == EstadoRecuperacion.Disponible)
                .OrderBy(x => x.FechaVencimiento ?? DateOnly.MaxValue)
                .ToList() ?? [];
            if (disponibles.Count == 0)
            {
                _error = "Este alumno no tiene recuperos disponibles para recordar.";
                return;
            }

            await using var db = await DbFactory.CreateDbContextAsync();
            var contactos = await db.BebesResponsables.AsNoTracking()
                .Where(x => x.BebeId == bebeId && x.AutorizaComunicaciones
                            && x.Responsable.Activo)
                .Select(x => new
                {
                    x.EsContactoPrincipal,
                    x.Responsable.Nombre,
                    x.Responsable.WhatsApp
                }).ToListAsync();
            var contacto = contactos
                .Where(x => !string.IsNullOrWhiteSpace(x.WhatsApp))
                .OrderByDescending(x => x.EsContactoPrincipal)
                .FirstOrDefault();
            if (contacto is null)
            {
                _error = $"No hay un responsable de {disponibles[0].Bebe} con WhatsApp y autorización para comunicaciones.";
                return;
            }

            var cantidad = disponibles.Count;
            var textoCantidad = cantidad == 1 ? "una clase pendiente de recupero" : $"{cantidad} clases pendientes de recupero";
            var proximoVencimiento = disponibles
                .Where(x => !x.SinVencimiento && x.FechaVencimiento.HasValue)
                .Select(x => x.FechaVencimiento!.Value)
                .DefaultIfEmpty(DateOnly.MaxValue).Min();
            var textoVencimiento = proximoVencimiento == DateOnly.MaxValue
                ? "" : $"\n📅 El recupero más próximo vence el {proximoVencimiento:dd/MM/yyyy}.";
            var mensaje = $"¡Hola! 👋\n\n" +
                $"Desde Bebés Nadadores te recordamos que {disponibles[0].Bebe} tiene {textoCantidad}." +
                textoVencimiento +
                "\n\n🏊 Escribinos para coordinar un horario con cupo y aprovechar los recuperos pendientes.\n\n¡Muchas gracias!\nBebés Nadadores";

            var url = WhatsAppService.CrearUrl(contacto.WhatsApp, mensaje);
            await JS.InvokeVoidAsync("abrirWhatsAppBebesNadadores", url);
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
        }
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
            _error = Errores.Mensaje(ex);
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
        await CargarClasesCompatiblesAsync();
    }

    // =========================================================
    // SEMANAS
    // =========================================================

    protected async Task SemanaAnterior()
    {
        _buscarHastaVencimiento = false;
        _inicioSemana = _inicioSemana.AddDays(-7);

        await CargarAsync();

        if (_recuperacionSeleccionadaId.HasValue)
        {
            await CargarClasesCompatiblesAsync();
        }
    }

    protected async Task SemanaSiguiente()
    {
        _buscarHastaVencimiento = false;
        _inicioSemana = _inicioSemana.AddDays(7);

        await CargarAsync();

        if (_recuperacionSeleccionadaId.HasValue)
        {
            await CargarClasesCompatiblesAsync();
        }
    }

    protected async Task IrSemanaActual()
    {
        _buscarHastaVencimiento = false;
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
            if (_bebeSeleccionadoId.HasValue)
            {
                var disponibles = RecuperosDelBebe.Count;
                if (disponibles == 0)
                {
                    _alumnoSeleccionado = null;
                    _bebeSeleccionadoId = null;
                }
                else
                {
                    if (_alumnoSeleccionado is not null)
                        _alumnoSeleccionado = _alumnoSeleccionado with { Cantidad = disponibles };
                    await ElegirRecuperoAsync(RecuperosDelBebe[0].RecuperacionId);
                }
            }
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
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
            _errorModal = Errores.Mensaje(ex);

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
            _errorModal = Errores.Mensaje(ex);
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
            await CargarClasesCompatiblesAsync();
        }
        catch (Exception ex)
        {
            _error = Errores.Mensaje(ex);
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
        return tipo.ObtenerTexto();
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
