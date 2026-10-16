namespace BebesNadadores.Application.Busqueda;

/*
 * Buscador de la barra superior.
 *
 * Busca bebés por: nombre, apellido o DNI del bebé; nombre,
 * apellido, DNI, teléfono, WhatsApp o email de un responsable; y
 * por número de comprobante de un pago (referencia).
 *
 * No distingue mayúsculas ni acentos ("perez" encuentra "Pérez").
 * Con varias palabras, todas tienen que aparecer ("ana gom").
 */
public interface IBusquedaGlobalService
{
    Task<IReadOnlyList<ResultadoBusquedaDto>> BuscarAsync(
        string texto,
        int maximo = 8,
        CancellationToken ct = default);
}


public enum TipoResultadoBusqueda
{
    Bebe = 1,
    Responsable = 2,
    Comprobante = 3
}


/*
 * Siempre apunta a un bebé (legajo / pagos).
 * Detalle explica por qué coincidió.
 */
public sealed record ResultadoBusquedaDto(
    TipoResultadoBusqueda Tipo,
    Guid BebeId,
    string Titulo,
    string Detalle,
    bool DadoDeBaja);
