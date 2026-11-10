using BebesNadadores.Application.ListaEspera;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Inscripciones.Dtos;
using BebesNadadores.Application.ListaEspera.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BebesNadadores.Web.Components.Pages.ListaEspera;

public partial class ListaEspera : ComponentBase
{
    [Inject] protected IListaEsperaService Service { get; set; } = null!;
    [Inject] protected IInscripcionConsultaService ConsultaInscripciones { get; set; } = null!;
    [Inject] protected NavigationManager Navigation { get; set; } = null!;
    [Inject] protected IJSRuntime JS { get; set; } = null!;
    protected List<ListaEsperaItemDto> _items = [];
    protected List<NivelEsperaDto> _niveles = [];
    protected List<TurnoEsperaDto> _turnos = [];
    protected Dictionary<Guid, IReadOnlyList<TurnoDisponibleDto>> _disponibilidad = new();
    protected DateOnly _fechaDisponibilidad = DateOnly.FromDateTime(DateTime.Today);
    protected bool _cargando = true, _guardando, _modalAbierto, _verHistorial;
    protected Guid? _editandoId;
    protected ListaEsperaItemDto? _inscribiendo;
    protected string _buscarInscrito = "";
    protected List<BebeInscritoEsperaDto> _inscritos = [];
    protected Guid _bebeInscriptoId;
    protected CrearListaEsperaDto _form = new();
    protected string _busqueda = "";
    protected string? _error, _mensaje, _errorModal;
    protected IEnumerable<ListaEsperaItemDto> ItemsFiltrados => _items.Where(x =>
        string.IsNullOrWhiteSpace(_busqueda) ||
        (x.NombreBebe + " " + x.NombreResponsable + " " + x.Telefono + " " + x.Nivel)
            .Contains(_busqueda, StringComparison.OrdinalIgnoreCase));

