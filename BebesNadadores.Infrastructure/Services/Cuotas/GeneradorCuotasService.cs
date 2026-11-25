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
    // Generación manual de uno o varios períodos: todo el lote en una transacción.
    public Task<GeneracionCuotasResultadoDto> GenerarPeriodosAsync(DateOnly inicio, int cantidad,
        Guid? bebeId = null, Guid? usuarioId = null, CancellationToken ct = default)
        => ReintentoTransaccional.EjecutarAsync(() => GenerarPeriodosInternoAsync(inicio, cantidad, bebeId, usuarioId, ct), ct);

    private async Task<GeneracionCuotasResultadoDto> GenerarPeriodosInternoAsync(DateOnly inicio, int cantidad,
        Guid? bebeId, Guid? usuarioId, CancellationToken ct)
    {
        if (cantidad is < 1 or > 12) throw new InvalidOperationException("Seleccioná entre 1 y 12 cuotas.");
        inicio = new DateOnly(inicio.Year, inicio.Month, 1);
        var config = await configuracionService.ObtenerAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var trx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var generadas = 0;
        var existentes = 0;
        var evaluadas = 0;
        var errores = new List<string>();
        var afectados = new HashSet<Guid>();
        for (var n = 0; n < cantidad; n++)
        {
            var periodo = inicio.AddMonths(n);
            var inscripciones = await db.Inscripciones.AsNoTracking().Include(x => x.Bebe).Include(x => x.Plan)
                .Where(x => (!bebeId.HasValue || x.BebeId == bebeId.Value)
                    && x.Estado == EstadoInscripcion.Activa && x.Bebe.Estado == EstadoBebe.Activo
                    && x.FechaInicio <= periodo && (!x.FechaFin.HasValue || x.FechaFin.Value >= periodo))
                .OrderByDescending(x => x.FechaInicio).ThenBy(x => x.Id).ToListAsync(ct);
            var cuotas = await db.Cuotas.AsNoTracking().Where(x => x.Periodo == periodo
                && (!bebeId.HasValue || x.Inscripcion.BebeId == bebeId.Value))
                .Select(x => new { x.Inscripcion.BebeId, x.InscripcionId, x.Estado }).ToListAsync(ct);
            if (inscripciones.Count == 0)
            {
                if (bebeId.HasValue && cuotas.Any(x => x.Estado != EstadoCuota.Anulada)) { existentes++; continue; }
                errores.Add($"{periodo:MM/yyyy}: no hay inscripciones activas para el período. Revisá el ciclo lectivo.");
                continue;
            }
            var tarifas = await db.TarifasPlan.AsNoTracking().Where(x => x.VigenciaDesde <= periodo
                && (!x.VigenciaHasta.HasValue || x.VigenciaHasta.Value >= periodo))
                .OrderByDescending(x => x.VigenciaDesde).ToListAsync(ct);
            foreach (var inscripcion in inscripciones.GroupBy(x => x.BebeId).Select(g => g.First()))
            {
                evaluadas++;
                afectados.Add(inscripcion.BebeId);
                if (cuotas.Any(x => x.BebeId == inscripcion.BebeId && x.Estado != EstadoCuota.Anulada)) { existentes++; continue; }
                if (cuotas.Any(x => x.BebeId == inscripcion.BebeId))
                {
                    if (bebeId.HasValue) errores.Add($"{periodo:MM/yyyy}: existe una cuota anulada. Reactivala desde la cuenta corriente.");
                    else existentes++;
                    continue;
                }
                var tarifa = tarifas.FirstOrDefault(x => x.PlanId == inscripcion.PlanId);
                if (tarifa is null)
                {
                    errores.Add($"{periodo:MM/yyyy}: falta tarifa vigente para {inscripcion.Bebe.Apellido}, {inscripcion.Bebe.Nombre} (plan {inscripcion.Plan.Nombre}).");
                    continue;
                }
                var importe = ReglasCuota.CalcularImporteMensual(tarifa.PrecioMensual, inscripcion.TipoDescuento, inscripcion.ValorDescuento);
                var vence = new DateOnly(periodo.Year, periodo.Month, Math.Min(config.DiaVencimientoCuota, DateTime.DaysInMonth(periodo.Year, periodo.Month)));
                db.Cuotas.Add(new Cuota { Id = Guid.NewGuid(), InscripcionId = inscripcion.Id, Periodo = periodo,
                    FechaVencimiento = vence, Importe = importe, Estado = importe == 0m ? EstadoCuota.Pagada :
                    vence < BebesNadadores.Domain.Common.FechaArgentina.Hoy ? EstadoCuota.Vencida : EstadoCuota.Pendiente,
                    Descripcion = $"Cuota mensual {periodo:MM/yyyy} (generada manualmente)", CreadoPorUsuarioId = usuarioId });
                generadas++;
            }
        }
        if (errores.Count > 0) throw new InvalidOperationException("No se generó ninguna cuota. Primero configurá las tarifas del nuevo período o ciclo lectivo y revisá las inscripciones. " + string.Join(" | ", errores.Take(8)));
        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);
        var advertencias = new List<string>();
        foreach (var id in afectados)
        {
            try { await pagoService.AplicarSaldosAFavorAsync(id, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { advertencias.Add($"Cuotas generadas; no se pudo aplicar saldo a favor de {id}: {ex.Message}"); }
        }
        return new GeneracionCuotasResultadoDto(inicio, evaluadas, generadas, existentes, 0, advertencias);
    }

    // =========================================================
    // CUOTA INDIVIDUAL PARA COBRO ADELANTADO
    // =========================================================

    public Task<GeneracionCuotaIndividualDto> GenerarParaBebeAsync(
        Guid bebeId, DateOnly periodo, Guid usuarioId, CancellationToken ct = default)
        => ReintentoTransaccional.EjecutarAsync(
            () => GenerarParaBebeInternoAsync(bebeId, periodo, usuarioId, ct), ct);

    private async Task<GeneracionCuotaIndividualDto> GenerarParaBebeInternoAsync(
        Guid bebeId, DateOnly periodo, Guid usuarioId, CancellationToken ct)
    {
        if (bebeId == Guid.Empty || usuarioId == Guid.Empty)
            throw new InvalidOperationException("No se pudo identificar al bebé o al usuario.");
        periodo = new DateOnly(periodo.Year, periodo.Month, 1);
        var config = await configuracionService.ObtenerAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var trx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var existente = await db.Cuotas.AsNoTracking()
            .Where(x => x.Inscripcion.BebeId == bebeId && x.Periodo == periodo)
            .OrderBy(x => x.Estado == EstadoCuota.Anulada)
            .FirstOrDefaultAsync(ct);
        if (existente is not null)
        {
            if (existente.Estado == EstadoCuota.Anulada)
                throw new InvalidOperationException("Existe una cuota anulada para este período. Reactivala desde la cuenta corriente; no se creará otra.");
            await trx.CommitAsync(ct);
            return new GeneracionCuotaIndividualDto(existente.Id, periodo, existente.Importe, true);
        }

        var inscripcion = await db.Inscripciones.AsNoTracking()
            .Include(x => x.Bebe)
            .Where(x => x.BebeId == bebeId
                && x.Estado == EstadoInscripcion.Activa
                && x.Bebe.Estado == EstadoBebe.Activo
                && x.FechaInicio <= periodo
                && (!x.FechaFin.HasValue || x.FechaFin.Value >= periodo))
            .OrderByDescending(x => x.FechaInicio).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("El bebé no tiene una inscripción activa para el mes elegido. Revisá su inscripción antes de generar la cuota.");

        var tarifa = await db.TarifasPlan.AsNoTracking()
            .Where(x => x.PlanId == inscripcion.PlanId
                && x.VigenciaDesde <= periodo
                && (!x.VigenciaHasta.HasValue || x.VigenciaHasta.Value >= periodo))
            .OrderByDescending(x => x.VigenciaDesde).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("No hay una tarifa vigente para el plan en ese período.");
        var importe = ReglasCuota.CalcularImporteMensual(
            tarifa.PrecioMensual, inscripcion.TipoDescuento, inscripcion.ValorDescuento);
        var vence = new DateOnly(periodo.Year, periodo.Month, config.DiaVencimientoCuota);
        var cuota = new Cuota
        {
            InscripcionId = inscripcion.Id,
            Periodo = periodo,
            FechaVencimiento = vence,
            Importe = importe,
            Estado = importe == 0m ? EstadoCuota.Pagada
                : vence < BebesNadadores.Domain.Common.FechaArgentina.Hoy
                    ? EstadoCuota.Vencida : EstadoCuota.Pendiente,
            Descripcion = $"Cuota mensual {periodo:MM/yyyy} (generada manualmente)",
            CreadoPorUsuarioId = usuarioId
        };
        db.Cuotas.Add(cuota);
        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);
        // Aplicación posterior al commit. Si falla, la cuota YA está creada:
        // informamos la advertencia sin hacer creer que falló la generación.
        string? advertencia = null;
        try { await pagoService.AplicarSaldosAFavorAsync(bebeId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            advertencia = "La cuota se generó, pero no se pudo aplicar el saldo a favor. "
                + "Revisá la cuenta corriente antes de registrar otro cobro. " + ex.Message;
        }
        return new GeneracionCuotaIndividualDto(cuota.Id, periodo, importe, false, advertencia);
    }

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
