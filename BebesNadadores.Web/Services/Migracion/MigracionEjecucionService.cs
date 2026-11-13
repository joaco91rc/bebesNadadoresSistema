using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Web.Services.Migracion;

// Ejecuta la migración REAL solo contra la base de pruebas local y limpia.
// NO habilitar en producción: TurneraTestFix/FinanzasActivosTest también bloquean producción.
public sealed class MigracionEjecucionService(
    IWebHostEnvironment environment,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    ILogger<MigracionEjecucionService> logger)
{
    private readonly SemaphoreSlim _mutex = new(1, 1);
    public string Estado { get; private set; } = "Sin iniciar";
    public bool Ocupado { get; private set; }
    public bool PrevisualizacionCompleta { get; private set; }
    public bool PruebaAplicada { get; private set; }
    public string? CodigoPreparado { get; private set; }
    public string Registro { get; private set; } = "";
    private DateOnly? _cortePreparado;
    public string? UltimoBackup { get; private set; }
    public bool ProduccionHabilitada => configuration.GetValue("Migracion:PermitirEjecucionProduccion", false) &&
        !string.IsNullOrWhiteSpace(configuration["Migracion:BaseProduccionPermitida"]);
    private readonly StringBuilder _log = new();

    public async Task PrepararAsync(string codigo, DateOnly corte)
    {
        if (!await _mutex.WaitAsync(0)) throw new InvalidOperationException("Hay otra migración ejecutándose.");
        try
        {
            Ocupado = true;
            Estado = "Validando condiciones";
            PrevisualizacionCompleta = false;
            PruebaAplicada = false;
            CodigoPreparado = null;
            _log.Clear();
            await VerificarDestinoVacioAsync(produccion: EsDestinoProduccion());
            var source = RutaArchivo(codigo);
            var cleaned = Path.Combine(Path.GetDirectoryName(source)!, codigo + "-staging.sql");
            Estado = "Aislando el esquema bbn2 del respaldo";
            await ExtraerSoloBbn2Async(source, cleaned);
            Estado = "Comprobando base MySQL de staging";
            var tablas = await EjecutarMysqlAsync("SHOW TABLES;", null);
            if (!string.IsNullOrWhiteSpace(tablas))
                throw new InvalidOperationException("La base MySQL de staging no está vacía. Debe utilizarse una base nueva y exclusiva; no se borra automáticamente ninguna tabla.");
            Estado = "Restaurando MySQL de staging";
            await EjecutarMysqlAsync(null, cleaned);
            Registrar("Respaldo restaurado en la base MySQL aislada.");
            Estado = "Simulando migrador 2026 (ROLLBACK)";
            var reportes = CarpetaReportes(codigo);
            Directory.CreateDirectory(reportes);
            // Al repetir la simulación se exige una nueva revisión de tarifas,
            // aunque existan archivos de una previsualización anterior.
            foreach (var nombre in new[] { "tarifas_validadas_2026.csv", "tarifas_excepciones_2026.csv" })
            {
                var marcador = Path.Combine(reportes, nombre + ".subido.sha256");
                if (File.Exists(marcador)) File.Delete(marcador);
            }
            await EjecutarMigradorAsync(corte, reportes, "--dry-run");
            CodigoPreparado = codigo;
            _cortePreparado = corte;
            PrevisualizacionCompleta = true;
            Estado = "Vista previa completada. Revisá los reportes antes de aplicar a TEST.";
        }
        catch (Exception ex)
        {
            Estado = "Falló la preparación: " + ex.Message;
            Registrar(Estado);
            logger.LogError(ex, "Falló la preparación de migración en TEST");
            throw;
        }
        finally { Ocupado = false; _mutex.Release(); }
    }

    public async Task AplicarSoloTestAsync(string codigo)
    {
        if (!await _mutex.WaitAsync(0)) throw new InvalidOperationException("Hay otra migración ejecutándose.");
        try
        {
            Ocupado = true;
            if (!PrevisualizacionCompleta || CodigoPreparado != codigo || _cortePreparado is null)
                throw new InvalidOperationException("Primero debe completarse la simulación del mismo archivo.");
            await VerificarDestinoVacioAsync(produccion: false);
            var reportes = CarpetaReportes(codigo);
            var tarifa = Path.Combine(reportes, "tarifas_validadas_2026.csv");
            var excepciones = Path.Combine(reportes, "tarifas_excepciones_2026.csv");
            if (!MigracionCargaService.TarifasSubidasYVerificadas(reportes))
                throw new InvalidOperationException("Subí ambos CSV desde la pantalla de migración y validá sus importes antes del COMMIT.");
            Estado = "Importando datos principales en TEST";
            await EjecutarMigradorAsync(_cortePreparado.Value, reportes);
            Estado = "Aplicando Turnera oficial en TEST";
            await EjecutarMigradorAsync(_cortePreparado.Value, reportes, "--corregir-turnera-test", "--aplicar-turnera-test");
            Estado = "Conciliando finanzas V4.8 en TEST";
            await EjecutarMigradorAsync(_cortePreparado.Value, reportes, "--conciliar-finanzas-test", "--aplicar-finanzas-test");
            PruebaAplicada = true;
            Estado = "Migración TEST terminada. Producción sigue bloqueada.";
            Registrar(Estado);
        }
        catch (Exception ex)
        {
            Estado = "Ejecución TEST interrumpida: " + ex.Message;
            Registrar(Estado);
            logger.LogError(ex, "La migración TEST se interrumpió. Puede haber pasos confirmados parcialmente.");
            throw;
        }
        finally { Ocupado = false; _mutex.Release(); }
    }

    // COMMIT de primera importación: solamente tras previsualización, backup y confirmación explícita.
    // La cadena tiene tres transacciones separadas. Si una etapa falla NO se reintenta:
    // se restaura el backup PostgreSQL (y se compara el informe del dump).
    public async Task AplicarProduccionAsync(string codigo, string confirmacion)
    {
        if (!await _mutex.WaitAsync(0)) throw new InvalidOperationException("Hay otra migración ejecutándose.");
        try
        {
            Ocupado = true;
            if (!ProduccionHabilitada || !EsDestinoProduccion())
                throw new InvalidOperationException("Migración productiva no habilitada para este destino.");
            var destino = configuration["Migracion:BaseProduccionPermitida"]!;
            if (!string.Equals(confirmacion, "MIGRAR " + destino, StringComparison.Ordinal))
                throw new InvalidOperationException("Escribí exactamente MIGRAR seguido del nombre de la base.");
            if (!PrevisualizacionCompleta || CodigoPreparado != codigo || _cortePreparado is null)
                throw new InvalidOperationException("Primero simulá el mismo dump y revisá sus reportes.");
            await VerificarDestinoVacioAsync(produccion: true);
            var reportes = CarpetaReportes(codigo);
            if (!MigracionCargaService.TarifasSubidasYVerificadas(reportes))
                throw new InvalidOperationException("Faltan los dos CSV cargados y verificados desde la pantalla para este respaldo.");
            Estado = "Generando respaldo PostgreSQL previo a COMMIT";
            UltimoBackup = await RespaldarPostgresAsync(codigo);
            Registrar("Respaldo PostgreSQL verificado en " + UltimoBackup);
            Estado = "Ejecutando migración principal en PRODUCCIÓN";
            await EjecutarMigradorAsync(_cortePreparado.Value, reportes, "--autorizar-produccion");
            Estado = "Aplicando Turnera oficial en PRODUCCIÓN";
            await EjecutarMigradorAsync(_cortePreparado.Value, reportes,
                "--corregir-turnera-test", "--aplicar-turnera-test", "--autorizar-produccion");
            Estado = "Conciliando finanzas verificadas en PRODUCCIÓN";
            await EjecutarMigradorAsync(_cortePreparado.Value, reportes,
                "--conciliar-finanzas-test", "--aplicar-finanzas-test", "--autorizar-produccion");
            Estado = "COMMIT PRODUCTIVO FINALIZADO. Reportes pendientes para la administrativa.";
            Registrar(Estado);
        }
        catch (Exception ex)
        {
            Estado = "PRODUCCIÓN: ejecución detenida: " + ex.Message;
            Registrar(Estado);
            logger.LogCritical(ex, "Migración productiva interrumpida. NO REINTENTAR sin restaurar backup.");
            throw;
        }
        finally { Ocupado = false; _mutex.Release(); }
    }

    private bool EsDestinoProduccion()
    {
        if (!ProduccionHabilitada) return false;
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        using (db)
            return string.Equals(db.Database.GetDbConnection().Database,
                configuration["Migracion:BaseProduccionPermitida"], StringComparison.Ordinal);
    }

    private async Task<string> RespaldarPostgresAsync(string codigo)
    {
        using var scope = scopeFactory.CreateScope();
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using (db)
        {
            var conn = new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
            var carpeta = configuration["Migracion:CarpetaBackupsPersistente"];
            if (string.IsNullOrWhiteSpace(carpeta) || !Path.IsPathFullyQualified(carpeta))
                throw new InvalidOperationException("Configurá Migracion:CarpetaBackupsPersistente en un volumen persistente, fuera del contenedor efímero.");
            if (Path.GetFullPath(carpeta).StartsWith(Path.GetFullPath(environment.WebRootPath ??
                Path.Combine(environment.ContentRootPath, "wwwroot")), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("El respaldo no puede quedar bajo wwwroot.");
            Directory.CreateDirectory(carpeta);
            var ruta = Path.Combine(carpeta, "pre-migracion-" + CodigoSeguro(codigo) + ".dump");
            var psi = new ProcessStartInfo(configuration["Migracion:PgDumpCli"] ?? "pg_dump")
            {
                RedirectStandardError = true, RedirectStandardOutput = true,
                UseShellExecute = false, CreateNoWindow = true
            };
            foreach (var arg in new[] { "--format=custom", "--no-password", "--file=" + ruta,
                "--host=" + conn.Host, "--port=" + conn.Port, "--username=" + conn.Username,
                "--dbname=" + conn.Database }) psi.ArgumentList.Add(arg);
            psi.Environment["PGPASSWORD"] = conn.Password ?? "";
            using var proc = Process.Start(psi) ?? throw new InvalidOperationException("pg_dump no disponible en el servidor.");
            var error = proc.StandardError.ReadToEndAsync();
            var salida = proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            var err = await error; await salida;
            if (proc.ExitCode != 0 || !File.Exists(ruta) || new FileInfo(ruta).Length < 1024)
                throw new InvalidOperationException("Backup PostgreSQL falló: " + (err.Length > 500 ? err[^500..] : err));
            return ruta;
        }
    }

    public string CarpetaReportes(string codigo) => Path.Combine(
        environment.ContentRootPath, "App_Data", "Migraciones", "reportes-" + CodigoSeguro(codigo));

    private string RutaArchivo(string codigo)
    {
        var ruta = Path.Combine(environment.ContentRootPath, "App_Data", "Migraciones", CodigoSeguro(codigo) + ".sql");
        if (!File.Exists(ruta)) throw new FileNotFoundException("El archivo cargado no existe; volvé a cargarlo.");
        return ruta;
    }

    private static string CodigoSeguro(string codigo) =>
        Guid.TryParseExact(codigo, "N", out _) ? codigo : throw new InvalidOperationException("Identificador de archivo inválido.");

    private async Task VerificarDestinoVacioAsync(bool produccion)
    {
        if (produccion)
        {
            if (!ProduccionHabilitada || !environment.IsProduction() ||
                !configuration.GetValue("Migracion:VentanaMantenimiento", false) ||
                configuration.GetValue("BackgroundServices:GeneracionCuotasHabilitada", true) ||
                configuration.GetValue("BackgroundServices:RecuperosAutomaticosHabilitados", true))
                throw new InvalidOperationException("COMMIT requiere Production, ventana de mantenimiento y ambos background services deshabilitados.");
        }
        else if (!configuration.GetValue("Migracion:PermitirEjecucionTest", false) || !environment.IsDevelopment())
            throw new InvalidOperationException("El circuito TEST exige Development y PermitirEjecucionTest.");
        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var actual = db.Database.GetDbConnection().Database;
        if (produccion)
        {
            if (!string.Equals(actual, configuration["Migracion:BaseProduccionPermitida"], StringComparison.Ordinal))
                throw new InvalidOperationException("Base productiva no coincide con el nombre autorizado.");
        }
        else
        {
            if (actual != "BebesNadadores_MigracionTest")
                throw new InvalidOperationException("Destino prohibido: solo BebesNadadores_MigracionTest.");
            var postgres = new Npgsql.NpgsqlConnectionStringBuilder(
    db.Database.GetDbConnection().ConnectionString);



var hostPermitido = configuration["Migracion:PostgresHostTestPermitido"];

if (string.IsNullOrWhiteSpace(hostPermitido) ||
    !string.Equals(postgres.Host, hostPermitido, StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException(
        "Host PostgreSQL de TEST no autorizado.");
}
        }
        if (await db.Bebes.AnyAsync() || await db.Inscripciones.AnyAsync() ||
            await db.Asistencias.AnyAsync() || await db.Recuperaciones.AnyAsync() ||
            await db.HistorialAlumnosLegacy.AnyAsync() ||
            await db.Pagos.AnyAsync() || await db.Cuotas.AnyAsync())
            throw new InvalidOperationException("La base destino ya contiene datos operativos. No se mezcla ni sobreescribe información existente: preparar una base limpia con maestros y usuarios.");
    }

    // Acepta dos formatos de respaldo MySQL:
    // 1) Exportación antigua multibase: CREATE DATABASE `bbn2`; USE `bbn2`;
    //    al principio. Conservamos exclusivamente el primer bloque bbn2.
    // 2) mysqldump de una sola base: encabezado "-- Host: ... Database: bbn2",
    //    sin CREATE DATABASE ni USE. Se importa en la base de staging YA seleccionada.
    // Los INSERT se mantienen byte a byte (Latin1 se usa solo para sustituir DEFINER).
    // La cuenta MySQL de staging debe tener privilegios SOLO sobre bbn_migracion_*.
    private static async Task ExtraerSoloBbn2Async(string source, string destination)
    {
        var bytes = await File.ReadAllBytesAsync(source);
        if (bytes.Length < 1024)
            throw new InvalidOperationException("El respaldo está vacío o incompleto.");

        var finPrimera = Array.IndexOf(bytes, (byte)'\n', 0);
        var finSegunda = finPrimera < 0 ? -1 : Array.IndexOf(bytes, (byte)'\n', finPrimera + 1);
        if (finPrimera < 0 || finSegunda < 0)
            throw new InvalidOperationException("El respaldo no tiene encabezado válido.");

        var primera = Encoding.ASCII.GetString(bytes, 0, Math.Min(finPrimera, 200))
            .TrimStart('\uFEFF').Trim();
        var segunda = Encoding.ASCII.GetString(bytes, finPrimera + 1,
            Math.Min(finSegunda - finPrimera - 1, 200)).Trim();

        int inicio;
        int finBloque;

        if (Regex.IsMatch(primera,
                @"^CREATE\s+DATABASE\s+IF\s+NOT\s+EXISTS\s+`bbn2`",
                RegexOptions.IgnoreCase) &&
            Regex.IsMatch(segunda, @"^USE\s+`bbn2`\s*;", RegexOptions.IgnoreCase))
        {
            // Respaldo multibase. Solo aislamos su primer bloque, como antes.
            inicio = finSegunda + 1;
            finBloque = bytes.Length;
            for (var pos = inicio; pos < bytes.Length;)
            {
                var finLinea = Array.IndexOf(bytes, (byte)'\n', pos);
                if (finLinea < 0) finLinea = bytes.Length;
                var encabezado = Encoding.ASCII.GetString(bytes, pos,
                    Math.Min(160, finLinea - pos)).TrimStart();
                if (Regex.IsMatch(encabezado,
                        @"^(CREATE\s+DATABASE|USE\s+`)", RegexOptions.IgnoreCase))
                {
                    finBloque = pos;
                    break;
                }
                pos = finLinea + 1;
            }
        }
        else
        {
            // Respaldo monobase de MySQL 5.1 (ej.: BebesNadadoresBase.sql).
            // La base bbn2 figura en el comentario del encabezado; no ejecutamos
            // CREATE DATABASE/USE porque la conexión CLI selecciona staging.
            var encabezado = Encoding.ASCII.GetString(bytes, 0,
                Math.Min(bytes.Length, 4096));
            if (!encabezado.Contains("MySQL dump", StringComparison.OrdinalIgnoreCase) ||
                !Regex.IsMatch(encabezado,
                    @"(?m)^--\s*Host:\s*[^\r\n]*?\bDatabase:\s*`?bbn2`?\s*\r?$",
                    RegexOptions.IgnoreCase))
                throw new InvalidOperationException(
                    "El SQL no corresponde a un mysqldump de la base bbn2. No se restaura.");

            inicio = 0;
            finBloque = bytes.Length;
        }

        var bloque = Encoding.Latin1.GetString(bytes, inicio, finBloque - inicio);

        // Una base MySQL aislada no debe poder cambiarse desde el archivo.
        // Revisamos todas las líneas, incluyendo las instrucciones ejecutables /*!...*/.
        // Estas defensas complementan los privilegios restringidos del usuario MySQL.
        if (Regex.IsMatch(bloque,
                @"(?im)^\s*(?:CREATE|DROP|ALTER)\s+(?:DATABASE|SCHEMA)\b|^\s*USE\s+[`\w]",
                RegexOptions.IgnoreCase | RegexOptions.Multiline) ||
            Regex.IsMatch(bloque,
                @"(?im)^\s*/\*!\d{5}\s+(?:(?:CREATE|DROP|ALTER)\s+(?:DATABASE|SCHEMA)\b|USE\s+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline) ||
            Regex.IsMatch(bloque, @"(?i)\bINTO\s+(?:OUTFILE|DUMPFILE)\b") ||
            Regex.IsMatch(bloque, @"(?im)^\s*(?:SOURCE|\\\.|SYSTEM)\s+"))
            throw new InvalidOperationException(
                "El SQL incluye comandos que podrían abandonar el esquema temporal. No se restaura.");

        // No aceptar nombres de base calificados en SQL ejecutable del dump.
        // Evita operaciones directas sobre esquemas diferentes al seleccionado.
        if (Regex.IsMatch(bloque,
                @"(?i)\b(?:FROM|JOIN|INTO|UPDATE|TABLE|VIEW|TRIGGER|REFERENCES)\s+`[^`]+`\s*\.\s*`"))
            throw new InvalidOperationException(
                "El SQL contiene objetos calificados con un nombre de base. Revisar antes de restaurar.");

        var tablas = Regex.Matches(bloque, @"(?im)^CREATE\s+TABLE\s+`?\w+`?\s*\(").Count;
        if (tablas < 10 || bloque.Count(c => c == '\n') < 100)
            throw new InvalidOperationException(
                "El bloque bbn2 parece incompleto: faltan tablas o contenido.");

        // El dump de MySQL 5.1 contiene DEFINER de usuarios SECRETARIA/root.
        // Al quitarlos, los objetos quedan bajo el usuario restringido de staging.
        bloque = Regex.Replace(bloque,
            @"/\*!\d{5}\s+DEFINER=[^\r\n]*?\*/",
            "/* DEFINER legado omitido */", RegexOptions.IgnoreCase);

        await File.WriteAllBytesAsync(destination, Encoding.Latin1.GetBytes(bloque));
    }

    private async Task<string> EjecutarMysqlAsync(string? sql, string? archivo)
    {
        var host = configuration["Migracion:MySqlHost"] ?? "127.0.0.1";
        var hostPermitido = configuration["Migracion:MySqlHostPermitido"];
        if (host != "127.0.0.1" && host != "localhost" &&
            !string.Equals(host, hostPermitido, StringComparison.Ordinal))
            throw new InvalidOperationException("Host MySQL no autorizado: indicar el host privado exacto en Migracion:MySqlHostPermitido.");
        var bd = configuration["Migracion:MySqlBaseTemporal"] ?? "bbn_migracion_web";
        if (!Regex.IsMatch(bd, "^bbn_migracion_[a-z0-9_]+$"))
            throw new InvalidOperationException("La base MySQL temporal debe comenzar por bbn_migracion_.");
        var user = configuration["Migracion:MySqlUsuario"];
        var password = configuration["Migracion:MySqlPassword"];
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("Configurá el usuario MySQL restringido para staging en User Secrets.");
        var cli = configuration["Migracion:MySqlCli"] ?? "mysql";
        var args = new[] { "--protocol=tcp", "--host=" + host, "--port=" + (configuration["Migracion:MySqlPuerto"] ?? "3306"), "--user=" + user, "--default-character-set=utf8", "--database=" + bd, "--batch", "--skip-column-names" };
        var psi = new ProcessStartInfo(cli) { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in args) psi.ArgumentList.Add(arg);
        psi.Environment["MYSQL_PWD"] = password;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("No se pudo ejecutar mysql.exe. Configurá Migracion:MySqlCli.");
        var stdOut = p.StandardOutput.ReadToEndAsync();
        var stdErr = p.StandardError.ReadToEndAsync();
        if (archivo is not null)
        {
            await using var input = File.OpenRead(archivo);
            await input.CopyToAsync(p.StandardInput.BaseStream);
        }
        else await p.StandardInput.WriteAsync(sql ?? "");
        p.StandardInput.Close();
        await p.WaitForExitAsync();
        var salida = await stdOut;
        var error = await stdErr;
        if (p.ExitCode != 0) throw new InvalidOperationException("mysql.exe finalizó con error: " + error[..Math.Min(error.Length, 800)]);
        return salida;
    }

    private async Task EjecutarMigradorAsync(DateOnly corte, string reportes, params string[] acciones)
    {
        using var scope = scopeFactory.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var pg = db.Database.GetDbConnection().ConnectionString;
        var mySql = new MySqlConnector.MySqlConnectionStringBuilder
        {
            Server = configuration["Migracion:MySqlHost"] ?? "127.0.0.1",
            Port = uint.Parse(configuration["Migracion:MySqlPuerto"] ?? "3306"),
            Database = configuration["Migracion:MySqlBaseTemporal"] ?? "bbn_migracion_web",
            UserID = configuration["Migracion:MySqlUsuario"],
            Password = configuration["Migracion:MySqlPassword"],
            SslMode = MySqlConnector.MySqlSslMode.None
        }.ConnectionString;
        var dll = configuration["Migracion:RutaMigradorDll"];
        var publicado = !string.IsNullOrWhiteSpace(dll);
        var proyecto = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "BebesNadadores.Migrador", "BebesNadadores.Migrador.csproj"));
        if (publicado && !File.Exists(dll))
            throw new InvalidOperationException("No existe el migrador publicado indicado en Migracion:RutaMigradorDll.");
        if (!publicado && !environment.IsDevelopment())
            throw new InvalidOperationException("Producción requiere el DLL publicado en Migracion:RutaMigradorDll.");
        if (!publicado && !File.Exists(proyecto))
            throw new InvalidOperationException("No se encuentra BebesNadadores.Migrador.csproj junto al proyecto Web.");
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = publicado ? Path.GetDirectoryName(dll)! : Path.GetDirectoryName(proyecto)!,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        var inicio = publicado ? new[] { dll! } : new[] { "run", "--no-launch-profile", "--project", proyecto, "--" };
        foreach (var arg in inicio.Concat(new[] { "--corte", corte.ToString("yyyy-MM-dd"), "--desde", "2019", "--anio-completo", "2026", "--reportes", reportes }).Concat(acciones))
            psi.ArgumentList.Add(arg);
        // No pasar contraseñas en argumentos de proceso ni imprimir cadenas de conexión en logs.
        psi.Environment["BEBES_POSTGRES"] = pg;
        if (acciones.Contains("--autorizar-produccion"))
            psi.Environment["BEBES_MIGRACION_PROD_TARGET"] = configuration["Migracion:BaseProduccionPermitida"] ?? "";
        psi.Environment["BEBES_LEGACY_MYSQL"] = mySql;
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("No fue posible ejecutar dotnet migrador.");
        var stderr = proc.StandardError.ReadToEndAsync();
        var stdout = proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        var sal = await stdout;
        var err = await stderr;
        Registrar(sal.Length > 2000 ? sal[^2000..] : sal);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException("Migrador terminó con error. " + (err.Length > 900 ? err[^900..] : err));
    }

    private void Registrar(string texto)
    {
        _log.AppendLine(texto);
        Registro = _log.Length > 9000 ? _log.ToString()[^9000..] : _log.ToString();
    }
}
