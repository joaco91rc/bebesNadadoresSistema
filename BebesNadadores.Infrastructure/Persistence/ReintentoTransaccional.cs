using Npgsql;

namespace BebesNadadores.Infrastructure.Persistence;

/*
 * =============================================================
 * REINTENTO DE TRANSACCIONES SERIALIZABLE
 * =============================================================
 *
 * En PostgreSQL una transacción Serializable puede FALLAR A
 * PROPÓSITO cuando otra transacción concurrente toca datos
 * relacionados:
 *
 *   40001  serialization_failure  -> "could not serialize access..."
 *   40P01  deadlock_detected
 *
 * No es un error del dato: la base pide que la operación se
 * vuelva a ejecutar desde cero. Este helper hace exactamente eso.
 *
 * Uso (cada intento crea su propio DbContext y su transacción,
 * porque el método interno los crea):
 *
 *   public Task<Guid> RegistrarAsync(Dto dto, CancellationToken ct = default) =>
 *       ReintentoTransaccional.EjecutarAsync(
 *           () => RegistrarInternoAsync(dto, ct),
 *           ct);
 *
 * Importante: dentro del método interno NO hacer
 *   catch { await transaction.RollbackAsync(); throw; }
 * Si la transacción ya está abortada, el Rollback explota y
 * tapa el error original. El "await using" ya hace rollback.
 */
public static class ReintentoTransaccional
{
    public const int MaximoIntentos = 4;


    public static async Task<T> EjecutarAsync<T>(
        Func<Task<T>> operacion,
        CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await operacion();
            }
            catch (Exception ex) when (EsConflictoDeConcurrencia(ex))
            {
                if (intento >= MaximoIntentos)
                {
                    throw new InvalidOperationException(
                        "El sistema estaba procesando otra operación al mismo tiempo " +
                        "y no pudo completar la suya. Volvé a intentar en unos segundos.",
                        ex);
                }

                /*
                 * Espera corta y creciente con algo de azar para que
                 * dos operaciones que chocaron no vuelvan a chocar.
                 */
                var espera =
                    TimeSpan.FromMilliseconds(
                        (40 * intento) + Random.Shared.Next(0, 60));

                await Task.Delay(
                    espera,
                    ct);
            }
        }
    }


    public static Task EjecutarAsync(
        Func<Task> operacion,
        CancellationToken ct)
    {
        return EjecutarAsync(
            async () =>
            {
                await operacion();
                return true;
            },
            ct);
    }


    /*
     * El error puede venir directo (PostgresException) o envuelto
     * por EF (DbUpdateException / InvalidOperationException
     * "transient failure"): se recorre toda la cadena.
     */
    public static bool EsConflictoDeConcurrencia(
        Exception? ex)
    {
        for (var actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual is PostgresException
                {
                    SqlState:
                        PostgresErrorCodes.SerializationFailure
                        or PostgresErrorCodes.DeadlockDetected
                })
            {
                return true;
            }
        }

        return false;
    }
}
