using BebesNadadores.Domain.Common;

namespace BebesNadadores.Domain.Entities;

public class ConfiguracionSistema : EntidadAuditable
{
    public static readonly Guid RegistroUnicoId =
        Guid.Parse("7e7542b7-3654-4b1b-a7dd-b3bc9d135a31");

    public string NombreInstitucion { get; set; } = "Bebés Nadadores";
    public string? WhatsApp { get; set; }
    public string? Direccion { get; set; }
    public string? LogoUrl { get; set; }

    public int DiasVigenciaRecupero { get; set; } = 45;
    public int MesesVigenciaApto { get; set; } = 1;
    public int DiasAlertaApto { get; set; } = 15;
    public int DiaVencimientoCuota { get; set; } = 10;
    public int EdadMaximaMeses { get; set; } = 84;
}
