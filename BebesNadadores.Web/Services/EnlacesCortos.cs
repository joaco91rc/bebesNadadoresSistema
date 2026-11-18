using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Infrastructure.Persistence;

namespace BebesNadadores.Web.Services;

public static class EnlacesCortos
{
    public static async Task<string> CrearAsync(IDbContextFactory<AppDbContext> factory, string baseUri, string etiqueta, string destino, int dias, CancellationToken ct = default)
    {
        var limpio = new string(etiqueta.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).Select(c => char.IsAsciiLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray());
        limpio = string.Join("-", limpio.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (limpio.Length > 120) limpio = limpio[..120];
        var vence = DateTimeOffset.UtcNow.AddDays(dias);
        await using var db = await factory.CreateDbContextAsync(ct);
        for (var intento = 0; intento < 20; intento++)
        {
            var codigo = Convert.ToBase64String(RandomNumberGenerator.GetBytes(3)).Replace('+', '-').Replace('/', '_');
            var slug = limpio + "-" + codigo;
            var insertados = await db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO "EnlacesCortosPublicos" ("Slug", "Destino", "VenceUtc") VALUES ({slug}, {destino}, {vence}) ON CONFLICT ("Slug") DO NOTHING""", ct);
            if (insertados == 1)
                return new Uri(new Uri(baseUri), "e/" + slug).AbsoluteUri;
        }
        throw new InvalidOperationException("No se pudo generar un enlace único. Volvé a intentar.");
    }

    public static async Task<IResult> ResolverAsync(string slug, IDbContextFactory<AppDbContext> factory, HttpContext http, CancellationToken ct)
    {
        http.Response.Headers.CacheControl = "private, no-store";
        http.Response.Headers["Referrer-Policy"] = "no-referrer";
        http.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
        if (slug.Length > 160) return Results.NotFound();
        await using var db = await factory.CreateDbContextAsync(ct);
        var ahora = DateTimeOffset.UtcNow;
        var destino = await db.Database.SqlQuery<string>($"""SELECT "Destino" AS "Value" FROM "EnlacesCortosPublicos" WHERE "Slug" = {slug} AND "VenceUtc" > {ahora}""").SingleOrDefaultAsync(ct);
        if (destino is null || !(destino.StartsWith("/cumple/", StringComparison.Ordinal) || destino.StartsWith("/comprobantes/descargar/", StringComparison.Ordinal))) return Results.NotFound();
        return Results.LocalRedirect(destino);
    }
}
