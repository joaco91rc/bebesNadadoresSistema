namespace BebesNadadores.Web.Services.Facturas;

/// <summary>
/// Documento fiscal del RECEPTOR. El CUIT del emisor se configura por separado.
/// Admitimos DNI (7 u 8 dígitos) y CUIL (11 dígitos con verificador correcto).
/// </summary>
public static class DocumentoFiscalReceptor
{
    public static string SoloDigitos(string? valor) =>
        new((valor ?? string.Empty).Where(c => c is >= '0' and <= '9').ToArray());

    public static string Tipo(string? valor) => SoloDigitos(valor).Length == 11 ? "CUIL" : "DNI";

    public static bool DniValido(string? valor)
    {
        var doc = SoloDigitos(valor);
        return doc.Length is 7 or 8;
    }

    public static bool CuilValido(string? valor)
    {
        var doc = SoloDigitos(valor);
        if (doc.Length != 11) return false;
        int[] pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var verificador = 11 - Enumerable.Range(0, 10).Sum(i => (doc[i] - '0') * pesos[i]) % 11;
        if (verificador == 11) verificador = 0;
        // Si el resto es 10 no hay dígito verificador decimal válido.
        return verificador == doc[10] - '0';
    }

    public static bool Valido(string? valor, string? tipo = null) => tipo switch
    {
        "DNI" => DniValido(valor),
        "CUIL" => CuilValido(valor),
        null or "" => DniValido(valor) || CuilValido(valor),
        _ => false
    };

}
