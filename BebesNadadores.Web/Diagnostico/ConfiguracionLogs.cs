using Serilog;
using Serilog.Events;

namespace BebesNadadores.Web.Diagnostico;

/*
 * =============================================================
 * LOGS (Serilog)
 * =============================================================
 *
 * Archivos en la carpeta "logs" junto a la aplicación (o la que
 * indique Logs:Carpeta en appsettings):
 *
 *   bebes-AAAAMMDD.log     todo lo informativo (arranques, jobs,
 *                          validaciones que vio el usuario, errores)
 *   errores-AAAAMMDD.log   solo advertencias y errores: es el que
 *                          hay que mirar cuando alguien avisa de un
 *                          problema (buscar el código que le mostró
 *                          la pantalla)
 *
 * Un archivo por día, máximo 20 MB cada uno, se conservan
 * Logs:DiasRetencion días (60 por defecto).
 *
 * Niveles por defecto: Information para el sistema y Warning para
 * Microsoft / EF (si no, se loguea cada consulta SQL). Se pueden
 * cambiar en appsettings.json con una sección "Serilog" (opcional):
 *
 *   "Serilog": { "MinimumLevel": { "Default": "Information",
 *                "Override": { "Microsoft": "Warning" } } }
 *
 * IIS: el usuario del pool de aplicaciones necesita permiso de
 * escritura en la carpeta logs.
 *
 * Nota: el código está en la carpeta "Diagnostico" y no en "Logs"
 * porque el .gitignore estándar de Visual Studio ignora [Ll]ogs/
 * y la carpeta no se subía al repositorio.
 */
public static class ConfiguracionLogs
{
    private const string Plantilla =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}{NewLine}" +
        "    {Message:lj}{NewLine}{Exception}";

    private const long TamanioMaximoArchivo =
        20L * 1024 * 1024;


    /*
     * Logger mínimo para registrar errores mientras la app arranca
     * (antes de leer la configuración).
     */
    public static void CrearLoggerInicial()
    {
        Log.Logger =
            new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(
                    Path.Combine(ObtenerCarpeta(null), "arranque-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 10)
                .CreateBootstrapLogger();
    }


    public static void Configurar(
        HostBuilderContext context,
        IServiceProvider services,
        LoggerConfiguration configuracion)
    {
        var carpeta =
            ObtenerCarpeta(context.Configuration["Logs:Carpeta"]);

        var dias =
            context.Configuration.GetValue("Logs:DiasRetencion", 60);

        configuracion
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            // appsettings (si tiene sección "Serilog") pisa lo anterior
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: Plantilla)
            .WriteTo.File(
                Path.Combine(carpeta, "bebes-.log"),
                outputTemplate: Plantilla,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: dias,
                fileSizeLimitBytes: TamanioMaximoArchivo,
                rollOnFileSizeLimit: true,
                shared: true)
            .WriteTo.File(
                Path.Combine(carpeta, "errores-.log"),
                restrictedToMinimumLevel: LogEventLevel.Warning,
                outputTemplate: Plantilla,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: dias,
                fileSizeLimitBytes: TamanioMaximoArchivo,
                rollOnFileSizeLimit: true,
                shared: true);
    }


    private static string ObtenerCarpeta(
        string? configurada)
    {
        return string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(AppContext.BaseDirectory, "logs")
            : configurada;
    }
}
