namespace BebesNadadores.Application.Recuperos;

/*
 * Recuperos por clases que no se dictaron (feriado, cierre del
 * natatorio o cancelación manual).
 *
 * El recupero se genera recién cuando llega el día de la clase
 * perdida. Por eso, además de generarse al guardar un feriado o
 * al cancelar una clase, un job diario revisa los días que ya
 * pasaron: así un feriado cargado con meses de anticipación
 * genera sus recuperos cuando corresponde.
 */
public interface IRecuperosClasesNoDictadasService
{
    /*
     * Genera los recuperos que falten de los últimos días
     * (idempotente: si ya existen, no los duplica).
     */
    Task<int> GenerarPendientesAsync(
        CancellationToken ct = default);
}
