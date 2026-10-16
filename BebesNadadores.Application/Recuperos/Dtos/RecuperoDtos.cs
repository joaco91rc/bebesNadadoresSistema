using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Recuperos.Dtos;


// ============================================================
// RESUMEN
// ============================================================

public sealed record RecuperosResumenDto(
    int Disponibles,
    int VencenProximos,
    int Reservados,
    int LugaresSemana);


// ============================================================
// RECUPERO ACTIVO DE UN BEBÉ
// ============================================================

public sealed record RecuperacionActivaDto(
    Guid RecuperacionId,
    Guid BebeId,
    string Bebe,
    Guid NivelOrigenId,
    string NivelOrigen,
    TipoRecuperacion Tipo,
    EstadoRecuperacion Estado,
    DateOnly FechaGeneracion,
    DateOnly? FechaVencimiento,
    bool SinVencimiento,

    DateOnly FechaOrigen,
    TimeOnly HoraOrigen,
    string TurnoOrigen,

    Guid? SesionUsoId,
    DateOnly? FechaUso,
    TimeOnly? HoraUso);


// ============================================================
// LUGAR DISPONIBLE PARA RECUPERO
// ============================================================

public sealed record VacanteRecuperoDto(
    Guid SesionClaseId,
    Guid TurnoId,

    DateOnly Fecha,

    TimeOnly HoraInicio,
    TimeOnly HoraFin,

    Guid NivelId,
    string Nivel,

    string Turno,

    int Cupo,

    int Regulares,

    int AusentesConAviso,

    int RecuperosReservados,

    int VacantesFijas,

    int VacantesLiberadasPorAviso,

    int VacantesParaRecupero,

    IReadOnlyList<string> Profesores,

    // Bebés que tienen reservada ahí su clase inicial.
    int ClaseInicial = 0);


// ============================================================
// CANDIDATO PARA UNA VACANTE
// ============================================================

public sealed record CandidatoRecuperoDto(
    Guid RecuperacionId,
    Guid BebeId,
    string Bebe,

    TipoRecuperacion Tipo,

    DateOnly FechaOrigen,

    DateOnly? FechaVencimiento,

    int? DiasRestantes,

    bool TienePreferencias,

    bool CoincidePreferencia,

    string? Preferencia);


// ============================================================
// PANEL COMPLETO
// ============================================================

public sealed record RecuperosPanelDto(
    RecuperosResumenDto Resumen,
    IReadOnlyList<RecuperacionActivaDto> Recuperos,
    IReadOnlyList<VacanteRecuperoDto> Vacantes);