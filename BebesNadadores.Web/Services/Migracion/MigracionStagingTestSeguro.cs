using System.Text;
using System.Text.RegularExpressions;
using MySqlConnector;

namespace BebesNadadores.Web.Services.Migracion;

/// <summary>
/// Exclusivamente para la base MySQL descartable bbn_migracion_test.
/// Nunca limpia bases PostgreSQL ni la base de staging productiva.
/// </summary>
internal static class MigracionStagingTestSeguro
{
    private const string BaseTest = "bbn_migracion_test";

    public static async Task LimpiarAsync(IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!environment.IsDevelopment() ||
            !configuration.GetValue("Migracion:PermitirEjecucionTest", false) ||
            !string.Equals(configuration["Migracion:MySqlBaseTemporal"], BaseTest,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Limpieza automática prohibida fuera del staging MySQL exclusivo de TEST.");

        var host = configuration["Migracion:MySqlHost"] ?? "127.0.0.1";
        var permitido = configuration["Migracion:MySqlHostPermitido"];
        if (host != "127.0.0.1" && host != "localhost" &&
            !string.Equals(host, permitido, StringComparison.Ordinal))
            throw new InvalidOperationException("Host MySQL de staging no autorizado.");

        var usuario = configuration["Migracion:MySqlUsuario"];
        var clave = configuration["Migracion:MySqlPassword"];
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(clave))
            throw new InvalidOperationException("Faltan credenciales de MySQL staging.");

        var conexion = new MySqlConnectionStringBuilder
        {
            Server = host,
            Port = uint.Parse(configuration["Migracion:MySqlPuerto"] ?? "3306"),
            Database = BaseTest,
            UserID = usuario,
            Password = clave,
            SslMode = MySqlSslMode.None,
            AllowUserVariables = true
        };

        await using var db = new MySqlConnection(conexion.ConnectionString);
        await db.OpenAsync();

        var vistas = new List<string>();
        var tablas = new List<string>();
        await using (var comando = new MySqlCommand(
            "SELECT TABLE_NAME, TABLE_TYPE FROM information_schema.TABLES " +
            "WHERE TABLE_SCHEMA = @bd", db))
        {
            comando.Parameters.AddWithValue("@bd", BaseTest);
            await using var reader = await comando.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var nombre = reader.GetString(0);
                if (reader.GetString(1) == "VIEW") vistas.Add(nombre);
                else tablas.Add(nombre);
            }
        }

        static string NombreSeguro(string nombre) => "`" + nombre.Replace("`", "``") + "`";

        // FK_CHECKS es una variable de sesión, no del servidor compartido.
        await using (var apagar = new MySqlCommand("SET SESSION FOREIGN_KEY_CHECKS=0", db))
            await apagar.ExecuteNonQueryAsync();
        try
        {
            foreach (var vista in vistas)
            {
                await using var cmd = new MySqlCommand(
                    "DROP VIEW IF EXISTS " + NombreSeguro(vista), db);
                await cmd.ExecuteNonQueryAsync();
            }
            foreach (var tabla in tablas)
            {
                await using var cmd = new MySqlCommand(
                    "DROP TABLE IF EXISTS " + NombreSeguro(tabla), db);
                await cmd.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await using var restaurar = new MySqlCommand("SET SESSION FOREIGN_KEY_CHECKS=1", db);
            await restaurar.ExecuteNonQueryAsync();
        }

        // Si hubiera rutinas sueltas en staging, la limpieza debe detenerse para
        // revisarlas; no se eliminan esquemas ni objetos fuera de esta base.
        await using var comprobar = new MySqlCommand(
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA=@bd", db);
        comprobar.Parameters.AddWithValue("@bd", BaseTest);
        if (Convert.ToInt64(await comprobar.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException("No se pudo vaciar completamente el staging MySQL TEST.");
    }

    /// <summary>
    /// Quita los bloques de triggers (no necesarios para leer tablas/datos históricos)
    /// sin modificar el SQL original subido por el usuario.
    /// Compatible con el formato DELIMITER ;; / DELIMITER ; de mysqldump 5.1.
    /// </summary>
    public static async Task<int> OmitirTriggersLegadosAsync(string rutaSqlTemporal)
    {
        var bytes = await File.ReadAllBytesAsync(rutaSqlTemporal);
        var contenido = Encoding.Latin1.GetString(bytes);
        var patron = new Regex(
            @"(?ims)^[ \t]*DELIMITER[ \t]+;;[ \t]*\r?\n.*?^[ \t]*DELIMITER[ \t]+;[ \t]*\r?(?:\n|\z)",
            RegexOptions.Compiled);
        var cantidad = 0;
        var nuevo = patron.Replace(contenido, match =>
        {
            // Si un bloque delimitado no es un trigger, abortamos en lugar
            // de eliminar procedimientos o funciones sin comprobarlo.
            if (!Regex.IsMatch(match.Value, @"(?i)\bTRIGGER\s+`"))
                throw new InvalidOperationException(
                    "Hay un bloque DELIMITER desconocido en el dump; revisar antes de migrar.");
            cantidad++;
            return "-- Trigger legacy omitido exclusivamente para staging\n";
        });

        if (Regex.IsMatch(nuevo, @"(?i)\bCREATE\s+(?:DEFINER\s*=\s*[^\s]+\s+)?TRIGGER\b"))
            throw new InvalidOperationException(
                "Quedó un trigger fuera de los bloques DELIMITER; revisar el respaldo.");

        if (cantidad > 0)
            await File.WriteAllBytesAsync(rutaSqlTemporal, Encoding.Latin1.GetBytes(nuevo));

        return cantidad;
    }
}
