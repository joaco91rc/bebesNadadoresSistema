using System.Runtime.CompilerServices;
using BebesNadadores.Application.Abstractions;
using BebesNadadores.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BebesNadadores.Web.Diagnostico;

/*
 * =============================================================
 * MANEJO DE ERRORES EN PANTALLA
 * =============================================================
 *
 * Todas las pantallas hacen:
 *
 *   catch (Exception ex)
 *   {
 *       _error = Errores.Mensaje(ex);
 *   }
 *
 * (Errores se inyecta en _Imports.razor para todos los componentes.)
 *
 * - Regla de negocio ("La caja ya está cerrada"): se muestra el
 *   mensaje tal cual y queda en el log como información.
 * - Error conocido de base de datos (duplicado, en uso, sin
 *   conexión): mensaje claro en español + log.
 * - Cualquier otro error: mensaje genérico con un código corto y el
 *   detalle completo (stack trace, pantalla, usuario) en el log.
 *   Con ese código se encuentra el error en logs/errores-*.log.
 */
public sealed class ManejadorErrores(
    ILogger<ManejadorErrores> logger,
    IUsuarioActual usuarioActual)
{
    public string Mensaje(
        Exception ex,
        [CallerMemberName] string operacion = "",
        [CallerFilePath] string archivo = "")
    {
        var pantalla =
            ObtenerPantalla(archivo);

        // -----------------------------------------------------
        // 1. Regla de negocio
        // -----------------------------------------------------

        if (EsReglaDeNegocio(ex))
        {
            logger.LogInformation(
                "Validación en {Pantalla}.{Operacion} ({Usuario}): {Mensaje}",
                pantalla,
                operacion,
                ObtenerUsuario(),
                ex.Message);

            return ex.Message;
        }


        if (ex is OperationCanceledException)
        {
            return "La operación se canceló. Volvé a intentar.";
        }


        // -----------------------------------------------------
        // 2. Errores conocidos de base de datos
        // -----------------------------------------------------

        var codigo =
            Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        var conocido =
            TraducirErrorConocido(ex);

        logger.LogError(
            ex,
            "Error [{Codigo}] en {Pantalla}.{Operacion} ({Usuario})",
            codigo,
            pantalla,
            operacion,
            ObtenerUsuario());

        return conocido is not null
            ? $"{conocido} (código {codigo})"
            : "Ocurrió un error inesperado y quedó registrado. " +
              "Volvé a intentar; si se repite, avisale al administrador " +
              $"con este código: {codigo}.";
    }


    /*
     * Es regla de negocio si:
     * - es ReglaNegocioException, o
     * - es InvalidOperationException (o UnauthorizedAccessException)
 *   lanzada por código del sistema
     *   (los servicios usan esa excepción con mensajes en español).
     *   Las que lanzan EF, Npgsql o LINQ ("Sequence contains no
     *   elements") NO cuentan: esas son errores técnicos.
     */
    public static bool EsReglaDeNegocio(
        Exception ex)
    {
        if (ex is ReglaNegocioException)
            return true;

        if (ex is not (InvalidOperationException or UnauthorizedAccessException))
            return false;

        var ensamblado =
            ex.TargetSite?.DeclaringType?.Assembly.GetName().Name;

        return ensamblado is not null
            && ensamblado.StartsWith(
                "BebesNadadores",
                StringComparison.Ordinal);
    }


    private static string? TraducirErrorConocido(
        Exception ex)
    {
        if (ex is DbUpdateConcurrencyException)
        {
            return "Otro usuario modificó estos datos al mismo tiempo. " +
                   "Recargá la pantalla y volvé a intentar.";
        }

        var postgres =
            Buscar<PostgresException>(ex);

        if (postgres is not null)
        {
            return postgres.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation =>
                    "Ya existe un registro con esos datos.",

                PostgresErrorCodes.ForeignKeyViolation =>
                    "No se puede completar porque el registro está relacionado con otros datos.",

                PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected =>
                    "El sistema estaba procesando otra operación al mismo tiempo. Volvé a intentar.",

                PostgresErrorCodes.StringDataRightTruncation =>
                    "Uno de los textos es demasiado largo.",

                _ => null
            };
        }

        if (Buscar<NpgsqlException>(ex) is not null
            || Buscar<TimeoutException>(ex) is not null)
        {
            return "No se pudo conectar con la base de datos. " +
                   "Probá de nuevo en unos minutos.";
        }

        return null;
    }


    private static T? Buscar<T>(
        Exception ex)
        where T : Exception
    {
        for (Exception? actual = ex; actual is not null; actual = actual.InnerException)
        {
            if (actual is T encontrado)
                return encontrado;
        }

        return null;
    }


    private string ObtenerUsuario()
    {
        try
        {
            /*
             * En Blazor Server el estado de autenticación ya está
             * resuelto, la tarea termina en el momento.
             */
            var tarea =
                usuarioActual.ObtenerAsync();

            return tarea.IsCompletedSuccessfully
                ? tarea.Result?.Nombre ?? "sin usuario"
                : "desconocido";
        }
        catch
        {
            return "desconocido";
        }
    }


    private static string ObtenerPantalla(
        string archivo)
    {
        if (string.IsNullOrWhiteSpace(archivo))
            return "Pantalla";

        var nombre =
            Path.GetFileName(archivo.Replace('\\', '/'));

        // Pagos.razor.cs -> Pagos ; Turnos.razor -> Turnos
        var punto =
            nombre.IndexOf('.');

        return punto > 0
            ? nombre[..punto]
            : nombre;
    }
}
