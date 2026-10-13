using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Cuotas;
using BebesNadadores.Application.Cuotas.Dtos;
using BebesNadadores.Application.Pagos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Cuotas;

public sealed class GeneradorCuotasService(
    IDbContextFactory<AppDbContext> factory,
    IPagoService pagoService,
    IConfiguracionSistemaService configuracionService
) : IGeneradorCuotasService
{
    // =========================================================
    // GENERAR
    // =========================================================

    public async Task<GeneracionCuotasResultadoDto> GenerarAsync(
        DateOnly periodo,
        CancellationToken ct = default
    )
    {
        /*
         * Normalizamos siempre al primer día.
         *
         * 18/11/2026
         *      ↓
         * 01/11/2026
         */
        periodo = new DateOnly(periodo.Year, periodo.Month, 1);
        var configuracion = await configuracionService.ObtenerAsync(ct);

        var fechaVencimiento = new DateOnly(
            periodo.Year,
            periodo.Month,
            configuracion.DiaVencimientoCuota
        );

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var advertencias = new List<string>();

        var bebeIdsParaAplicarSaldo = new HashSet<Guid>();

        var generadas = 0;

        var yaExistentes = 0;

        var sinTarifa = 0;

        var evaluadas = 0;

        await using var db = await factory.CreateDbContextAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );

        try
        {
            // =====================================================
            // INSCRIPCIONES QUE CORRESPONDEN AL PERÍODO
            // =====================================================

            /*
             * La inscripción tiene que existir al comenzar
             * el mes.
             *
             * Esto es importante para cambios de plan:
             *
             * Plan A hasta 14/10
             * Plan B desde 15/10
             *
             * Octubre pertenece al Plan A.
             * Noviembre pertenece al Plan B.
             */

            var inscripciones = await db
                .Inscripciones.AsNoTracking()
                .Include(x => x.Bebe)
                .Where(x =>
                    x.FechaInicio <= periodo
                    && (!x.FechaFin.HasValue || x.FechaFin.Value >= periodo)
                    && x.Estado != EstadoInscripcion.Cancelada
                    && x.Estado != EstadoInscripcion.Pausada
                )
                .OrderBy(x => x.Bebe.Apellido)
                .ThenBy(x => x.Bebe.Nombre)
                .ToListAsync(ct);

            evaluadas = inscripciones.Count;

            if (inscripciones.Count == 0)
            {
                await transaction.CommitAsync(ct);

                return new GeneracionCuotasResultadoDto(periodo, 0, 0, 0, 0, []);
            }

            // =====================================================
            // CUOTAS YA EXISTENTES
            // =====================================================

            var inscripcionIds = inscripciones.Select(x => x.Id).ToList();

            var cuotasExistentes = await db
                .Cuotas.AsNoTracking()
                .Where(x => x.Periodo == periodo && inscripcionIds.Contains(x.InscripcionId))
                .Select(x => x.InscripcionId)
                .ToListAsync(ct);

            var existentes = cuotasExistentes.ToHashSet();

            // =====================================================
            // TARIFAS VIGENTES
            // =====================================================

            var planIds = inscripciones.Select(x => x.PlanId).Distinct().ToList();

            var tarifas = await db
                .TarifasPlan.AsNoTracking()
                .Where(x =>
                    planIds.Contains(x.PlanId)
                    && x.VigenciaDesde <= periodo
                    && (!x.VigenciaHasta.HasValue || x.VigenciaHasta.Value >= periodo)
                )
                .OrderByDescending(x => x.VigenciaDesde)
                .ToListAsync(ct);

            // =====================================================
            // GENERAR
            // =====================================================

            foreach (var inscripcion in inscripciones)
            {
                // ---------------------------------------------
                // YA EXISTE
                // ---------------------------------------------

                if (existentes.Contains(inscripcion.Id))
                {
                    yaExistentes++;

                    /*
                     * Aunque ya exista la cuota,
                     * igualmente queremos que al llegar
                     * al período se intente aplicar
                     * cualquier saldo a favor.
                     */
                    bebeIdsParaAplicarSaldo.Add(inscripcion.BebeId);

                    continue;
                }

                // ---------------------------------------------
                // TARIFA DEL PLAN
                // ---------------------------------------------

                var tarifa = tarifas
                    .Where(x => x.PlanId == inscripcion.PlanId)
                    .OrderByDescending(x => x.VigenciaDesde)
                    .FirstOrDefault();

                if (tarifa is null)
                {
                    sinTarifa++;

                    advertencias.Add(
                        $"{inscripcion.Bebe.Apellido}, "
                            + $"{inscripcion.Bebe.Nombre}: "
                            + $"el plan no posee tarifa vigente "
                            + $"para {periodo:MM/yyyy}."
                    );

                    continue;
                }

                // ---------------------------------------------
                // IMPORTE
                // ---------------------------------------------

                var importe = CalcularImporte(
                    tarifa.PrecioMensual,
                    inscripcion.TipoDescuento,
                    inscripcion.ValorDescuento
                );

                // ---------------------------------------------
                // ESTADO INICIAL
                // ---------------------------------------------

                var estado = fechaVencimiento < hoy ? EstadoCuota.Vencida : EstadoCuota.Pendiente;

                // ---------------------------------------------
                // CUOTA
                // ---------------------------------------------

                var cuota = new Cuota
                {
                    Id = Guid.NewGuid(),

                    InscripcionId = inscripcion.Id,

                    Periodo = periodo,

                    FechaVencimiento = fechaVencimiento,

                    Importe = importe,

                    Estado = estado,

                    Descripcion = $"Cuota mensual " + $"{periodo:MM/yyyy}",
                };

                db.Cuotas.Add(cuota);

                /*
                 * También la agregamos al HashSet
                 * en memoria para evitar duplicados
                 * dentro de la misma ejecución.
                 */
                existentes.Add(inscripcion.Id);

                bebeIdsParaAplicarSaldo.Add(inscripcion.BebeId);

                generadas++;
            }

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);

            throw;
        }

        // =========================================================
        // SALDOS A FAVOR
        // =========================================================

        /*
         * PagoService toma pagos anteriores
         * que tengan importe no imputado
         * y los aplica a cuotas disponibles.
         *
         * Se hace después del commit porque
         * PagoService utiliza su propio DbContext.
         */

        foreach (var bebeId in bebeIdsParaAplicarSaldo)
        {
            try
            {
                await pagoService.AplicarSaldosAFavorAsync(bebeId, ct);
            }
            catch (Exception ex)
            {
                /*
                 * La generación de la cuota ya fue
                 * confirmada en la base.
                 *
                 * Si falla la imputación automática
                 * no eliminamos la cuota.
                 */

                advertencias.Add(
                    $"No se pudo aplicar automáticamente "
                        + $"el saldo a favor del bebé "
                        + $"{bebeId}: {ex.Message}"
                );
            }
        }

        return new GeneracionCuotasResultadoDto(
            periodo,
            evaluadas,
            generadas,
            yaExistentes,
            sinTarifa,
            advertencias
        );
    }

    // =========================================================
    // IMPORTE
    // =========================================================

    private static decimal CalcularImporte(
        decimal tarifa,
        TipoDescuento? tipoDescuento,
        decimal? valorDescuento
    )
    {
        var descuento = CalcularDescuento(tarifa, tipoDescuento, valorDescuento);

        var importe = Math.Max(0, tarifa - descuento);

        return decimal.Round(importe, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal CalcularDescuento(
        decimal tarifa,
        TipoDescuento? tipoDescuento,
        decimal? valorDescuento
    )
    {
        if (!tipoDescuento.HasValue || !valorDescuento.HasValue || valorDescuento.Value <= 0)
        {
            return 0;
        }

        var valor = valorDescuento.Value;

        return tipoDescuento.Value switch
        {
            TipoDescuento.Porcentaje => Math.Min(tarifa, tarifa * valor / 100m),

            TipoDescuento.MontoFijo => Math.Min(tarifa, valor),

            _ => 0,
        };
    }
}
