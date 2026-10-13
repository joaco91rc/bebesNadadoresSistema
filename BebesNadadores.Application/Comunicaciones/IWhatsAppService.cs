namespace BebesNadadores.Application.Comunicaciones;

public interface IWhatsAppService
{
    string CrearUrl(string telefono, string mensaje);
}
