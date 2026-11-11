namespace BebesNadadores.Migrador;

public sealed record LegacyPersona(long Id, string ApellidoNombre, string Domicilio, string TelefonoFijo,
    string TelefonoAlternativo, string Email, DateOnly? FechaNacimiento, string Observacion,
    string TelefonoPadre, string TelefonoMadre, bool Activo, DateTime? FechaInscripcion);

public sealed record LegacyResponsable(long AlumnoId, string Nombre, string Dni);
public sealed record LegacyClase(long Id, string Codigo, DateOnly Fecha, string Dia, string Hora, string NivelNombre, long PersonaId);
public sealed record LegacyPago(long Id, long PersonaId, int Mes, long TipoPagoId, decimal Importe, DateTime FechaPago, int AnioPago, int TurnosPago, decimal? Descuento, string TipoPago);
public sealed record LegacyMoroso(long Id, long PersonaId, int Mes, int Anio, decimal Importe, long? TipoPagoId);
public sealed record LegacyListaEspera(long Id, long PersonaId, int? TurnoId, long? TurnoPersonaId, DateOnly Fecha, string Codigo, long? PresenteAusenteId);
