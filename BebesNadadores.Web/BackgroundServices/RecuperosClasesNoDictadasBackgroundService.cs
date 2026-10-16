using BebesNadadores.Application.Recuperos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BebesNadadores.Web.BackgroundServices;

/*
 * Recuperos automáticos:
 *
 * 1. Clases que no se dictaron (feriado / cierre / cancelación
 *    manual): se generan cuando llega el día.
 * 2. Inicio a mitad de mes: el recupero de regalo se genera
 *    cuando la turnera ya tiene la primera clase del bebé.
 *
 * Sin esto, un feriado cargado con anticipación nunca generaba
 * recuperos: solo se generaban al guardar el feriado y solo para
 * fechas que ya habían pasado.
 *
 * Corre a las 00:20 (hora Argentina) y al levantar la aplicación.
 * Es idempotente: nunca duplica un recupero.
 */
public sealed class RecuperosClasesNoDictadasBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecuperosClasesNoDictadasBackgroundService> logger)
    : TareaDiariaBackgroundService(scopeFactory, logger)
{
    protected override string NombreTarea =>
        "Recuperos automáticos";

    protected override TimeOnly HoraEjecucion =>
        new(0, 20);


    protected override async Task EjecutarAsync(
        IServiceProvider services,
        CancellationToken ct)
    {
        var servicio =
            services.GetRequiredService<IRecuperosClasesNoDictadasService>();

        var generados =
            await servicio.GenerarPendientesAsync(ct);

        var inicio =
            await services
                .GetRequiredService<IRecuperosInicioMitadMesService>()
                .GenerarPendientesAsync(ct);

        Logger.LogInformation(
            "Recuperos automáticos: {Generados} por clases no dictadas, {Inicio} por inicio a mitad de mes.",
            generados,
            inicio);
    }
}
