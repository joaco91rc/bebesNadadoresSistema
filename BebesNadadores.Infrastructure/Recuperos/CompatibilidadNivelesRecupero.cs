using System.Globalization;
using System.Text;

namespace BebesNadadores.Infrastructure.Services.Recuperos;

/// <summary>Compatibilidad UNIDIRECCIONAL autorizada por el natatorio.</summary>
internal static class CompatibilidadNivelesRecupero
{
    public static bool Permite(string? nivelOrigen, string? nivelDestino)
    {
        var origen = Normalizar(nivelOrigen);
        var destino = Normalizar(nivelDestino);
        if (origen.Length == 0 || destino.Length == 0) return false;
        if (origen == destino) return true;
        // 3 y 4 años puede recuperar con 3 a 6 años.
        // 5 y 6 años puede recuperar con 3 a 6 años.
        return EsGrupoMixto(destino) && (EsTresCuatro(origen) || EsCincoSeis(origen));
    }

    private static bool EsGrupoMixto(string s) =>
        s.Contains("3 a 6") || s.Contains("3 y 6") || s.Contains("3-6");

    private static bool EsTresCuatro(string s) =>
        s.Contains("3 y 4") || s.Contains("3 a 4") || s.Contains("3-4");

    private static bool EsCincoSeis(string s) =>
        s.Contains("5 y 6") || s.Contains("5 a 6") || s.Contains("5-6");

    private static string Normalizar(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var formD = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var t = new StringBuilder();
        foreach (var c in formD)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                t.Append(c);
        return string.Join(" ", t.ToString().Replace("anos", "").Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
