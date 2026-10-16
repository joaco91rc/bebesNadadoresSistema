using BebesNadadores.Application.Cuotas;
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
    ILogger<GeneracionCuotasBackgroundService> logger)
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
