using System.Security.Claims;
using System.Security.Cryptography;
using System.Globalization;
using System.Text;
using BebesNadadores.Infrastructure.Identity;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Web.Services.Migracion;

public sealed record MigracionAnalisisDto(string Codigo, string NombreArchivo, long Bytes,
    string Sha256, DateTimeOffset FechaCargaUtc, string BaseDestino,
    int BebesExistentes, int CuotasExistentes, int PagosExistentes, bool RequiereConciliacion);

public sealed class MigracionCargaService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IDbContextFactory<AppDbContext> factory,
    AuthenticationStateProvider authentication,
    ILogger<MigracionCargaService> logger)
{
    private const long MaxBytes = 150L * 1024 * 1024;
    public async Task<MigracionAnalisisDto> RecibirYAnalizarAsync(IBrowserFile archivo, CancellationToken ct = default)
    {
        var usuario = await ExigirAdministradorAsync();
        if (!configuration.GetValue("Migracion:PermitirCargaSql", false))
            throw new InvalidOperationException("La carga de archivos SQL está deshabilitada en este entorno. Habilitala solo durante la ventana de migración.");
        if (!archivo.Name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seleccioná un archivo SQL exportado desde el sistema anterior.");
        if (archivo.Size is < 1 or > MaxBytes)
            throw new InvalidOperationException("El SQL debe tener entre 1 byte y 150 MB.");

        // Nunca escribir el nombre suministrado por el navegador ni guardar en wwwroot.
        var codigo = Guid.NewGuid().ToString("N");
        var directorio = Path.Combine(environment.ContentRootPath, "App_Data", "Migraciones");
        Directory.CreateDirectory(directorio);
        var ruta = Path.Combine(directorio, codigo + ".sql");
        string sha;
        try
        {
            await using var origen = archivo.OpenReadStream(MaxBytes, ct);
            await using var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 65536, FileOptions.Asynchronous);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[65536];
            int n;
            while ((n = await origen.ReadAsync(buffer, ct)) > 0)
            {
                hash.AppendData(buffer.AsSpan(0,n));
                await destino.WriteAsync(buffer.AsMemory(0,n), ct);
            }
            await destino.FlushAsync(ct);
            sha = Convert.ToHexString(hash.GetHashAndReset());
        }
        catch { try { File.Delete(ruta); } catch { } throw; }

        await using var db = await factory.CreateDbContextAsync(ct);
        // Lectura de estado de PostgreSQL sin hacer cambios; nunca ejecutar SQL importado aquí.
        var nombreBase = db.Database.GetDbConnection().Database;
        var bebes = await db.Bebes.CountAsync(ct);
        var cuotas = await db.Cuotas.CountAsync(ct);
        var pagos = await db.Pagos.CountAsync(ct);
        logger.LogWarning("Carga SQL de migración {Codigo}; admin {Usuario}; SHA256={Sha}; destino={Destino}; archivo local protegido.",
            codigo, usuario, sha, nombreBase);
        return new MigracionAnalisisDto(codigo, Path.GetFileName(archivo.Name), archivo.Size,
            sha, DateTimeOffset.UtcNow, nombreBase, bebes, cuotas, pagos, true);
    }
    // Los CSV se cargan desde la Web. Se vinculan al código del dump y se autentican
    // mediante un SHA-256 individual, para no aceptar los archivos de ejemplo creados
    // por el simulador ni reutilizar tarifas de otra migración.
    public async Task<string> GuardarTarifasAsync(IBrowserFile archivo, string codigo,
        bool excepciones, CancellationToken ct = default)
    {
        var admin = await ExigirAdministradorAsync();
        if (!configuration.GetValue("Migracion:PermitirCargaSql", false))
            throw new InvalidOperationException("La carga de migración está deshabilitada.");
        if (!Guid.TryParseExact(codigo, "N", out _))
            throw new InvalidOperationException("Código de migración inválido.");
        if (archivo.Size is < 1 or > 1024 * 1024)
            throw new InvalidOperationException("El CSV debe tener entre 1 byte y 1 MB.");
        if (!archivo.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Seleccioná un archivo CSV.");

        var baseDir = Path.Combine(environment.ContentRootPath, "App_Data", "Migraciones");
        if (!File.Exists(Path.Combine(baseDir, codigo + ".sql")))
            throw new InvalidOperationException("El dump asociado no está disponible.");
        var dir = Path.Combine(baseDir, "reportes-" + codigo);
        if (!Directory.Exists(dir))
            throw new InvalidOperationException("Primero ejecutá la simulación para crear los reportes.");

        var fileName = excepciones ? "tarifas_excepciones_2026.csv" : "tarifas_validadas_2026.csv";
        var path = Path.Combine(dir, fileName);
        var temp = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var input = archivo.OpenReadStream(1024 * 1024, ct))
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 16384, FileOptions.Asynchronous))
                await input.CopyToAsync(output, ct);

            // No aceptar archivos sin el formato requerido por Finanzas V4.8.
            // Los importes deben ser positivos y usar punto para decimales.
            var lines = await File.ReadAllLinesAsync(temp, new UTF8Encoding(false, true), ct);
            var expected = excepciones ? "IDPERSONA;MES;TARIFA_NETA" : "MES;TARIFA_NETA";
            if (lines.Length == 0 || lines[0].Trim().TrimStart('\uFEFF') != expected)
                throw new InvalidOperationException("Encabezado inválido. Se espera: " + expected);
            var found = new HashSet<string>(StringComparer.Ordinal);
            var monthlyCount = 0;
            for (var index = 1; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(';');
                if (parts.Length != (excepciones ? 3 : 2))
                    throw new InvalidOperationException($"Cantidad de columnas incorrecta en línea {index + 1}.");
                var monthIndex = excepciones ? 1 : 0;
                if (!int.TryParse(parts[monthIndex].Trim(), out var month) || month is < 2 or > 12)
                    throw new InvalidOperationException($"Mes inválido en línea {index + 1}.");
                var key = month.ToString(CultureInfo.InvariantCulture);
                if (excepciones)
                {
                    if (!long.TryParse(parts[0].Trim(), out var personId) || personId <= 0)
                        throw new InvalidOperationException($"IDPERSONA inválido en línea {index + 1}.");
                    key = personId.ToString(CultureInfo.InvariantCulture) + ";" + key;
                }
                if (!decimal.TryParse(parts[^1].Trim(), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var amount) || amount <= 0)
                    throw new InvalidOperationException($"Tarifa inválida en línea {index + 1}.");
                if (!found.Add(key))
                    throw new InvalidOperationException($"Tarifa duplicada en línea {index + 1}.");
                monthlyCount++;
            }
            if (!excepciones && monthlyCount == 0)
                throw new InvalidOperationException("El CSV de tarifas generales no puede estar vacío.");
            var sha = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(temp, ct)));
            File.Move(temp, path, overwrite: true);
            // El marcador es una prueba de carga explícita por la UI, no una plantilla.
            await File.WriteAllTextAsync(path + ".subido.sha256", sha, Encoding.ASCII, ct);
            logger.LogWarning("CSV {Nombre} de migración {Codigo}, SHA256={Sha}, admin {Admin}",
                fileName, codigo, sha, admin);
            return $"{fileName} validado ({monthlyCount} filas) y asociado al respaldo {codigo[..8]}…";
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static bool TarifasSubidasYVerificadas(string directory)
    {
        foreach (var name in new[] { "tarifas_validadas_2026.csv", "tarifas_excepciones_2026.csv" })
        {
            var path = Path.Combine(directory, name);
            var marker = path + ".subido.sha256";
            if (!File.Exists(path) || !File.Exists(marker)) return false;
            var expected = File.ReadAllText(marker, Encoding.ASCII).Trim();
            if (!string.Equals(expected, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
                StringComparison.Ordinal)) return false;
        }
        return true;
    }

    private async Task<Guid> ExigirAdministradorAsync()
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true || !principal.IsInRole(RolesSistema.Administrador))
            throw new UnauthorizedAccessException("Solo un administrador puede cargar respaldos para migración.");
        if (!Guid.TryParse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id))
            throw new UnauthorizedAccessException("No se pudo determinar el usuario actual.");
        return id;
    }
}
