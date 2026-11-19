namespace BebesNadadores.Domain.Common;

/// <summary>Una primera clase puntual puede anticiparse hasta el mes calendario anterior.
/// La fecha contractual y el período de la cuota no se modifican.</summary>
public static class PrimeraClaseAnticipada
{
    public static DateOnly FechaMinima(DateOnly inicioInscripcion, DateOnly hoy)
    {
        var mesInicio = new DateOnly(inicioInscripcion.Year, inicioInscripcion.Month, 1);
        var limite = mesInicio == DateOnly.MinValue ? mesInicio : mesInicio.AddMonths(-1);
        return hoy > limite ? hoy : limite;
    }
}
