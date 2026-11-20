using BebesNadadores.Web.Services.Facturas;
var a = Guid.NewGuid();
var b = Guid.NewGuid();
var c = Guid.NewGuid();
PagoFiscalCandidato P(Guid bebe, decimal importe) => new(Guid.NewGuid(), bebe, importe);
void Verificar(string nombre, IReadOnlyList<PagoFiscalCandidato> pagos, decimal importe, params Guid[] esperados)
{
    var resultado = ConciliadorFacturas.BuscarUnica(pagos, importe);
    bool ok = esperados.Length == 0 ? resultado == null : resultado != null &&
        resultado.Select(p => p.BebeId).ToHashSet().SetEquals(esperados);
    if (!ok) throw new Exception("Falló: " + nombre);
    Console.WriteLine("OK: " + nombre);
}
var hermanos = new[] { P(a, 80000), P(b, 70000) };
Verificar("Juntos con descuento", hermanos, 150000, a, b);
Verificar("Separado primero", hermanos, 80000, a);
Verificar("Separado segundo", hermanos, 70000, b);
Verificar("Mismo importe ambiguo", new[] { P(a, 80000), P(b, 80000) }, 80000);
Verificar("Mismo importe juntos", new[] { P(a, 80000), P(b, 80000) }, 160000, a, b);
Verificar("Tres hermanos", new[] { P(a, 80000), P(b, 70000), P(c, 60000) }, 210000, a, b, c);
Verificar("Sin coincidencia", hermanos, 140000);
Verificar("Dos combinaciones distintas", new[] { P(a, 80000), P(b, 50000), P(c, 30000) }, 80000);
Verificar("Pago fraccionado mismo alumno", new[] { P(a, 30000), P(a, 50000), P(b, 70000) }, 80000, a);
Verificar("Sin pagos", Array.Empty<PagoFiscalCandidato>(), 80000);
Verificar("Importe cero", hermanos, 0);
Verificar("Importe exacto con centavos", new[] { P(a, 80000.15m), P(b, 70000.25m) }, 150000.40m, a, b);
Verificar("Limite de volumen", Enumerable.Range(0,19).Select(_=>P(a,1000)).ToArray(), 19000);
