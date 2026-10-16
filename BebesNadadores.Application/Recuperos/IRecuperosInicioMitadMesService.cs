namespace BebesNadadores.Application.Recuperos;

/*
 * Recupero de regalo para quien arranca a mitad de mes
 * (ver ReglasCuota). Se genera con origen en la primera clase del
 * bebé; si esa clase todavía no existe en la turnera, lo genera el
 * job diario cuando aparece.
 */
public interface IRecuperosInicioMitadMesService
{
    Task<int> GenerarPendientesAsync(
        CancellationToken ct = default);
}
