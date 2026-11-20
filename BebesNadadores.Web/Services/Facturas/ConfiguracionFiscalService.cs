using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Web.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Web.Services.Facturas;

public sealed class ConfiguracionFiscalDatos
{
    public string CuitEmisor { get; set; } = "";
    public string RazonSocial { get; set; } = "Bebés Nadadores";
    public string CorreoInstitucional { get; set; } = "administracion@bebesnadadores.com";
    public int DiasTolerancia { get; set; }
    public bool AsociacionAutomatica { get; set; } = true;
    public bool ConciliarHermanos { get; set; } = true;
    public DateTimeOffset? ModificadoUtc { get; set; }
    public string? ModificadoPor { get; set; }
}

public sealed class ConfiguracionFiscalService(IDbContextFactory<AppDbContext> factory,
    IConfiguration config, IAuthorizationService authorization)
{
    private const string Columnas = """
        SELECT "CuitEmisor", "RazonSocial", "CorreoInstitucional", "DiasTolerancia",
        "AsociacionAutomatica", "ConciliarHermanos", "ModificadoUtc", "ModificadoPor"
        FROM "ConfiguracionFiscal" WHERE "Id"=1
        """;

    public static string NormalizarCuit(string? valor) => (valor ?? "").Trim().Replace("-", "").Replace(" ", "");
    public static bool CuitValido(string? valor)
    {
        var cuit = NormalizarCuit(valor);
        if (cuit.Length != 11 || cuit.Any(c => c < '0' || c > '9') || cuit.All(c => c == '0')) return false;
        int[] pesos = [5,4,3,2,7,6,5,4,3,2];
        int verificador = (11 - Enumerable.Range(0,10).Sum(i => (cuit[i]-'0')*pesos[i]) % 11) % 11;
        return verificador == cuit[10]-'0';
    }

    public async Task<ConfiguracionFiscalDatos> ObtenerAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        return await ObtenerAsync(db);
    }

    internal async Task<ConfiguracionFiscalDatos> ObtenerAsync(AppDbContext db)
    {
        var filas = await db.Database.SqlQueryRaw<ConfiguracionFiscalDatos>(Columnas).ToListAsync();
        return filas.SingleOrDefault() ?? new ConfiguracionFiscalDatos
        {
            CuitEmisor = NormalizarCuit(config["Facturas:CuitEmisor"])
        };
    }

    public async Task GuardarAsync(ConfiguracionFiscalDatos datos, ClaimsPrincipal usuario)
    {
        if (!(await authorization.AuthorizeAsync(usuario, PoliticasSistema.SoloAdministrador)).Succeeded)
            throw new UnauthorizedAccessException("Solo un administrador puede modificar estos datos.");
        var cuit = NormalizarCuit(datos.CuitEmisor);
        var razon = datos.RazonSocial.Trim();
        var correo = datos.CorreoInstitucional.Trim();
        if (!CuitValido(cuit)) throw new InvalidOperationException("Ingresá un CUIT válido de 11 dígitos, incluido su dígito verificador.");
        if (razon.Length is < 1 or > 200) throw new InvalidOperationException("Ingresá una razón social de hasta 200 caracteres.");
        if (correo.Length > 254 || !MailAddress.TryCreate(correo, out var direccion) || !string.Equals(direccion.Address, correo, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ingresá un correo institucional válido.");
        if (datos.DiasTolerancia is < 0 or > 30) throw new InvalidOperationException("La tolerancia debe estar entre 0 y 30 días.");
        var idUsuario = usuario.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException("No se pudo identificar al usuario.");
        var actor = $"{usuario.Identity?.Name ?? "Administrador"} ({idUsuario})";
        await using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();
        // Serializa incluso la primera escritura (cuando todavía no existe la fila).
        await db.Database.ExecuteSqlRawAsync("LOCK TABLE \"ConfiguracionFiscal\" IN SHARE ROW EXCLUSIVE MODE");
        var anterior = await ObtenerAsync(db);
        if (anterior.ModificadoUtc != datos.ModificadoUtc)
            throw new InvalidOperationException("Otro administrador modificó la configuración. Actualizá la pantalla antes de guardar.");
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ConfiguracionFiscal" ("Id","CuitEmisor","RazonSocial","CorreoInstitucional","DiasTolerancia","AsociacionAutomatica","ConciliarHermanos","ModificadoUtc","ModificadoPor")
            VALUES (1,{cuit},{razon},{correo},{datos.DiasTolerancia},{datos.AsociacionAutomatica},{datos.ConciliarHermanos},clock_timestamp(),{actor})
            ON CONFLICT ("Id") DO UPDATE SET "CuitEmisor"=EXCLUDED."CuitEmisor", "RazonSocial"=EXCLUDED."RazonSocial",
            "CorreoInstitucional"=EXCLUDED."CorreoInstitucional", "DiasTolerancia"=EXCLUDED."DiasTolerancia",
            "AsociacionAutomatica"=EXCLUDED."AsociacionAutomatica", "ConciliarHermanos"=EXCLUDED."ConciliarHermanos",
            "ModificadoUtc"=EXCLUDED."ModificadoUtc", "ModificadoPor"=EXCLUDED."ModificadoPor"
            """);
        var nuevo = await ObtenerAsync(db);
        var anteriorJson = JsonSerializer.Serialize(anterior);
        var nuevoJson = JsonSerializer.Serialize(nuevo);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ConfiguracionFiscalAuditoria" ("Usuario","Anterior","Nuevo")
            VALUES ({actor},CAST({anteriorJson} AS jsonb),CAST({nuevoJson} AS jsonb))
            """);
        await tx.CommitAsync();
    }
}
