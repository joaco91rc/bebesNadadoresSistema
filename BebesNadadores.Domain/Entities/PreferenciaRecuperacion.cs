using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class PreferenciaRecuperacion : EntidadAuditable
{
    public Guid BebeId { get; set; }
    public Bebe Bebe { get; set; } = null!;

    public DiaSemana DiaSemana { get; set; }
    public FranjaHoraria FranjaHoraria { get; set; } = FranjaHoraria.Indistinta;

    public TimeOnly? HoraDesde { get; set; }
    public TimeOnly? HoraHasta { get; set; }

    public bool Activa { get; set; } = true;
}
