using BebesNadadores.Application.Abstractions;
using BebesNadadores.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BebesNadadores.Infrastructure.Persistence.Auditoria;

/*
 * =============================================================
 * AUDITORÍA AUTOMÁTICA
 * =============================================================
 *
 * Antes de cada SaveChanges completa, en toda EntidadAuditable:
 *
 *   Alta:          CreadoPorUsuarioId   (si el servicio no lo cargó)
 *   Modificación:  ModificadoUtc + ModificadoPorUsuarioId
 *
 * y protege CreadoUtc / CreadoPorUsuarioId para que una
 * modificación nunca los pise.
 *
 * Así ningún servicio tiene que acordarse de cargarlo. Si un
 * servicio ya lo cargó a mano (por ejemplo con el usuario que
 * recibe por parámetro), se respeta.
 *
 * En los procesos automáticos (generación de cuotas, recuperos)
 * no hay usuario: queda null, que en pantalla se muestra como
 * "Sistema".
 */
public sealed class AuditoriaInterceptor(
    IUsuarioActual usuarioActual)
    : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var usuario =
                await usuarioActual.ObtenerAsync(cancellationToken);

            Completar(
                eventData.Context,
                usuario?.Id);
        }

        return await base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }


    /*
     * El sistema guarda siempre con SaveChangesAsync. Si algún día
     * se usa SaveChanges sincrónico, igual se completan las fechas
     * y se protege la creación (sin buscar el usuario, para no
     * bloquear el hilo).
     */
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Completar(
                eventData.Context,
                null);
        }

        return base.SavingChanges(
            eventData,
            result);
    }


    private static void Completar(
        DbContext context,
        Guid? usuarioId)
    {
        // El interceptor corre antes de que EF detecte los cambios.
        context.ChangeTracker.DetectChanges();

        var ahora =
            DateTimeOffset.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<EntidadAuditable>())
        {
            switch (entry.State)
            {
                case EntityState.Added:

                    entry.Entity.CreadoPorUsuarioId ??=
                        usuarioId;

                    break;


                case EntityState.Modified:

                    entry.Entity.ModificadoUtc =
                        ahora;

                    var cargadoPorElServicio =
                        entry.Property(x => x.ModificadoPorUsuarioId).IsModified
                        && entry.Entity.ModificadoPorUsuarioId.HasValue;

                    if (usuarioId.HasValue && !cargadoPorElServicio)
                    {
                        entry.Entity.ModificadoPorUsuarioId =
                            usuarioId;
                    }

                    // La creación no se toca nunca.
                    entry.Property(x => x.CreadoUtc).IsModified = false;
                    entry.Property(x => x.CreadoPorUsuarioId).IsModified = false;

                    break;
            }
        }
    }
}
