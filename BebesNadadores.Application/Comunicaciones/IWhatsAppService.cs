namespace BebesNadadores.Application.Comunicaciones;

public interface IWhatsAppService
{
    /*
     * Link a WhatsApp Web con el mensaje cargado.
     * Abrilo con la función JS abrirWhatsAppBebesNadadores(url):
     * en el celular / tablet la convierte para abrir la app.
     */
    string CrearUrl(string telefono, string mensaje);

    /*
     * Link para abrir el chat (sin mensaje). wa.me abre la app en
     * el celular y WhatsApp Web / Desktop en la PC.
     * Devuelve null si el número no es válido.
     */
    string? CrearUrlChat(string? telefono);
}
