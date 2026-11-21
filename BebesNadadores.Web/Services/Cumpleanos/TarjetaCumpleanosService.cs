using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Infrastructure.Persistence;

namespace BebesNadadores.Web.Services.Cumpleanos;

/// <summary>
/// Genera links temporales, individuales y firmados. No expone DNI ni teléfonos.
/// El endpoint público no requiere iniciar sesión, pero necesita token válido.
/// </summary>
public sealed class TarjetaCumpleanosService(
    IDataProtectionProvider dataProtection,
    IDbContextFactory<AppDbContext> factory,
    IWebHostEnvironment environment)
{
    private const string Proposito = "BebesNadadores.TarjetaCumpleanos.v1";
    private readonly ITimeLimitedDataProtector _protector = dataProtection
        .CreateProtector(Proposito).ToTimeLimitedDataProtector();

    public string CrearEnlace(Guid bebeId, string baseUri)
    {
        // La emisión está limitada a la acción interna del Dashboard autenticado.
        var token = _protector.Protect(bebeId.ToString("N"), TimeSpan.FromDays(7));
        return new Uri(new Uri(baseUri), "cumple/" + Uri.EscapeDataString(token)).AbsoluteUri;
    }

    public async Task<string> CrearEnlaceDescriptivoAsync(Guid bebeId, string baseUri, string nombre, int edad, CancellationToken ct = default)
    {
        var etiqueta = string.Concat(nombre.Select(c => char.IsLetterOrDigit(c) ? c : '-')).Trim('-');
        var token = _protector.Protect(bebeId.ToString("N"), TimeSpan.FromDays(7));
        return await BebesNadadores.Web.Services.EnlacesCortos.CrearAsync(factory, baseUri, $"{etiqueta}-cumple-{edad}", "/cumple/" + Uri.EscapeDataString(token), 7, ct);
    }

    public async Task<IResult> MostrarAsync(string token, HttpContext http, CancellationToken ct)
    {
        Guid bebeId;
        try
        {
            if (string.IsNullOrWhiteSpace(token) || token.Length > 2048 ||
                !Guid.TryParseExact(_protector.Unprotect(token), "N", out bebeId))
                return Results.NotFound();
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return Results.NotFound();
        }
        catch (FormatException)
        {
            return Results.NotFound();
        }

        await using var db = await factory.CreateDbContextAsync(ct);
        var bebe = await db.Bebes.AsNoTracking()
            .Where(x => x.Id == bebeId)
            .Select(x => new { x.Nombre, x.FechaNacimiento })
            .FirstOrDefaultAsync(ct);
        if (bebe is null) return Results.NotFound();

        var hoy = BebesNadadores.Domain.Common.FechaArgentina.Hoy;
        var edad = Math.Max(0, hoy.Year - bebe.FechaNacimiento.Year -
            (hoy.Month < bebe.FechaNacimiento.Month ||
             (hoy.Month == bebe.FechaNacimiento.Month && hoy.Day < bebe.FechaNacimiento.Day) ? 1 : 0));
        var nombre = WebUtility.HtmlEncode(NombreParaSaludo.Obtener(bebe.Nombre));
        var anios = edad == 1 ? "1 añito" : $"{edad} añitos";

        var ruta = Path.Combine(environment.WebRootPath ??
            Path.Combine(environment.ContentRootPath, "wwwroot"), "cumple", "tarjeta.html");
        if (!File.Exists(ruta)) return Results.Problem("No se encontró la plantilla del saludo.");
        var html = await File.ReadAllTextAsync(ruta, ct);
        var musica = File.Exists(Path.Combine(environment.WebRootPath ??
            Path.Combine(environment.ContentRootPath, "wwwroot"), "audio", "cumpleanos.mp3"));
        var origen = $"{http.Request.Scheme}://{http.Request.Host}";
        html = html.Replace("__NOMBRE__", nombre, StringComparison.Ordinal)
            .Replace("__EDAD__", WebUtility.HtmlEncode(anios), StringComparison.Ordinal)
            .Replace("__OG_IMAGE__", WebUtility.HtmlEncode(origen + "/images/saludo-cumpleanos.png"), StringComparison.Ordinal)
            .Replace("__MUSICA__", musica ? "true" : "false", StringComparison.Ordinal);
        http.Response.Headers.CacheControl = "private, no-store, max-age=0";
        http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        http.Response.Headers["X-Content-Type-Options"] = "nosniff";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        return Results.Content(html, "text/html; charset=utf-8");
    }
}
