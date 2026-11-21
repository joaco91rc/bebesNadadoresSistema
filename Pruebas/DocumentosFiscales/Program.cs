using BebesNadadores.Web.Services.Facturas;

int aprobadas = 0;
void Probar(string nombre, bool esperado, bool real)
{
    if (esperado != real) throw new Exception($"FALLÓ: {nombre}, esperado={esperado}, real={real}");
    Console.WriteLine($"OK: {nombre}");
    aprobadas++;
}

Probar("DNI siete dígitos", true, DocumentoFiscalReceptor.Valido("1234567", "DNI"));
Probar("DNI ocho dígitos", true, DocumentoFiscalReceptor.Valido("12345678", "DNI"));
Probar("DNI con puntos", true, DocumentoFiscalReceptor.Valido("12.345.678", "DNI"));
Probar("DNI de once dígitos rechazado", false, DocumentoFiscalReceptor.Valido("20123456786", "DNI"));
Probar("CUIL de ocho dígitos rechazado", false, DocumentoFiscalReceptor.Valido("12345678", "CUIL"));
var cuil = "20123456786";
// Construir un CUIL con dígito verificador para no depender de ejemplos externos.
int[] pesos = [5,4,3,2,7,6,5,4,3,2];
var baseCuil = "20" + "12345678";
var dv = 11 - Enumerable.Range(0,10).Sum(i=>(baseCuil[i]-'0')*pesos[i]) % 11;
if (dv == 11) dv = 0;
if (dv == 10) throw new Exception("Elegir otro DNI para la prueba de CUIL");
cuil = baseCuil + dv;
Probar("CUIL válido", true, DocumentoFiscalReceptor.Valido(cuil, "CUIL"));
Probar("CUIL con guiones", true, DocumentoFiscalReceptor.Valido($"{cuil[..2]}-{cuil[2..10]}-{cuil[10]}", "CUIL"));
Probar("CUIL con verificador inválido", false, DocumentoFiscalReceptor.Valido(baseCuil + ((dv+1)%10), "CUIL"));
// El documento principal y cada auxiliar se validan por separado.
// Un DNI auxiliar puede ser de otra persona que el CUIL principal (o viceversa).
Probar("DNI principal y CUIL auxiliar independientes", true,
    DocumentoFiscalReceptor.Valido("87654321") && DocumentoFiscalReceptor.Valido(cuil));
Probar("CUIL principal y DNI auxiliar de otro titular", true,
    DocumentoFiscalReceptor.Valido(cuil) && DocumentoFiscalReceptor.Valido("23456789"));
Probar("DNI principal y otro DNI auxiliar", true,
    DocumentoFiscalReceptor.Valido("12345678") && DocumentoFiscalReceptor.Valido("23456789"));
Console.WriteLine($"{aprobadas} pruebas aprobadas.");
