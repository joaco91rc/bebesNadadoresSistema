using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Infrastructure.Persistence;

namespace BebesNadadores.Web.Services.Comprobantes;

/// <summary>
/// Links con vigencia de 30 días para descargar recibos sin iniciar sesión.
/// No guardar archivos en una carpeta pública: el PDF se genera al solicitarlo.
/// </summary>
public sealed class EnlaceComprobantePagoService(IDataProtectionProvider dataProtection, IDbContextFactory<AppDbContext> factory)
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

    public async Task<string> CrearEnlaceDescriptivoAsync(Guid pagoId, string baseUri, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var pago = await db.Pagos.AsNoTracking().Include(x => x.Bebe).Include(x => x.Imputaciones).ThenInclude(x => x.Cuota)
            .SingleOrDefaultAsync(x => x.Id == pagoId && x.AnuladoUtc == null, ct);
        if (pago is null) throw new InvalidOperationException("Pago no disponible.");
        var nombre = ComprobantePagoService.NombreRecibo($"{pago.Bebe.Nombre} {pago.Bebe.Apellido}", pago.Imputaciones.Select(x => x.Cuota.Periodo));
        var token = _protector.Protect(pagoId.ToString("N"), TimeSpan.FromDays(30));
        return await BebesNadadores.Web.Services.EnlacesCortos.CrearAsync(factory, baseUri, nombre, "/comprobantes/descargar/" + Uri.EscapeDataString(token), 30, ct);
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
