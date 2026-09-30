using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Bebes.Dtos;

public sealed record ResponsableLegajoDto(
    Guid Id,
    string NombreCompleto,
    string? Dni,
    string Telefono,
    string WhatsApp,
    string Email,
    string? Domicilio,
    RelacionResponsable Relacion,
    bool EsContactoPrincipal,
    bool AutorizaComunicaciones);


public sealed record TurnoLegajoDto(
    Guid TurnoId,
    string Nombre,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string Nivel,
    string? Sector,
    bool EsPrincipal,
    IReadOnlyList<string> Profesores);


public sealed record InscripcionLegajoDto(
    Guid Id,

    Guid PlanId,
    string Plan,
    int VecesPorSemana,
    decimal? TarifaMensual,

    DateOnly FechaInicio,
    DateOnly? FechaFin,

    EstadoInscripcion Estado,
    MedioPago MedioPagoPreferido,

    TipoDescuento? TipoDescuento,
    decimal? ValorDescuento,
    string? MotivoDescuento,

    string? Observaciones,

    IReadOnlyList<TurnoLegajoDto> Turnos);


public sealed record ProximaClaseLegajoDto(
    Guid TurnoId,
    DateOnly Fecha,
    DiaSemana DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string Nivel,
    string? Sector,
    IReadOnlyList<string> Profesores);


public sealed record AptoMedicoLegajoDto(
    Guid Id,
    DateOnly FechaRevision,
    DateOnly VigenciaHasta,
    ResultadoAptoMedico Resultado,
    string? Observaciones,
    Guid MedicoUsuarioId,
    string? DocumentoStorageKey,
    string? DocumentoNombreOriginal,
    bool Vigente);


public sealed record AsistenciaResumenLegajoDto(
    int Total,
    int Presentes,
    int AusentesConAviso,
    int Ausentes,
    int Pendientes,
    int Recuperos,
    int PorcentajeAsistencia);


public sealed record AsistenciaLegajoDto(
    Guid AsistenciaId,
    Guid SesionClaseId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    string Nivel,
    EstadoAsistencia Estado,
    bool EsRecupero,
    string? Observaciones);


public sealed record RecuperacionLegajoDto(
    Guid Id,
    TipoRecuperacion Tipo,
    EstadoRecuperacion Estado,
    DateOnly FechaGeneracion,
    DateOnly? FechaVencimiento,
    bool SinVencimiento,
    bool Vencida,
    DateOnly FechaClaseOrigen,
    string NivelOrigen,
    Guid? SesionUsoId,
    DateOnly? FechaUso,
    string? NivelUso);


public sealed record PagoLegajoDto(
    Guid Id,
    DateTimeOffset FechaUtc,
    decimal Importe,
    MedioPago MedioPago,
    string? ReferenciaExterna,
    string? Observaciones,
    string? Responsable);


public sealed record BebeLegajoDto(
    Guid Id,
    string Nombre,
    string Apellido,
    string NombreCompleto,
    DateOnly FechaNacimiento,
    Sexo Sexo,
    string? Dni,
    DateOnly FechaAlta,
    EstadoBebe Estado,
    DateOnly? FechaBaja,
    string? MotivoBaja,
    Guid? NivelActualId,
    string? NivelActual,
    int EdadMeses,
    int EdadAnios,
    bool AlcanzoEdadMaxima,

    ResponsableLegajoDto? ResponsablePrincipal,
    IReadOnlyList<ResponsableLegajoDto> Responsables,

    InscripcionLegajoDto? InscripcionActual,
    ProximaClaseLegajoDto? ProximaClase,

    AptoMedicoLegajoDto? AptoActual,
    IReadOnlyList<AptoMedicoLegajoDto> AptosMedicos,

    AsistenciaResumenLegajoDto AsistenciaResumen,
    IReadOnlyList<AsistenciaLegajoDto> Asistencias,

    IReadOnlyList<RecuperacionLegajoDto> Recuperaciones,

    decimal PagadoMesActual,
    IReadOnlyList<PagoLegajoDto> Pagos);