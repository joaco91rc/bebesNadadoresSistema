namespace BebesNadadores.Web.Services.Facturas;

public sealed record PagoFiscalCandidato(Guid Id, Guid BebeId, decimal Importe);

public static class ConciliadorFacturas
{
    // Exactitud a centavos. Si existen dos combinaciones de pagos, no elegir al azar.
    // Acotar el trabajo: 18 pagos son suficientes para el caso normal de una familia.
    public static List<PagoFiscalCandidato>? BuscarUnica(IReadOnlyList<PagoFiscalCandidato> pagos, decimal importe)
    {
        if (importe <= 0 || pagos.Count == 0 || pagos.Count > 18 ||
            pagos.Any(p => p.Importe <= 0) || pagos.Select(p => p.Id).Distinct().Count() != pagos.Count)
            return null;
        List<PagoFiscalCandidato>? resultado = null;
        var actual = new List<PagoFiscalCandidato>();
        bool ambigua = false;
        void Buscar(int indice, decimal total)
        {
            if (ambigua || total > importe) return;
            if (total == importe)
            {
                if (resultado != null) ambigua = true;
                else resultado = new(actual);
                return;
            }
            if (indice == pagos.Count) return;
            actual.Add(pagos[indice]);
            Buscar(indice + 1, total + pagos[indice].Importe);
            actual.RemoveAt(actual.Count - 1);
            Buscar(indice + 1, total);
        }
        Buscar(0, 0m);
        return ambigua ? null : resultado;
    }
}
