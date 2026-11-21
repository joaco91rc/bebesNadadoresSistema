namespace BebesNadadores.Web.Services.Cumpleanos;

/// <summary>
/// Nombre corto para saludos. No modifica los datos del legajo.
/// Cuando Nombre contiene nombre y apellido unidos, conserva solo el primer nombre.
/// </summary>
public static class NombreParaSaludo
{
    public static string Obtener(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return "";
        var partes = nombre.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return partes.Length == 0 ? "" : partes[0];
    }
}
