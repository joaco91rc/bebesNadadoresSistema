using BebesNadadores.Application.Recuperos;

namespace BebesNadadores.Infrastructure.Services.Recuperos;

// Se conserva la interfaz para los servicios y jobs existentes. El inicio
// a mitad de mes solo modifica la tarifa elegida; no genera recuperos.
// Los recuperos historicos ya registrados no se modifican aqui.
public sealed class GeneradorRecuperosInicioMitadMes : IRecuperosInicioMitadMesService
{
    public Task<int> GenerarPendientesAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(0);
    }
}
