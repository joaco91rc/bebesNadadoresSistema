using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class Nivel : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    public int EdadMinMeses { get; set; }
    public int EdadMaxMeses { get; set; }

    public int DuracionMinutos { get; set; }
    public int CupoPredeterminado { get; set; }

    public int ProfesoresMinimos { get; set; } = 1;
    public int ProfesoresMaximos { get; set; } = 1;

    public bool Activo { get; set; } = true;

    public ICollection<Turno> Turnos { get; set; } = new List<Turno>();
}
