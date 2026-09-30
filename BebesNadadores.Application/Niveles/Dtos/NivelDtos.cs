namespace BebesNadadores.Application.Niveles.Dtos;

public sealed record NivelDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    int Orden,
    int EdadMinMeses,
    int EdadMaxMeses,
    int DuracionMinutos,
    int CupoPredeterminado,
    int ProfesoresMinimos,
    int ProfesoresMaximos,
    bool Activo,
    int TurnosActivos);


public sealed class GuardarNivelDto
{
    public string Nombre { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    public int Orden { get; set; }

    public int EdadMinMeses { get; set; }

    public int EdadMaxMeses { get; set; }

    public int DuracionMinutos { get; set; }

    public int CupoPredeterminado { get; set; }

    public int ProfesoresMinimos { get; set; }

    public int ProfesoresMaximos { get; set; }

    public bool Activo { get; set; } = true;
}