using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Domain.Entities;

namespace BebesNadadores.Domain.Entities;

public class ReservaSesionClase : EntidadAuditable
{
    public Guid Id { get; set; }

    public Guid SesionClaseId { get; set; }

    public SesionClase SesionClase { get; set; } = null!;


    public Guid InscripcionId { get; set; }

    public Inscripcion Inscripcion { get; set; } = null!;


    public Guid BebeId { get; set; }

    public Bebe Bebe { get; set; } = null!;


    public TipoReservaSesion Tipo { get; set; }

    public EstadoReservaSesion Estado { get; set; }
        = EstadoReservaSesion.Activa;
}