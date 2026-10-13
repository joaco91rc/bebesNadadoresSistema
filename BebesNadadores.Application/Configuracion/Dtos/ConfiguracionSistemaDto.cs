namespace BebesNadadores.Application.Configuracion.Dtos;

public sealed record ConfiguracionSistemaDto(
    Guid Id,
    string NombreInstitucion,
    string? WhatsApp,
    string? Direccion,
    string? LogoUrl,
    int DiasVigenciaRecupero,
    int MesesVigenciaApto,
    int DiasAlertaApto,
    int DiaVencimientoCuota,
    int EdadMaximaMeses);

public sealed class GuardarInstitucionDto
{
    public string NombreInstitucion { get; set; } = string.Empty;
    public string? WhatsApp { get; set; }
    public string? Direccion { get; set; }
    public string? LogoUrl { get; set; }
}

public sealed class GuardarReglasSistemaDto
{
    public int DiasVigenciaRecupero { get; set; } = 45;
    public int MesesVigenciaApto { get; set; } = 1;
    public int DiasAlertaApto { get; set; } = 15;
    public int DiaVencimientoCuota { get; set; } = 10;
    public int EdadMaximaMeses { get; set; } = 84;
}
