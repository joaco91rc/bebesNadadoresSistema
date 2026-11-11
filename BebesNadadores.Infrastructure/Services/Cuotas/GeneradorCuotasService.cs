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

    public Task<GeneracionCuotasResultadoDto> GenerarAsync(
        DateOnly periodo,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => GenerarInternoAsync(
                periodo,
                ct),
            ct);
    }


    private async Task<GeneracionCuotasResultadoDto> GenerarInternoAsync(
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

        // Sin try/catch con Rollback manual: si la transacción ya está
        // abortada, el Rollback explota y tapa el error real. El
        // "await using" hace el rollback solo.
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
        && x.Estado == EstadoInscripcion.Activa
        && x.Bebe.Estado == EstadoBebe.Activo
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

        /*
         * La cuota es del BEBÉ por período, no de la inscripción.
         *
         * Caso: cambio de plan aplicado el día 1. La inscripción
         * vieja ya tiene la cuota del mes (generada a las 03:05) y
         * la nueva arranca ese mismo día 1. Sin este control se
         * generaba una segunda cuota del mismo mes.
         */
        var bebeIds = inscripciones.Select(x => x.BebeId).Distinct().ToList();

        var bebesConCuota = (
            await db
                .Cuotas.AsNoTracking()
                .Where(x =>
                    x.Periodo == periodo
                    && x.Estado != EstadoCuota.Anulada
                    && bebeIds.Contains(x.Inscripcion.BebeId)
                )
                .Select(x => x.Inscripcion.BebeId)
                .ToListAsync(ct)
        ).ToHashSet();

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

            if (existentes.Contains(inscripcion.Id) || bebesConCuota.Contains(inscripcion.BebeId))
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

            bebesConCuota.Add(inscripcion.BebeId);

            bebeIdsParaAplicarSaldo.Add(inscripcion.BebeId);

            generadas++;
        }

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);


        /*
         * También se revisan todos los bebés con saldo a favor (pagos
         * con dinero sin imputar), no solo los que recibieron una
         * cuota nueva: si tienen alguna cuota pendiente (por ejemplo,
         * una del mes siguiente ya generada), se les aplica. Así el
         * saldo a favor nunca queda "olvidado".
         */
        var conSaldoAFavor =
            await db.Pagos
                .AsNoTracking()
                .Where(x =>
                    x.AnuladoUtc == null
                    && x.Importe >
                        (x.Imputaciones.Sum(i => (decimal?)i.Importe) ?? 0m))
                .Select(x => x.BebeId)
                .Distinct()
                .ToListAsync(ct);

        bebeIdsParaAplicarSaldo.UnionWith(conSaldoAFavor);
    


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
        // Misma cuenta que el alta y la reinscripción.
        return ReglasCuota.CalcularImporteMensual(tarifa, tipoDescuento, valorDescuento);
    }
}
