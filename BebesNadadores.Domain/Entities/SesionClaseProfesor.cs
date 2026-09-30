namespace BebesNadadores.Domain.Entities;

public class SesionClaseProfesor
{
    public Guid SesionClaseId { get; set; }

    public SesionClase SesionClase { get; set; } = null!;


    public Guid ProfesorId { get; set; }

    public Profesor Profesor { get; set; } = null!;


    public bool EsPrincipal { get; set; }


    // =========================================================
    // CONTROL DOCENTE
    // =========================================================

    /// <summary>
    /// Indica que este profesor fue agregado como reemplazo
    /// mediante una modificación puntual de la clase.
    /// </summary>
    public bool EsReemplazo { get; set; }


    /// <summary>
    /// La administrativa confirmó el control docente.
    /// </summary>
    public bool ControlConfirmado { get; set; }


    /// <summary>
    /// Confirma que el profesor efectivamente dictó la clase.
    /// </summary>
    public bool DictoClase { get; set; }


    public DateTimeOffset? ConfirmadoUtc { get; set; }


    public Guid? ConfirmadoPorUsuarioId { get; set; }

    public Guid? ReemplazaAProfesorId { get; set; }
}