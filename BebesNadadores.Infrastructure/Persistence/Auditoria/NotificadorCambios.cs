using BebesNadadores.Application.Abstractions;

namespace BebesNadadores.Infrastructure.Persistence.Auditoria;

public sealed class NotificadorCambios : INotificadorCambios
{
    public event Action? DatosGuardados;

    public void Notificar()
    {
        try
        {
            DatosGuardados?.Invoke();
        }
        catch
        {
            // Un componente que escucha nunca puede romper un guardado.
        }
    }
}
