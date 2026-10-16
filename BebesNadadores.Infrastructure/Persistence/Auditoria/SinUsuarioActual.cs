using BebesNadadores.Application.Abstractions;

namespace BebesNadadores.Infrastructure.Persistence.Auditoria;

/*
 * Implementación por defecto (procesos sin usuario: jobs, pruebas).
 * La Web la reemplaza por la que lee el usuario logueado.
 */
public sealed class SinUsuarioActual : IUsuarioActual
{
    public Task<UsuarioActualDto?> ObtenerAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult<UsuarioActualDto?>(null);
    }
}
