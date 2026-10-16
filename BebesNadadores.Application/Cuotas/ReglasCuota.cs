using BebesNadadores.Domain.Enums;

namespace BebesNadadores.Application.Cuotas;

/*
 * =============================================================
 * REGLAS DE IMPORTE DE CUOTA
 * =============================================================
 *
 * Único lugar con la cuenta del importe de una cuota y la regla
 * del primer mes. Lo usan el generador mensual, el alta de bebé,
 * la reinscripción y las pantallas (para mostrar lo mismo que
 * después se cobra).
 *
 * Primer mes (regla del natatorio):
 *
 *   Arranca del 1 al 14   -> cuota completa.
 *   Arranca del 15 en adelante -> la administrativa elige:
 *       a) arrancar ese día: MEDIA cuota (se cobra completa en el
 *          alta) + 1 clase de recupero de regalo, o
 *       b) arrancar el 1° del mes siguiente: cuota completa desde
 *          ese mes.
 */
public static class ReglasCuota
{
    /*
     * Desde este día del mes, el primer mes se cobra media cuota.
     */
    public const int DiaDesdeMediaCuota = 15;


    public static bool EsInicioAMitadDeMes(
        DateOnly fechaInicio)
    {
        return fechaInicio.Day >= DiaDesdeMediaCuota;
    }


    public static DateOnly PrimerDiaMesSiguiente(
        DateOnly fecha)
    {
        return new DateOnly(
                fecha.Year,
                fecha.Month,
                1)
            .AddMonths(1);
    }


    /*
     * Importe mensual con el descuento de la inscripción.
     */
    public static decimal CalcularImporteMensual(
        decimal tarifa,
        TipoDescuento? tipoDescuento,
        decimal? valorDescuento)
    {
        var descuento =
            CalcularDescuento(
                tarifa,
                tipoDescuento,
                valorDescuento);

        return Redondear(
            Math.Max(0, tarifa - descuento));
    }


    public static decimal CalcularDescuento(
        decimal tarifa,
        TipoDescuento? tipoDescuento,
        decimal? valorDescuento)
    {
        if (!tipoDescuento.HasValue || !valorDescuento.HasValue || valorDescuento.Value <= 0)
            return 0;

        var valor =
            valorDescuento.Value;

        return Redondear(
            tipoDescuento.Value switch
            {
                TipoDescuento.Porcentaje => Math.Min(tarifa, tarifa * valor / 100m),
                TipoDescuento.MontoFijo => Math.Min(tarifa, valor),
                _ => 0
            });
    }


    /*
     * Importe de la cuota del mes en que arranca la inscripción.
     */
    public static decimal CalcularImportePrimerMes(
        decimal importeMensual,
        DateOnly fechaInicio)
    {
        return EsInicioAMitadDeMes(fechaInicio)
            ? Redondear(importeMensual / 2m)
            : importeMensual;
    }


    private static decimal Redondear(
        decimal valor)
    {
        return decimal.Round(
            valor,
            2,
            MidpointRounding.AwayFromZero);
    }
}
