namespace BebesNadadores.Application.Bancos.Dtos;

public sealed class GuardarBancoDto
{
    public string Nombre { get; set; }
        = string.Empty;

    public bool Activo { get; set; }
        = true;
}