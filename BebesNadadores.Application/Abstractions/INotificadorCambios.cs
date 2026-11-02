namespace BebesNadadores.Application.Abstractions;

/*
 * Avisa que se guardaron datos en la sesión del usuario.
 *
 * Lo dispara la auditoría (AuditoriaInterceptor) después de cada
 * guardado en la base. Lo escuchan los componentes que muestran
 * información calculada, como la campana de avisos, para
 * actualizarse solos sin que el usuario tenga que tocar nada.
 *
 * Es Scoped: en Blazor Server cada usuario conectado tiene el suyo,
 * así que solo se entera de sus propios cambios. Los cambios de
 * otros usuarios los toma la campana con su actualización periódica.
 */
public interface INotificadorCambios
{
    event Action? DatosGuardados;

    void Notificar();
}