    protected override async Task OnInitializedAsync()
    {
        try { _niveles = (await Service.ListarNivelesAsync()).ToList(); _turnos = (await Service.ListarTurnosAsync()).ToList(); }
        catch (Exception ex) { _error = ex.Message; }
        await CargarAsync();
    }
    protected async Task CargarAsync()
    {
        _cargando = true;
        try
        {
            _items = (await Service.ListarAsync(_verHistorial)).ToList();
            _fechaDisponibilidad = DateOnly.FromDateTime(DateTime.Today);
            var niveles = _items.Where(x => x.Estado is EstadoListaEspera.Activa or EstadoListaEspera.Notificada)
                .Select(x => x.NivelId).Distinct().ToList();
            var consultas = await Task.WhenAll(niveles.Select(async id =>
                (Id: id, Turnos: await ConsultaInscripciones.ListarTurnosDisponiblesAsync(id, _fechaDisponibilidad))));
            _disponibilidad = consultas.ToDictionary(x => x.Id, x => x.Turnos);
            _error = null;
        }
        catch (Exception ex) { _error = ex.Message; }
        finally { _cargando = false; }
    }
    protected void AbrirNuevo() { _editandoId = null; _form = new(); _errorModal = null; _modalAbierto = true; }
    protected void AbrirEditar(ListaEsperaItemDto item)
    {
        _editandoId = item.Id;
        _form = new CrearListaEsperaDto { NombreBebe = item.NombreBebe, NombreResponsable = item.NombreResponsable,
            Telefono = item.Telefono, Email = item.Email, NivelId = item.NivelId, TurnoId = item.TurnoId,
            DiaPreferido = item.DiaPreferido, FranjaHoraria = item.FranjaHoraria, Observaciones = item.Observaciones };
        _errorModal = null; _modalAbierto = true;
    }
    protected void CerrarModal() { if (!_guardando) _modalAbierto = false; }
    protected void CambiarNivel(ChangeEventArgs e) { _form.NivelId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : Guid.Empty; _form.TurnoId = null; }
    protected void CambiarTurno(ChangeEventArgs e) { _form.TurnoId = Guid.TryParse(e.Value?.ToString(), out var id) ? id : null; }
    protected void CambiarDia(ChangeEventArgs e) { _form.DiaPreferido = Enum.TryParse<DiaSemana>(e.Value?.ToString(), out var dia) ? dia : null; }
    protected async Task GuardarAsync()
    {
        _guardando = true; _errorModal = null;
        try
        {
            if (_editandoId is Guid id)
                await Service.ActualizarAsync(id, new ActualizarListaEsperaDto { NombreBebe = _form.NombreBebe,
                    NombreResponsable = _form.NombreResponsable, Telefono = _form.Telefono, Email = _form.Email,
                    NivelId = _form.NivelId, TurnoId = _form.TurnoId, DiaPreferido = _form.DiaPreferido,
                    FranjaHoraria = _form.FranjaHoraria, Observaciones = _form.Observaciones });
            else await Service.AgregarAsync(_form);
            _modalAbierto = false; _mensaje = "Solicitud guardada."; await CargarAsync();
        }
        catch (Exception ex) { _errorModal = ex.Message; }
        finally { _guardando = false; }
    }
    protected async Task CambiarEstadoAsync(ListaEsperaItemDto item, EstadoListaEspera estado)
    {
        try { await Service.CambiarEstadoAsync(item.Id, estado); _mensaje = "Estado actualizado."; await CargarAsync(); }
        catch (Exception ex) { _error = ex.Message; }
    }
    protected async Task AvisarAsync(ListaEsperaItemDto item)
    {
        var numero = new string(item.Telefono.Where(char.IsDigit).ToArray());
        if (numero.Length < 10) { _error = "El teléfono debe tener código de país para WhatsApp (por ejemplo, 549...)."; return; }
        var texto = Uri.EscapeDataString($"Hola {item.NombreResponsable}, te contactamos de Bebés Nadadores por tu consulta para {item.NombreBebe}. Tenemos novedades sobre la disponibilidad del nivel {item.Nivel}. ¿Seguís interesado/a?");
        // Marcar notificada solo cuando la administrativa lo indica. Abrir WhatsApp no confirma entrega.
        await JS.InvokeVoidAsync("open", $"https://wa.me/{numero}?text={texto}", "_blank", "noopener,noreferrer");
    }
    protected async Task MarcarInscriptoAsync(ListaEsperaItemDto item)
    {
        _inscribiendo = item;
        _buscarInscrito = item.NombreBebe;
        await BuscarInscritosAsync();
    }
    protected async Task BuscarInscritosAsync()
    {
        try { _inscritos = (await Service.BuscarInscritosAsync(_buscarInscrito)).ToList(); _error = null; }
        catch (Exception ex) { _error = ex.Message; }
    }
    protected async Task ConfirmarInscripcionAsync()
    {
        if (_inscribiendo is null || _bebeInscriptoId == Guid.Empty) return;
        try
        {
            await Service.CambiarEstadoAsync(_inscribiendo.Id, EstadoListaEspera.Asignada, _bebeInscriptoId);
            _inscribiendo = null; _bebeInscriptoId = Guid.Empty;
            _mensaje = "Inscripción vinculada y confirmada."; await CargarAsync();
        }
        catch (Exception ex) { _error = ex.Message; }
    }
    protected static string TextoEstado(EstadoListaEspera estado) => estado switch
    {
        EstadoListaEspera.Activa => "Esperando cupo", EstadoListaEspera.Notificada => "Avisada",
        EstadoListaEspera.Asignada => "Inscripto", EstadoListaEspera.Cancelada => "Cancelada", _ => "Finalizada"
    };
    protected static string TextoPreferencia(ListaEsperaItemDto item) =>
        (item.DiaPreferido?.ToString() ?? "Cualquier día") + " · " + item.FranjaHoraria;
    // Para una inscripción usamos vacantes permanentes, no las ausencias puntuales
    // que liberan cupos temporales destinados a recuperos.
    protected (string Clase, string Titulo, string Detalle) Disponibilidad(ListaEsperaItemDto item)
    {
        if (item.Estado is not (EstadoListaEspera.Activa or EstadoListaEspera.Notificada))
            return ("neutral", "Solicitud finalizada", "");
        if (!_disponibilidad.TryGetValue(item.NivelId, out var turnos))
            return ("neutral", "Disponibilidad sin consultar", "Actualizá la pantalla para volver a consultar.");
        var libres = turnos.Where(x => x.Disponibles > 0).ToList();
        if (libres.Count == 0)
            return ("unavailable", "Sin vacantes en el nivel", "No hay cupos regulares disponibles para nuevas inscripciones.");

        // Si eligieron un turno exacto, éste tiene prioridad.
        // Si no, se prioriza el día solicitado y luego la franja elegida.
        var coincidencias = libres.Where(x =>
            (!item.TurnoId.HasValue || x.TurnoId == item.TurnoId.Value) &&
            (!item.DiaPreferido.HasValue || x.DiaSemana == item.DiaPreferido.Value) &&
            CoincideFranja(x.HoraInicio, item.FranjaHoraria)).ToList();
        if (coincidencias.Count > 0)
            return ("available", "Vacante disponible para lo solicitado", DescribirTurnos(coincidencias));

        // Distinguir alternativas en el mismo día, aunque no coincidan en horario.
        var mismoDia = item.DiaPreferido.HasValue
            ? libres.Where(x => x.DiaSemana == item.DiaPreferido.Value).ToList()
            : new List<TurnoDisponibleDto>();
        if (mismoDia.Count > 0)
            return ("alternative", "Hay lugar el día elegido, en otro horario", DescribirTurnos(mismoDia));
        return ("alternative",
            item.DiaPreferido.HasValue ? "Posible lugar en un día diferente al elegido" : "Posible lugar en otro horario",
            DescribirTurnos(libres));
    }

    private static bool CoincideFranja(TimeOnly hora, FranjaHoraria franja) => franja switch
    {
        FranjaHoraria.Manana => hora.Hour < 12,
        FranjaHoraria.Tarde => hora.Hour >= 12 && hora.Hour < 18,
        FranjaHoraria.Noche => hora.Hour >= 18,
        _ => true
    };

    private static string DescribirTurnos(IEnumerable<TurnoDisponibleDto> turnos) =>
        string.Join(" · ", turnos.Take(4).Select(x =>
            $"{x.DiaSemana} {x.HoraInicio.ToString("HH:mm")} ({x.Disponibles} {(x.Disponibles == 1 ? "vacante" : "vacantes")})")) +
        (turnos.Count() > 4 ? " · y otros horarios" : "");

    // Se habilita si existe AL MENOS una vacante permanente en el nivel.
    // Puede ser alternativa al día deseado; el horario definitivo se elige en el alta.
    protected bool PuedeIniciarInscripcion(ListaEsperaItemDto item) =>
        item.Estado is EstadoListaEspera.Activa or EstadoListaEspera.Notificada &&
        _disponibilidad.TryGetValue(item.NivelId, out var turnos) &&
        turnos.Any(x => x.Disponibles > 0);

    protected void IniciarInscripcion(ListaEsperaItemDto item)
    {
        if (!PuedeIniciarInscripcion(item)) return;
        Navigation.NavigateTo($"/clientes/nuevo?esperaId={item.Id:D}");
    }

    protected void Volver() => Navigation.NavigateTo("/");
}
