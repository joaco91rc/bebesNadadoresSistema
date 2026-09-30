using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Domain.Entities;

public class Recuperacion : EntidadAuditable
{
    public Guid BebeId { get; set; }

    public Bebe Bebe { get; set; } = null!;


    public Guid InscripcionId { get; set; }

    public Inscripcion Inscripcion { get; set; } = null!;


    public Guid SesionOrigenId { get; set; }

    public SesionClase SesionOrigen { get; set; } = null!;


    /*
     * Es nullable porque un recupero generado por
     * feriado/cierre NO tiene asistencia origen.
     */
    public Guid? AsistenciaOrigenId { get; set; }

    public Asistencia? AsistenciaOrigen { get; set; }


    public TipoRecuperacion Tipo { get; set; }


    public DateOnly FechaGeneracion { get; set; }


    /*
     * AusenciaConAviso:
     * FechaGeneracion + 45 días.
     *
     * ClaseNoDictada:
     * null = sin vencimiento.
     */
    public DateOnly? FechaVencimiento { get; set; }


    public EstadoRecuperacion Estado { get; set; }
        = EstadoRecuperacion.Disponible;


    public Guid? SesionUsoId { get; set; }

    public SesionClase? SesionUso { get; set; }


    public DateTimeOffset? UtilizadaUtc { get; set; }


    public bool SinVencimiento =>
        !FechaVencimiento.HasValue;


    public bool EstaVencida(DateOnly hoy)
    {
        if (!FechaVencimiento.HasValue)
            return false;

        return
            Estado is
                EstadoRecuperacion.Disponible or
                EstadoRecuperacion.Reservada
            &&
            hoy > FechaVencimiento.Value;
    }


    public void MarcarUtilizada(
        Guid sesionUsoId)
    {
        Estado =
            EstadoRecuperacion.Utilizada;

        SesionUsoId =
            sesionUsoId;

        UtilizadaUtc =
            DateTimeOffset.UtcNow;
    }
}