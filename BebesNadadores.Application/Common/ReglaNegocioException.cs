namespace BebesNadadores.Application.Common;

/*
 * Error de regla de negocio: el mensaje está pensado para que lo
 * lea la persona que usa el sistema y se muestra tal cual.
 *
 *   throw new ReglaNegocioException("La caja del día ya está cerrada.");
 *
 * Hereda de InvalidOperationException para que todo el código que
 * ya atrapa InvalidOperationException siga funcionando igual.
 *
 * Nota: los servicios existentes lanzan InvalidOperationException
 * con mensajes en español; ManejadorErrores también los trata como
 * reglas de negocio cuando los lanza código del sistema (no EF ni
 * Npgsql). Para código nuevo, usar esta clase.
 */
public sealed class ReglaNegocioException(string mensaje)
    : InvalidOperationException(mensaje);
