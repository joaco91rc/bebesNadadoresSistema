using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.ListaEspera.Dtos;


// ============================================================
// RECUPERO DISPONIBLE DEL BEBÉ
// ============================================================

public sealed record RecuperacionDisponibleListaEsperaDto(
    Guid Id,
    TipoRecuperacion Tipo,
    DateOnly FechaGeneracion,
    DateOnly? FechaVencimiento,
    bool SinVencimiento);


// ============================================================
// RESULTADO DEL AUTOCOMPLETE
// ============================================================

public sealed record BebeListaEsperaBusquedaDto(
    Guid BebeId,
    string NombreCompleto,

    Guid NivelId,
    string Nivel,

    string? ResponsablePrincipal,
    string? WhatsApp,
    string? Email,

    IReadOnlyList<RecuperacionDisponibleListaEsperaDto>
        Recuperaciones);


// ============================================================
// ITEM DE LISTA DE ESPERA
// ============================================================

public sealed record ListaEsperaItemDto(
    Guid Id,

    Guid BebeId,
    string Bebe,

    Guid NivelId,
    string Nivel,

    Guid? RecuperacionId,
    TipoRecuperacion? TipoRecuperacion,
    DateOnly? RecuperacionVence,

    DiaSemana? DiaPreferido,
    FranjaHoraria FranjaHoraria,

    EstadoListaEspera Estado,

    DateTimeOffset FechaAltaUtc,

    string? Observaciones,

    string? ResponsablePrincipal,
    string? WhatsApp,
    string? Email);


// ============================================================
// ALTA
// ============================================================

public sealed class CrearListaEsperaDto
{
    public Guid BebeId { get; set; }

    public Guid RecuperacionId { get; set; }


    /*
     * NULL significa cualquier día.
     *
     * Ej:
     * Miércoles = solamente quiere recuperar
     * un miércoles.
     */
    public DiaSemana? DiaPreferido { get; set; }


    /*
     * Puede ser:
     *
     * Indistinta
     * Mañana
     * Tarde
     * Noche
     */
    public FranjaHoraria FranjaHoraria { get; set; }
        = FranjaHoraria.Indistinta;


    public string? Observaciones { get; set; }
}


// ============================================================
// EDICIÓN DE PREFERENCIAS
// ============================================================

public sealed class ActualizarListaEsperaDto
{
    public DiaSemana? DiaPreferido { get; set; }

    public FranjaHoraria FranjaHoraria { get; set; }
        = FranjaHoraria.Indistinta;

    public string? Observaciones { get; set; }
}