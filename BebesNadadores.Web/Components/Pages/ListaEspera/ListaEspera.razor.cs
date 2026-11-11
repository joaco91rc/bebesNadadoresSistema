using BebesNadadores.Application.ListaEspera;
using BebesNadadores.Application.ListaEspera.Dtos;
using BebesNadadores.Domain.Enums;
using Microsoft.AspNetCore.Components;
using BebesNadadores.Application.Comunicaciones;
using MudBlazor;
using Microsoft.JSInterop;

namespace BebesNadadores.Web.Components.Pages.ListaEspera;

public partial class ListaEspera : ComponentBase
{
    [Inject]
    protected IListaEsperaService ListaEsperaService { get; set; }
        = null!;


    [Inject]
    protected NavigationManager Navigation { get; set; }
        = null!;

        [Inject]
protected IWhatsAppService WhatsAppService { get; set; }
    = null!;


[Inject]
protected IJSRuntime JS { get; set; }
    = null!;


    // =========================================================
    // ESTADO
    // =========================================================

    protected List<ListaEsperaItemDto> _items =
        [];


    protected bool _cargando =
        true;


    protected bool _guardando;

    protected bool _cancelando;


    protected string? _error;

    protected string? _mensaje;

    protected string? _errorModal;


    protected string _busqueda =
        string.Empty;


    // =========================================================
    // MODAL
    // =========================================================

    protected bool _modalAbierto;


    protected ListaEsperaItemDto? _editando;


    protected BebeListaEsperaBusquedaDto?
        _bebeSeleccionado;


    protected Guid? _recuperacionSeleccionadaId;


    protected bool _diaEspecifico;


    protected DiaSemana _diaPreferido =
        DiaSemana.Miercoles;


    protected FranjaHoraria _franja =
        FranjaHoraria.Indistinta;


    protected string? _observaciones;


    protected ListaEsperaItemDto? _itemACancelar;


    // =========================================================
    // OPCIONES
    // =========================================================

    protected static readonly DiaSemana[]
        DiasDisponibles =
        [
            DiaSemana.Lunes,
            DiaSemana.Martes,
            DiaSemana.Miercoles,
            DiaSemana.Jueves,
            DiaSemana.Viernes,
            DiaSemana.Sabado,
            DiaSemana.Domingo
        ];


    protected static readonly FranjaHoraria[]
        FranjasDisponibles =
        [
            FranjaHoraria.Indistinta,
            FranjaHoraria.Manana,
            FranjaHoraria.Tarde,
            FranjaHoraria.Noche
        ];


    protected DateOnly Hoy =>
        DateOnly.FromDateTime(
            DateTime.Today);


    // =========================================================
    // KPIs
    // =========================================================

    protected int CantidadActivas =>
        _items.Count(x =>
            x.Estado ==
                EstadoListaEspera.Activa);


    protected int CantidadNotificadas =>
        _items.Count(x =>
            x.Estado ==
                EstadoListaEspera.Notificada);


    protected int CantidadConDia =>
        _items.Count(x =>
            x.DiaPreferido.HasValue);


    protected int CantidadVencenPronto =>
        _items.Count(x =>
            x.RecuperacionVence.HasValue
            &&
            x.RecuperacionVence.Value >=
                Hoy
            &&
            x.RecuperacionVence.Value <=
                Hoy.AddDays(7));


    // =========================================================
    // FILTRADO
    // =========================================================

    protected List<ListaEsperaItemDto>
        ItemsFiltrados
    {
        get
        {
            IEnumerable<ListaEsperaItemDto>
                query =
                    _items;


            if (!string.IsNullOrWhiteSpace(
                _busqueda))
            {
                var texto =
                    _busqueda.Trim();


                query =
                    query.Where(x =>
                        x.Bebe.Contains(
                            texto,
                            StringComparison.OrdinalIgnoreCase)

                        ||

                        x.Nivel.Contains(
                            texto,
                            StringComparison.OrdinalIgnoreCase)

                        ||

                        (
                            x.ResponsablePrincipal
                            ?.Contains(
                                texto,
                                StringComparison.OrdinalIgnoreCase)
                            ?? false
                        ));
            }


            return query
                .OrderBy(x =>
                    x.RecuperacionVence
                    ??
                    DateOnly.MaxValue)

                .ThenBy(x =>
                    x.FechaAltaUtc)

                .ToList();
        }
    }


