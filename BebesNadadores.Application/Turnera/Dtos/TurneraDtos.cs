using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Turnera.Dtos;

public sealed record ProfesorTurnoInputDto(
    Guid ProfesorId,
    bool EsPrincipal);


public sealed record ProfesorTurnoDto(
    Guid ProfesorId,
    string NombreCompleto,
    bool EsPrincipal);


public sealed record TurnoDto(
    Guid Id,
    string Nombre,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    int DuracionMinutos,
    TimeOnly HoraFin,
    Guid NivelId,
    string Nivel,
    int Cupo,
    string? Sector,
    DateOnly VigenteDesde,
    DateOnly? VigenteHasta,
    EstadoTurno Estado,
    IReadOnlyList<ProfesorTurnoDto> Profesores);


public sealed class CrearTurnoDto
{
    public string Nombre { get; set; } = string.Empty;

    public DiaSemana DiaSemana { get; set; }

    public TimeOnly HoraInicio { get; set; }

    public int DuracionMinutos { get; set; }

    public Guid NivelId { get; set; }

    public int Cupo { get; set; }

    public string? Sector { get; set; }

    public DateOnly VigenteDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public List<ProfesorTurnoInputDto> Profesores { get; set; }    = [];
    public Guid CicloLectivoId { get; set; }
}


public sealed class ActualizarTurnoDto
{
    public string Nombre { get; set; } = string.Empty;

    public DiaSemana DiaSemana { get; set; }

    public TimeOnly HoraInicio { get; set; }

    public int DuracionMinutos { get; set; }

    public Guid NivelId { get; set; }

    public int Cupo { get; set; }

    public string? Sector { get; set; }

    // Desde qué fecha entra en vigencia la modificación.
    public DateOnly AplicarDesde { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public EstadoTurno Estado { get; set; }
        = EstadoTurno.Activo;

    public List<ProfesorTurnoInputDto> Profesores { get; set; }
        = [];
}


public sealed record SesionClaseDto(
    Guid Id,
    Guid TurnoId,
    string Turno,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    int DuracionMinutos,
    TimeOnly HoraFin,
    Guid NivelId,
    string Nivel,
    int Cupo,
    int Inscriptos,
    int Vacantes,
    string? Sector,
    EstadoSesionClase Estado,
    string? MotivoCancelacion,
    Guid? DiaNoLaborableId,
    IReadOnlyList<ProfesorTurnoDto> Profesores);


public sealed record BebeSesionDto(
    Guid BebeId,
    Guid InscripcionId,
    string NombreCompleto,
    EstadoAsistencia? EstadoAsistencia,
    bool EsRecupero,
    // Cuotas vencidas con saldo pendiente, calculadas desde pagos reales.
    int CuotasVencidas = 0);


public sealed record SesionClaseDetalleDto(
    SesionClaseDto Sesion,
    IReadOnlyList<BebeSesionDto> Bebes);


public sealed record GeneracionSesionesDto(
    int Generadas,
    int YaExistentes);

    public sealed record NivelTurneraDto(
    Guid Id,
    string Nombre,
    int DuracionMinutos,
    int CupoPredeterminado,
    int ProfesoresMinimos,
    int ProfesoresMaximos);


public sealed record ProfesorDisponibleDto(
    Guid Id,
    string NombreCompleto);