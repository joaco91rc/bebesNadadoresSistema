namespace BebesNadadores.Application.CiclosLectivos.Dtos;

public sealed class GuardarCicloLectivoDto
{
    public int Anio { get; set; }

    public DateOnly FechaInicio { get; set; }

    public DateOnly FechaFin { get; set; }

    public string? Observaciones { get; set; }
}