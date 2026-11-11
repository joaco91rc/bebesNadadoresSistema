namespace BebesNadadores.Domain.Entities;

// Archivo histórico: nunca participa de cuotas, cupos ni inscripciones operativas.
public sealed class HistorialAlumnoLegacy
{
    public Guid Id { get; set; }
    public long LegacyPersonaId { get; set; }
    public Guid? BebeId { get; set; }
    public Bebe? Bebe { get; set; }
    public string NombreOriginal { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public DateOnly? FechaNacimiento { get; set; }
    public DateOnly? FechaPrimeraInscripcion { get; set; }
    public ICollection<HistorialAlumnoAnioLegacy> Anios { get; set; } = new List<HistorialAlumnoAnioLegacy>();
}
public sealed class HistorialAlumnoAnioLegacy
{
    public Guid Id { get; set; }
    public Guid HistorialAlumnoLegacyId { get; set; }
    public HistorialAlumnoLegacy HistorialAlumno { get; set; } = null!;
    public int Anio { get; set; }
    public DateOnly PrimeraClase { get; set; }
    public DateOnly UltimaClase { get; set; }
    public int CantidadRegistros { get; set; }
    public string? Niveles { get; set; }
    public int? FrecuenciaEstimada { get; set; }
}
