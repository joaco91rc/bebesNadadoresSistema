namespace BebesNadadores.Domain.Common;

/// <summary>Fecha operativa argentina, independiente de la zona horaria del VPS.</summary>
public static class FechaArgentina
{
    public static DateOnly Hoy
    {
        get
        {
            try {
                var zona = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
                return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zona));
            } catch {
                return DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
            }
        }
    }
}