    // =========================================================
    // PREFERENCIA ACTUAL
    // =========================================================

    protected string TextoPreferenciaActual
    {
        get
        {
            var dia =
                _diaEspecifico
                    ? ObtenerDiaTexto(
                        _diaPreferido)
                    : "Cualquier día";


            return
                $"{dia} · " +
                $"{ObtenerFranjaTexto(_franja)}";
        }
    }


    // =========================================================
    // INIT
    // =========================================================

    protected override async Task OnInitializedAsync()
    {
        await CargarAsync();
    }


    // =========================================================
    // CARGAR
    // =========================================================

    protected async Task CargarAsync()
    {
        try
        {
            _cargando =
                true;


            _error =
                null;


            _items =
                (await ListaEsperaService
                    .ListarAsync())
                    .ToList();
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
    // AUTOCOMPLETE
    // =========================================================

    protected async Task<IEnumerable<BebeListaEsperaBusquedaDto>>
        BuscarBebesAsync(
            string? texto,
            CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return [];
        }


        return await ListaEsperaService
            .BuscarBebesAsync(
                texto,
                10,
                ct);
    }


    protected static string TextoBebe(
        BebeListaEsperaBusquedaDto? bebe)
    {
        return bebe?.NombreCompleto
            ?? string.Empty;
    }


    protected Task BebeSeleccionadoAsync(
        BebeListaEsperaBusquedaDto? bebe)
    {
        _bebeSeleccionado =
            bebe;


        _recuperacionSeleccionadaId =
            null;


        if (
            bebe is not null
            &&
            bebe.Recuperaciones.Count == 1)
        {
            _recuperacionSeleccionadaId =
                bebe.Recuperaciones[0].Id;
        }


        return Task.CompletedTask;
    }


    protected void SeleccionarRecupero(
        Guid recuperacionId)
    {
        _recuperacionSeleccionadaId =
            recuperacionId;
    }


    // =========================================================
    // ABRIR NUEVO
    // =========================================================

    protected void AbrirNuevo()
    {
        _editando =
            null;


        _bebeSeleccionado =
            null;


        _recuperacionSeleccionadaId =
            null;


        _diaEspecifico =
            false;


        _diaPreferido =
            DiaSemana.Miercoles;


        _franja =
            FranjaHoraria.Indistinta;


        _observaciones =
            null;


        _errorModal =
            null;


        _modalAbierto =
            true;
    }


    // =========================================================
    // EDITAR
    // =========================================================

    protected void AbrirEditar(
        ListaEsperaItemDto item)
    {
        _editando =
            item;


        _bebeSeleccionado =
            null;


        _recuperacionSeleccionadaId =
            item.RecuperacionId;


        _diaEspecifico =
            item.DiaPreferido.HasValue;


        _diaPreferido =
            item.DiaPreferido
            ??
            DiaSemana.Miercoles;


        _franja =
            item.FranjaHoraria;


        _observaciones =
            item.Observaciones;


        _errorModal =
            null;


        _modalAbierto =
            true;
    }


    // =========================================================
    // PREFERENCIAS
    // =========================================================

    protected void SeleccionarCualquierDia()
    {
        _diaEspecifico =
            false;
    }


    protected void SeleccionarDiaEspecifico()
    {
        _diaEspecifico =
            true;
    }


    protected void SeleccionarDia(
        DiaSemana dia)
    {
        _diaPreferido =
            dia;


        _diaEspecifico =
            true;
    }


    protected void SeleccionarFranja(
        FranjaHoraria franja)
    {
        _franja =
            franja;
    }


    // =========================================================
    // GUARDAR
    // =========================================================

    protected async Task GuardarAsync()
    {
        try
        {
            _guardando =
                true;


            _errorModal =
                null;


            if (_editando is null)
            {
                if (_bebeSeleccionado is null)
                {
                    throw new InvalidOperationException(
                        "Debe seleccionar un bebé.");
                }


                if (!_recuperacionSeleccionadaId.HasValue)
                {
                    throw new InvalidOperationException(
                        "Debe seleccionar el recupero que desea utilizar.");
                }


                var dto =
                    new CrearListaEsperaDto
                    {
                        BebeId =
                            _bebeSeleccionado.BebeId,

                        RecuperacionId =
                            _recuperacionSeleccionadaId.Value,

                        DiaPreferido =
                            _diaEspecifico
                                ? _diaPreferido
                                : null,

                        FranjaHoraria =
                            _franja,

                        Observaciones =
                            _observaciones
                    };


                await ListaEsperaService
                    .AgregarAsync(dto);


                _mensaje =
                    $"{_bebeSeleccionado.NombreCompleto} fue agregado a la lista de espera.";
            }
            else
            {
                var dto =
                    new ActualizarListaEsperaDto
                    {
                        DiaPreferido =
                            _diaEspecifico
                                ? _diaPreferido
                                : null,

                        FranjaHoraria =
                            _franja,

                        Observaciones =
                            _observaciones
                    };


                await ListaEsperaService
                    .ActualizarAsync(
                        _editando.Id,
                        dto);


                _mensaje =
                    $"Preferencias de {_editando.Bebe} actualizadas.";
            }


            _modalAbierto =
                false;


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _errorModal =
                Errores.Mensaje(ex);
        }
        finally
        {
            _guardando =
                false;
        }
    }


    // =========================================================
    // CERRAR
    // =========================================================

    protected void CerrarModal()
    {
        if (_guardando)
        {
            return;
        }


        _modalAbierto =
            false;


        _errorModal =
            null;
    }


    // =========================================================
    // CANCELAR SOLICITUD
    // =========================================================

    protected void SolicitarCancelar(
        ListaEsperaItemDto item)
    {
        _itemACancelar =
            item;
    }


    protected void CerrarCancelar()
    {
        if (_cancelando)
        {
            return;
        }


        _itemACancelar =
            null;
    }


    protected async Task ConfirmarCancelarAsync()
    {
        if (_itemACancelar is null)
        {
            return;
        }


        try
        {
            _cancelando =
                true;


            _error =
                null;


            var nombre =
                _itemACancelar.Bebe;


            await ListaEsperaService
                .CancelarAsync(
                    _itemACancelar.Id);


            _itemACancelar =
                null;


            _mensaje =
                $"{nombre} fue quitado de la lista de espera.";


            await CargarAsync();
        }
        catch (Exception ex)
        {
            _error =
                Errores.Mensaje(ex);
        }
        finally
        {
            _cancelando =
                false;
        }
    }
// =========================================================
// WHATSAPP
// =========================================================

protected async Task AvisarWhatsAppAsync(
    ListaEsperaItemDto item)
{
    try
    {
        _error =
            null;


        if (string.IsNullOrWhiteSpace(
            item.WhatsApp))
        {
            _error =
                $"{item.Bebe} no tiene un número de WhatsApp cargado.";

            return;
        }


        if (string.IsNullOrWhiteSpace(
            item.ResponsablePrincipal))
        {
            _error =
                $"{item.Bebe} no tiene un contacto principal configurado.";

            return;
        }


        var preferenciaDia =
            ObtenerDiaTexto(
                item.DiaPreferido);


        var preferenciaFranja =
            ObtenerFranjaTexto(
                item.FranjaHoraria);


        var mensaje =
            $"¡Hola {item.ResponsablePrincipal}! 👋\n\n" +
            $"Te escribimos de Bebés Nadadores porque " +
            $"tenemos una vacante disponible para que " +
            $"{item.Bebe} pueda utilizar su recupero.\n\n" +
            $"🏊 Nivel: {item.Nivel}\n" +
            $"📅 Preferencia registrada: {preferenciaDia}\n" +
            $"🕒 Horario: {preferenciaFranja}\n\n" +
            $"Si te interesa, respondé este mensaje " +
            $"y coordinamos el lugar.\n\n" +
            $"Bebés Nadadores";


        var url =
            WhatsAppService.CrearUrl(
                item.WhatsApp,
                mensaje);


        await JS.InvokeVoidAsync(
            "abrirWhatsAppBebesNadadores",
            url);
    }
    catch (Exception ex)
    {
        _error =
            Errores.Mensaje(ex);
    }
}

    // =========================================================
    // ESTADOS
    // =========================================================

    protected static string ObtenerEstadoTexto(
        EstadoListaEspera estado)
    {
        return estado switch
        {
            EstadoListaEspera.Activa =>
                "ACTIVA",

            EstadoListaEspera.Notificada =>
                "NOTIFICADA",

            EstadoListaEspera.Asignada =>
                "ASIGNADA",

            EstadoListaEspera.Cancelada =>
                "CANCELADA",

            EstadoListaEspera.Vencida =>
                "VENCIDA",

            _ =>
                estado.ToString()
        };
    }


    protected static string ObtenerClaseEstado(
        EstadoListaEspera estado)
    {
        return estado switch
        {
            EstadoListaEspera.Activa =>
                "active",

            EstadoListaEspera.Notificada =>
                "notified",

            EstadoListaEspera.Asignada =>
                "assigned",

            _ =>
                string.Empty
        };
    }


    // =========================================================
    // VENCIMIENTO
    // =========================================================

    protected string ObtenerClaseVencimiento(
        ListaEsperaItemDto item)
    {
        if (!item.RecuperacionVence.HasValue)
        {
            return "";
        }


        var dias =
            item.RecuperacionVence.Value.DayNumber
            -
            Hoy.DayNumber;


        return dias <= 7
            ? "expires-soon"
            : "";
    }


    // =========================================================
    // DÍAS
    // =========================================================

    protected static string ObtenerDiaTexto(
        DiaSemana? dia)
    {
        return dia.HasValue
            ? ObtenerDiaTexto(
                dia.Value)
            : "Cualquier día";
    }


    protected static string ObtenerDiaTexto(
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


    protected static string ObtenerDiaCorto(
        DiaSemana dia)
    {
        return dia switch
        {
            DiaSemana.Lunes =>
                "LUN",

            DiaSemana.Martes =>
                "MAR",

            DiaSemana.Miercoles =>
                "MIÉ",

            DiaSemana.Jueves =>
                "JUE",

            DiaSemana.Viernes =>
                "VIE",

            DiaSemana.Sabado =>
                "SÁB",

            DiaSemana.Domingo =>
                "DOM",

            _ =>
                dia.ToString()
        };
    }


    // =========================================================
    // FRANJAS
    // =========================================================

    protected static string ObtenerFranjaTexto(
        FranjaHoraria franja)
    {
        return franja switch
        {
            FranjaHoraria.Indistinta =>
                "Cualquier horario",

            FranjaHoraria.Manana =>
                "Mañana",

            FranjaHoraria.Tarde =>
                "Tarde",

            FranjaHoraria.Noche =>
                "Noche",

            _ =>
                franja.ToString()
        };
    }


    protected static string ObtenerIconoFranja(
        FranjaHoraria franja)
    {
        return franja switch
        {
            FranjaHoraria.Manana =>
                Icons.Material.Filled.LightMode,

            FranjaHoraria.Tarde =>
                Icons.Material.Filled.WbTwilight,

            FranjaHoraria.Noche =>
                Icons.Material.Filled.DarkMode,

            _ =>
                Icons.Material.Filled.Schedule
        };
    }


    // =========================================================
    // RECUPERO
    // =========================================================

    protected static string ObtenerTipoRecuperoTexto(
        TipoRecuperacion tipo)
    {
        return tipo.ObtenerTexto();
    }


    // =========================================================
    // INICIALES
    // =========================================================

    protected static string ObtenerIniciales(
        string nombre)
    {
        var partes =
            nombre.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);


        if (partes.Length == 0)
        {
            return "?";
        }


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


    // =========================================================
    // VOLVER
    // =========================================================

    protected void Volver()
    {
        Navigation.NavigateTo(
            "/");
    }
}