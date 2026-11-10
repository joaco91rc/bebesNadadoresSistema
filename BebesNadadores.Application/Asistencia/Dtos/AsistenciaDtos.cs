using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Asistencia.Dtos;

public sealed record AsistenciaAlumnoDto(
    Guid BebeId,
    Guid InscripcionId,
    string NombreCompleto,
    EstadoAsistencia Estado,
    bool EsRecupero,
    string? Observaciones,
    DateTimeOffset? RegistradaUtc,
    bool GeneroRecupero,
    DateOnly? RecuperoVence,
    // Se computan cuotas vencidas con saldo pendiente, incluidas las parcialmente pagadas.
    int CuotasVencidas = 0);


public sealed record AsistenciaSesionDto(
    Guid SesionClaseId,
    Guid TurnoId,
    string Turno,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    EstadoSesionClase EstadoSesion,
    int TotalEsperados,
    int Presentes,
    int Ausentes,
    int AusentesConAviso,
    int Pendientes,
    IReadOnlyList<AsistenciaAlumnoDto> Alumnos,
    string Nivel = "",
    IReadOnlyList<string>? Profesores = null);
public sealed record AsistenciaClaseDiaDto(
    Guid SesionClaseId,
    Guid TurnoId,
    string Turno,
    string Nivel,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    EstadoSesionClase EstadoSesion,
    int TotalEsperados,
    int Presentes,
    int Ausentes,
    int AusentesConAviso,
    int Pendientes,
    IReadOnlyList<string> Profesores);
public sealed class RegistrarAsistenciaDto
{
    public Guid SesionClaseId { get; set; }

    public Guid BebeId { get; set; }

    public EstadoAsistencia Estado { get; set; }

    public string? Observaciones { get; set; }

    public Guid? UsuarioId { get; set; }
}


public sealed class RegistrarAsistenciaAlumnoDto
{
    public Guid BebeId { get; set; }

    public EstadoAsistencia Estado { get; set; }

    public string? Observaciones { get; set; }
}


public sealed class RegistrarAsistenciaMasivaDto
{
    public Guid SesionClaseId { get; set; }

    public Guid? UsuarioId { get; set; }

    public List<RegistrarAsistenciaAlumnoDto> Alumnos { get; set; }
        = [];
}