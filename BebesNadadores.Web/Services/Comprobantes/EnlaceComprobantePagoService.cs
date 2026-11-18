using Microsoft.AspNetCore.DataProtection;

namespace BebesNadadores.Web.Services.Comprobantes;

/// <summary>
/// Links con vigencia de 30 días para descargar recibos sin iniciar sesión.
/// No guardar archivos en una carpeta pública: el PDF se genera al solicitarlo.
/// </summary>
public sealed class EnlaceComprobantePagoService(IDataProtectionProvider dataProtection)
{
    private const string Proposito = "BebesNadadores.ComprobantePago.EnlacePublico.v1";
    private readonly ITimeLimitedDataProtector _protector = dataProtection
        .CreateProtector(Proposito)
        .ToTimeLimitedDataProtector();

    public string CrearEnlace(Guid pagoId, string baseUri)
    {
        if (pagoId == Guid.Empty)
            throw new ArgumentException("El pago no es válido.", nameof(pagoId));

        var token = _protector.Protect(pagoId.ToString("N"), TimeSpan.FromDays(30));
        return new Uri(new Uri(baseUri),
            "comprobantes/descargar/" + Uri.EscapeDataString(token)).AbsoluteUri;
    }

    public bool IntentarValidar(string? token, out Guid pagoId)
    {
        pagoId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            return false;

        try
        {
            return Guid.TryParseExact(_protector.Unprotect(token), "N", out pagoId)
                && pagoId != Guid.Empty;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false; // token falsificado o vencido
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
