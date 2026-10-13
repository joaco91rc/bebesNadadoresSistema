using BebesNadadores.Application.Cuotas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BebesNadadores.Web.BackgroundServices;

public sealed class GeneracionCuotasBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<GeneracionCuotasBackgroundService> logger)
    : BackgroundService
{
    // =========================================================
    // CONFIGURACIÓN
    // =========================================================

    private const int HoraEjecucion =
        3;


    private const int MinutoEjecucion =
        5;


    private static readonly TimeZoneInfo
        ZonaArgentina =
            CrearZonaArgentina();


    // =========================================================
    // EJECUCIÓN
    // =========================================================

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Servicio automático de generación de cuotas iniciado.");


        /*
         * Ejecutamos una vez apenas levanta la aplicación.
         *
         * Esto permite recuperar automáticamente el período
         * si el servidor estuvo apagado el día 1.
         */
        await EjecutarSeguroAsync(
            stoppingToken);


        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var demora =
                    CalcularDemoraHastaProximaEjecucion();


                logger.LogInformation(
                    "Próximo control automático de cuotas en {Demora}.",
                    demora);


                await Task.Delay(
                    demora,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }


            await EjecutarSeguroAsync(
                stoppingToken);
        }


        logger.LogInformation(
            "Servicio automático de generación de cuotas detenido.");
    }


    // =========================================================
    // EJECUCIÓN SEGURA
    // =========================================================

    private async Task EjecutarSeguroAsync(
        CancellationToken ct)
    {
        try
        {
            await EjecutarGeneracionAsync(
                ct);
        }
        catch (OperationCanceledException)
            when (ct.IsCancellationRequested)
        {
            // Cierre normal de la aplicación.
        }
        catch (Exception ex)
        {
            /*
             * Nunca dejamos caer el BackgroundService
             * por un error de generación.
             *
             * Se registra y volverá a intentar en la
             * próxima ejecución.
             */
            logger.LogError(
                ex,
                "Error durante la generación automática de cuotas.");
        }
    }


    // =========================================================
    // GENERAR CUOTAS DEL MES ACTUAL
    // =========================================================

    private async Task EjecutarGeneracionAsync(
        CancellationToken ct)
    {
        var ahoraArgentina =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                ZonaArgentina);


        var periodo =
            new DateOnly(
                ahoraArgentina.Year,
                ahoraArgentina.Month,
                1);


        logger.LogInformation(
            "Iniciando generación automática de cuotas para {Periodo}.",
            periodo.ToString("MM/yyyy"));


        /*
         * IGeneradorCuotasService es Scoped.
         *
         * Un BackgroundService es Singleton, por eso
         * debemos crear un scope por ejecución.
         */
        await using var scope =
            scopeFactory.CreateAsyncScope();


        var generador =
            scope.ServiceProvider
                .GetRequiredService<
                    IGeneradorCuotasService>();


        var resultado =
            await generador.GenerarAsync(
                periodo,
                ct);


        logger.LogInformation(
            "Generación de cuotas finalizada. " +
            "Período: {Periodo}. " +
            "Evaluadas: {Evaluadas}. " +
            "Generadas: {Generadas}. " +
            "Existentes: {Existentes}. " +
            "Sin tarifa: {SinTarifa}.",
            resultado.Periodo.ToString("MM/yyyy"),
            resultado.InscripcionesEvaluadas,
            resultado.CuotasGeneradas,
            resultado.CuotasYaExistentes,
            resultado.SinTarifa);


        foreach (var advertencia
            in resultado.Advertencias)
        {
            logger.LogWarning(
                "Generación de cuotas: {Advertencia}",
                advertencia);
        }
    }


    // =========================================================
    // PRÓXIMA EJECUCIÓN
    // =========================================================

    private static TimeSpan
        CalcularDemoraHastaProximaEjecucion()
    {
        var ahoraUtc =
            DateTimeOffset.UtcNow;


        var ahoraArgentina =
            TimeZoneInfo.ConvertTime(
                ahoraUtc,
                ZonaArgentina);


        /*
         * Próxima ejecución:
         *
         * mañana 03:05 hora Argentina.
         */
        var proximaLocal =
            ahoraArgentina.Date
                .AddDays(1)
                .AddHours(HoraEjecucion)
                .AddMinutes(MinutoEjecucion);


        proximaLocal =
            DateTime.SpecifyKind(
                proximaLocal,
                DateTimeKind.Unspecified);


        var proximaUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                proximaLocal,
                ZonaArgentina);


        var demora =
            proximaUtc -
            DateTime.UtcNow;


        /*
         * Protección por cualquier diferencia mínima
         * del reloj del servidor.
         */
        return demora > TimeSpan.Zero

            ? demora

            : TimeSpan.FromMinutes(1);
    }


    // =========================================================
    // ZONA HORARIA
    // =========================================================

    private static TimeZoneInfo
        CrearZonaArgentina()
    {
        /*
         * Linux / Docker.
         */
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch
        {
            // Continuamos con fallback.
        }


        /*
         * Windows.
         */
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
        catch
        {
            // Continuamos con fallback.
        }


        /*
         * Último fallback.
         *
         * Argentina = UTC-03.
         */
        return TimeZoneInfo
            .CreateCustomTimeZone(
                "Argentina",
                TimeSpan.FromHours(-3),
                "Argentina",
                "Argentina");
    }
}