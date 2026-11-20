using System.Globalization;
using System.Text;

namespace BebesNadadores.Infrastructure.Services.Recuperos;

/// <summary>Los tres grupos de 3 a 6 años son compatibles entre sí para recuperos.</summary>
internal static class CompatibilidadNivelesRecupero
{
    public static bool Permite(string? nivelOrigen, string? nivelDestino)
    {
        var origen = Normalizar(nivelOrigen);
        var destino = Normalizar(nivelDestino);
        if (origen.Length == 0 || destino.Length == 0) return false;
        if (origen == destino) return true;
        // Los niveles de bebés y 2 años sólo admiten su mismo nivel.
        return EsGrupoMayor(origen) && EsGrupoMayor(destino);
    }

    private static bool EsGrupoMayor(string s) => EsGrupoMixto(s) || EsTresCuatro(s) || EsCincoSeis(s);

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
