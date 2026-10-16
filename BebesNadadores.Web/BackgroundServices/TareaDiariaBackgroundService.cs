using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BebesNadadores.Web.BackgroundServices;

/*
 * =============================================================
 * TAREA DIARIA (base reutilizable)
 * =============================================================
 *
 * Corre una vez al levantar la aplicación (por si el servidor
 * estuvo apagado a la hora programada) y después todos los días
 * a la hora indicada, en hora de Argentina.
 *
 * Cada tarea concreta solo define nombre, hora y qué hacer:
 *
 *   public sealed class MiTarea(...) : TareaDiariaBackgroundService(...)
 *   {
 *       protected override string NombreTarea => "...";
 *       protected override TimeOnly HoraEjecucion => new(3, 5);
 *       protected override Task EjecutarAsync(IServiceProvider sp, CancellationToken ct) => ...;
 *   }
 *
 * Un error nunca tira abajo el servicio: se loguea y se vuelve a
 * intentar en la próxima ejecución.
 */
public abstract class TareaDiariaBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger logger)
    : BackgroundService
{
    protected abstract string NombreTarea { get; }

    protected abstract TimeOnly HoraEjecucion { get; }

    /*
     * Se ejecuta dentro de un scope nuevo: los servicios Scoped
     * (DbContext, servicios de negocio) se piden a "services".
     */
    protected abstract Task EjecutarAsync(
        IServiceProvider services,
        CancellationToken ct);

    protected ILogger Logger => logger;


    private static readonly TimeZoneInfo ZonaArgentina =
        CrearZonaArgentina();


    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Tarea automática '{Tarea}' iniciada.",
            NombreTarea);

        await EjecutarSeguroAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var demora =
                    CalcularDemoraHastaProximaEjecucion();

                logger.LogInformation(
                    "Próxima ejecución de '{Tarea}': {Proxima} (hora Argentina).",
                    NombreTarea,
                    TimeZoneInfo
                        .ConvertTime(DateTimeOffset.UtcNow.Add(demora), ZonaArgentina)
                        .ToString("dd/MM/yyyy HH:mm"));

                await Task.Delay(demora, stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            await EjecutarSeguroAsync(stoppingToken);
        }

        logger.LogInformation(
            "Tarea automática '{Tarea}' detenida.",
            NombreTarea);
    }


    private async Task EjecutarSeguroAsync(
        CancellationToken ct)
    {
        try
        {
            await using var scope =
                scopeFactory.CreateAsyncScope();

            await EjecutarAsync(
                scope.ServiceProvider,
                ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Cierre normal de la aplicación.
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error en la tarea automática '{Tarea}'.",
                NombreTarea);
        }
    }


    protected static DateOnly HoyArgentina()
    {
        var ahora =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                ZonaArgentina);

        return DateOnly.FromDateTime(ahora.DateTime);
    }


    /*
     * Mañana a la hora programada (ya corrió al arrancar).
     */
    private TimeSpan CalcularDemoraHastaProximaEjecucion()
    {
        var ahoraArgentina =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                ZonaArgentina);

        var proximaLocal =
            DateTime.SpecifyKind(
                ahoraArgentina.Date
                    .AddDays(1)
                    .Add(HoraEjecucion.ToTimeSpan()),
                DateTimeKind.Unspecified);

        var proximaUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                proximaLocal,
                ZonaArgentina);

        var demora =
            proximaUtc - DateTime.UtcNow;

        return demora > TimeSpan.Zero
            ? demora
            : TimeSpan.FromMinutes(1);
    }


    private static TimeZoneInfo CrearZonaArgentina()
    {
        try
        {
            // Linux / Docker
            return TimeZoneInfo.FindSystemTimeZoneById(
                "America/Argentina/Buenos_Aires");
        }
        catch
        {
            // Sigue con el id de Windows.
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Argentina Standard Time");
        }
        catch
        {
            // Último recurso: Argentina es UTC-03 sin horario de verano.
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Argentina",
            TimeSpan.FromHours(-3),
            "Argentina",
            "Argentina");
    }
}
