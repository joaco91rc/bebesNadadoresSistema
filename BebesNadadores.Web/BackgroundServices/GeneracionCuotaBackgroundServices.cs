using System.Globalization;
using BebesNadadores.Application.Cuotas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BebesNadadores.Web.BackgroundServices;

/*
 * Genera las cuotas del mes actual todos los días a las 03:05
 * (hora Argentina) y al levantar la aplicación, por si el
 * servidor estuvo apagado el día 1. Es idempotente.
 */
public sealed class GeneracionCuotasBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<GeneracionCuotasBackgroundService> logger,
    IConfiguration configuracion)
    : TareaDiariaBackgroundService(scopeFactory, logger)
{
    protected override string NombreTarea =>
        "Generación de cuotas";

    protected override TimeOnly HoraEjecucion =>
        new(3, 5);


    protected override async Task EjecutarAsync(
        IServiceProvider services,
        CancellationToken ct)
    {
        var hoy =
            HoyArgentina();

        var periodo =
            new DateOnly(
                hoy.Year,
                hoy.Month,
                1);

        // Configuración opcional fuera de producción. Si se define mal, no generar.
        var inicio = configuracion["BackgroundServices:GeneracionCuotasDesde"];
        if (inicio is not null)
        {
            if (!DateOnly.TryParseExact(inicio, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var desde) || desde.Day != 1)
                throw new InvalidOperationException("GeneracionCuotasDesde debe ser el primer día del mes (yyyy-MM-dd).");
            if (periodo < desde)
            {
                Logger.LogInformation("Generación automática de cuotas omitida para {Periodo}: inicio configurado {Desde}.",
                    periodo.ToString("MM/yyyy"), desde.ToString("dd/MM/yyyy"));
                return;
            }
        }

        Logger.LogInformation(
            "Iniciando generación automática de cuotas para {Periodo}.",
            periodo.ToString("MM/yyyy"));

        var generador =
            services.GetRequiredService<IGeneradorCuotasService>();

        var resultado =
            await generador.GenerarAsync(
                periodo,
                ct);

        Logger.LogInformation(
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

        foreach (var advertencia in resultado.Advertencias)
        {
            Logger.LogWarning(
                "Generación de cuotas: {Advertencia}",
                advertencia);
        }
    }
}
