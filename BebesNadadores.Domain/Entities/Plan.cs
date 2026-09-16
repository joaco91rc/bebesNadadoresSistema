using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class Plan : EntidadAuditable
{
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int VecesPorSemana { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<TarifaPlan> Tarifas { get; set; } = new List<TarifaPlan>();
    public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
}
