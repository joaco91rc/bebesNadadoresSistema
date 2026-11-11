using System.Globalization;
using System.Text;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Migrador;

/// <summary>
/// V4.8: conciliacion por pagos reales y cuotas compatibles con descuentos historicos sobre la base de prueba YA importada.
/// NO crea mora suponiendo impagos. NO crea recuperos ni modifica pagos.
/// Solo crea cuotas y vinculos de pagos tipo CUOTA (1) con tarifa neta verificada.
/// Cada mes sin evidencia o con contradicciones queda para revision manual.
/// </summary>
public static class FinanzasActivosTest
{
    private sealed record Decision(long Persona, Guid Bebe, Guid Inscripcion, int Mes,
        decimal TotalPagosCuota, decimal Mora, decimal? TarifaNeta, decimal ImporteCuota, decimal Saldo, string Accion,
        string Motivo, List<LegacyPago> Pagos);

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static string F(decimal v) => v.ToString("0.00", Invariant);
    private static string E(string s) => "\"" + s.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ") + "\"";

    public static async Task RunAsync(AppDbContext db, LegacyRepository legacy,
        MigrationOptions opt, bool aplicar, CancellationToken ct = default)
    {
        // Permiso productivo explícito, condicionado al destino exacto.
        if (!string.Equals(db.Database.GetDbConnection().Database,
            "BebesNadadores_MigracionTest", StringComparison.OrdinalIgnoreCase) && aplicar)
            ProductionCommitGuard.Check(db, opt.ConfirmProduction);
        if (opt.FullYear != 2026 || opt.Cutoff is null || opt.Cutoff.Value.Year != 2026)
            throw new InvalidOperationException("Indicar --anio-completo 2026 y --corte 2026-MM-DD.");
        if (opt.Cutoff.Value.Month < 2)
            throw new InvalidOperationException("El corte debe ser posterior al comienzo de clases en febrero.");

        // La tarifa nominal NO se deduce del importe pagado.
        // Cargar únicamente meses confirmados y excepciones verificadas.
        var tarifas = LoadMonthlyTariffs(opt.ReportDirectory);
        var excepciones = LoadPersonalTariffs(opt.ReportDirectory);
        var corte = opt.Cutoff.Value;
        var fuentes = await legacy.TurnosVigentesAsync(ct);
        var pagos = await legacy.PagosAsync(2026, corte, ct);
        var morosos = await legacy.MorososAsync(2026, ct);
        var activeIds = fuentes.Select(x => x.PersonaId).ToHashSet();
        var pagosBy = pagos.Where(x => x.TipoPagoId == 1 && x.AnioPago == 2026 && x.Mes is >= 2 and <= 12)
            .GroupBy(x => (x.PersonaId, x.Mes)).ToDictionary(g => g.Key, g => g.ToList());
        var debtBy = morosos.Where(x => (x.TipoPagoId is null or 1) && x.Mes is >= 2 and <= 12)
            .GroupBy(x => (x.PersonaId, x.Mes)).ToDictionary(g => g.Key, g => g.Sum(x => x.Importe));

        var linked = await db.HistorialAlumnosLegacy.AsNoTracking()
            .Where(x => x.BebeId.HasValue)
            .Select(x => new { x.LegacyPersonaId, x.BebeId }).ToListAsync(ct);
        var byLegacy = linked.GroupBy(x => x.LegacyPersonaId)
            .ToDictionary(x => x.Key, x => x.First().BebeId!.Value);
        var subs = await db.Inscripciones.AsNoTracking()
            .Where(x => x.FechaInicio.Year == 2026).ToListAsync(ct);
        var turns = await db.InscripcionesTurnos.AsNoTracking()
            .Select(x => new { x.InscripcionId, x.TurnoId }).ToListAsync(ct);
        var oficialIds = (await db.Turnos.AsNoTracking()
            .Where(x => x.Nombre.StartsWith("Oficial 2026"))
            .Select(x => x.Id).ToListAsync(ct)).ToHashSet();
        var liveSubs = turns.Where(x => oficialIds.Contains(x.TurnoId))
            .Select(x => x.InscripcionId).ToHashSet();
        var validSubsByBebe = subs.Where(x => liveSubs.Contains(x.Id))
            .GroupBy(x => x.BebeId).ToDictionary(x => x.Key, x => x.ToList());
        var bebes = (await db.Bebes.AsNoTracking().Select(x => x.Id).ToListAsync(ct)).ToHashSet();

        var missing = new List<string>();
        var decisions = new List<Decision>();
        var monthMax = Math.Min(11, corte.Month);
        foreach (var persona in activeIds.OrderBy(x => x))
        {
            var bebeId = byLegacy.GetValueOrDefault(persona,
                MigrationHelpers.StableGuid($"bebe:{persona}"));
            if (!bebes.Contains(bebeId) || !validSubsByBebe.TryGetValue(bebeId, out var candidates)
                || candidates.Count != 1)
            {
                missing.Add($"{persona};{bebeId};SIN_FICHA_O_INSCRIPCION_OFICIAL_UNICA");
                continue;
            }
            var ins = candidates[0];
            for (var month = 2; month <= monthMax; month++)
            {
                var key = (persona, month);
                var mov = pagosBy.GetValueOrDefault(key) ?? [];
                var paid = mov.Sum(x => x.Importe);
                var debt = debtBy.GetValueOrDefault(key);
                decimal? tarifa = excepciones.TryGetValue((persona, month), out var individual)
                    ? individual
                    : (tarifas.TryGetValue(month, out var mensual) ? mensual : null);

                // El valor neto historico se determina solo para este periodo.
                // NO registra descuento en la ficha del alumno ni cambia tarifas futuras.
                var esCompatible = false;
                if (!excepciones.ContainsKey((persona, month)) && tarifa is > 0m
                    && !mov.Any(x => x.Importe < 0m) && debt >= 0m)
                {
                    var reduccionConfirmada = month <= 7 ? 5000m : 10000m;
                    var neta = tarifa.Value - reduccionConfirmada;
                    if (neta > 0m &&
                        ((paid == neta && debt == 0m) || (paid == 0m && debt == neta)))
                    {
                        tarifa = neta;
                        esCompatible = true;
                    }
                }
                var decision = Classify(persona, bebeId, ins.Id, ins.FechaInicio,
                    month, paid, debt, tarifa, mov, corte);
                if (esCompatible && decision.Accion == "IMPORTAR_CUOTA")
                    decision = decision with { Motivo =
                        "Cuota historica compatible con bonificacion de hermanos confirmada por periodo; " +
                        "NO se migra descuento individual. " + decision.Motivo };
                decisions.Add(decision);
            }
        }

        // El dump nuevo puede tener otras cantidades de alumnos activos.
        // Mantener control de exhaustividad SIN exigir los números fijos del 02/11.
        var vinculados = decisions.Select(x => x.Persona).Distinct().Count();
        var pendientesUnicos = missing.Select(x => x.Split(';')[0]).Distinct().Count();
        if (vinculados + pendientesUnicos != activeIds.Count ||
            missing.Any(x => string.IsNullOrWhiteSpace(x)))
            throw new InvalidOperationException($"Conciliación incompleta: origen {activeIds.Count}, " +
                $"vinculados {vinculados}, pendientes {missing.Count}. No se aplican cambios.");

        // Estado de confianza PARA EL CORTE del dump, no para la fecha actual.
        // Una cuota sin evidencia NO autoriza mostrar "Al dia".
        // Las cuotas de un mes antes del dia 16 aun no cuentan como vencidas.
        var estados = decisions.GroupBy(x => x.Persona).OrderBy(x => x.Key).Select(g =>
        {
            var items = g.ToList();
            var vencidasDocumentadas = items.Count(x => x.Accion == "IMPORTAR_CUOTA" &&
                x.Saldo > 0m && new DateOnly(2026, x.Mes, 15) < corte);
            var mesesSinConciliarVencidos = items.Count(x =>
                x.Accion != "IMPORTAR_CUOTA" && x.Accion != "REVISAR_ALTA" &&
                new DateOnly(2026, x.Mes, 15) < corte);
            var mesesNoVencidosSinConciliar = items.Count(x =>
                x.Accion != "IMPORTAR_CUOTA" && x.Accion != "REVISAR_ALTA" &&
                new DateOnly(2026, x.Mes, 15) >= corte);
            var estado = mesesSinConciliarVencidos > 0 ? "PENDIENTE_REVISION"
                : vencidasDocumentadas >= 2 ? "DEBE_MAS_DE_UNA"
                : vencidasDocumentadas == 1 ? "DEBE_UNA"
                : "AL_DIA_AL_CORTE";
            return new
            {
                Persona = g.Key, Bebe = items[0].Bebe,
                VencidasDocumentadas = vencidasDocumentadas,
                MesesSinConciliarVencidos = mesesSinConciliarVencidos,
                MesesNoVencidosSinConciliar = mesesNoVencidosSinConciliar,
                Estado = estado
            };
        }).ToList();

        Directory.CreateDirectory(opt.ReportDirectory);
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_446_estado_indicador_v48.csv"),
            new[] { "IDPERSONA;BEBE_GUID;FECHA_CORTE;CUOTAS_VENCIDAS_CONFIRMADAS;MESES_VENCIDOS_SIN_CONCILIAR;MESES_NO_VENCIDOS_SIN_CONCILIAR;ESTADO_VALIDADO_AL_CORTE" }
            .Concat(estados.Select(x => $"{x.Persona};{x.Bebe};{corte:yyyy-MM-dd};{x.VencidasDocumentadas};{x.MesesSinConciliarVencidos};{x.MesesNoVencidosSinConciliar};{x.Estado}")), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_446_periodos_pendientes_v48.csv"),
            new[] { "IDPERSONA;MES;PAGO_CUOTA;DEUDA_ORIGEN;ACCION;VENCIDA_AL_CORTE;MOTIVO" }
            .Concat(decisions.Where(x => x.Accion != "IMPORTAR_CUOTA" && x.Accion != "REVISAR_ALTA")
                .Select(x => $"{x.Persona};{x.Mes};{F(x.TotalPagosCuota)};{F(x.Mora)};{x.Accion};" +
                    $"{(new DateOnly(2026, x.Mes, 15) < corte ? "SI" : "NO")};{E(x.Motivo)}")), Encoding.UTF8);

        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_446_conciliacion.csv"),
            new[] { "IDPERSONA;ANIO;MES;PAGOS_CUOTA;DEUDA_ORIGEN;TARIFA_NETA_VERIFICADA;IMPORTE_CUOTA;SALDO_PENDIENTE;ACCION;MOVIMIENTOS_CERO;MOVIMIENTOS_NEGATIVOS;MOTIVO" }
            .Concat(decisions.Select(d => $"{d.Persona};2026;{d.Mes};{F(d.TotalPagosCuota)};{F(d.Mora)};" +
                $"{(d.TarifaNeta.HasValue ? F(d.TarifaNeta.Value) : "")};{F(d.ImporteCuota)};{F(d.Saldo)};{d.Accion};{d.Pagos.Count(p => p.Importe == 0)};{d.Pagos.Count(p => p.Importe < 0)};{E(d.Motivo)}")), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "cuotas_compatibles_descuentos_v47.csv"),
            new[] { "IDPERSONA;ANIO;MES;PAGOS;DEUDA;IMPORTE_CUOTA;CRITERIO" }
            .Concat(decisions.Where(x => x.Accion == "IMPORTAR_CUOTA" &&
                        x.Motivo.StartsWith("Cuota historica compatible"))
                .Select(x => $"{x.Persona};2026;{x.Mes};{F(x.TotalPagosCuota)};{F(x.Mora)};{F(x.ImporteCuota)};" +
                    (x.TotalPagosCuota > 0m ? "PAGO_TOTAL_NETO" : "DEUDA_EXPLICITA_NETA"))), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_446_sin_vinculo.csv"),
            new[] { "IDPERSONA;BEBE_GUID;MOTIVO" }.Concat(missing), Encoding.UTF8);

        var aptos = decisions.Where(x => x.Accion == "IMPORTAR_CUOTA").ToList();
        var revisar = decisions.Count - aptos.Count;
        var totalCuotas = aptos.Sum(x => x.ImporteCuota);
        var totalPagos = aptos.Sum(x => x.TotalPagosCuota);
        var saldoExplicito = aptos.Sum(x => x.Saldo);
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_v47_resumen.csv"),
            new[] { "CONCEPTO;VALOR", $"ALUMNOS_VINCULADOS;{decisions.Select(x => x.Persona).Distinct().Count()}",
                $"PERIODOS_CON_TARIFA_CONFIRMADA;{decisions.Count(x => x.TarifaNeta.HasValue)}",
                $"CUOTAS_COMPATIBLES_DESCUENTO_HISTORICO;{decisions.Count(x => x.Accion == "IMPORTAR_CUOTA" && x.Motivo.StartsWith("Cuota historica compatible"))}",
                $"CUOTAS_IMPORTABLES;{aptos.Count}", $"PERIODOS_REVISION;{revisar}",
                $"IMPORTE_CUOTAS_IMPORTABLES;{F(totalCuotas)}", $"PAGOS_CUOTAS_IMPORTABLES;{F(totalPagos)}",
                $"SALDO_PENDIENTE_EXPLICITO;{F(saldoExplicito)}" }, Encoding.UTF8);
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_v48_resumen.csv"),
            new[] { "CONCEPTO;VALOR",
                $"ALUMNOS_ACTIVOS_CON_TURNO_OFICIAL;{estados.Count}",
                $"CUOTAS_IMPORTABLES;{aptos.Count}",
                $"PERIODOS_ANTERIORES_AL_ALTA;{decisions.Count(x => x.Accion == "REVISAR_ALTA")}",
                $"PERIODOS_SIN_EVIDENCIA;{decisions.Count(x => x.Accion == "REVISAR_SIN_EVIDENCIA")}",
                $"PAGOS_PARCIALES_SIN_MORA;{decisions.Count(x => x.Accion == "REVISAR_PARCIAL_SIN_MORA")}",
                $"EXCEDENTES;{decisions.Count(x => x.Accion == "REVISAR_EXCEDENTE")}",
                $"ALUMNOS_CON_ESTADO_PENDIENTE_REVISION;{estados.Count(x => x.Estado == "PENDIENTE_REVISION")}",
                $"ALUMNOS_CON_UNA_VENCIDA_DOCUMENTADA_Y_SIN_INCIERTOS;{estados.Count(x => x.Estado == "DEBE_UNA")}",
                $"ALUMNOS_CON_DOS_O_MAS_VENCIDAS_DOCUMENTADAS_Y_SIN_INCIERTOS;{estados.Count(x => x.Estado == "DEBE_MAS_DE_UNA")}",
                $"ALUMNOS_SIN_VENCIDA_NI_INCIERTOS_AL_CORTE;{estados.Count(x => x.Estado == "AL_DIA_AL_CORTE")}",
                $"IMPORTE_CUOTAS_IMPORTABLES;{F(totalCuotas)}",
                $"PAGOS_IMPUTABLES;{F(totalPagos)}",
                $"SALDO_DOCUMENTADO;{F(saldoExplicito)}"
            }, Encoding.UTF8);
        Console.WriteLine($"Finanzas activos: {activeIds.Count} origen, {vinculados} vinculados, {missing.Count} pendientes. " +
            $"Meses con evidencia: {aptos.Count:N0}; meses para revisión: {revisar:N0}.");
        Console.WriteLine($"Tarifas confirmadas: {tarifas.Count} meses y {excepciones.Count} excepciones individuales; " +
            $"cuotas validadas {F(totalCuotas)}; cobros imputables {F(totalPagos)}; saldo documentado {F(saldoExplicito)}.");
        Console.WriteLine($"Corte: {corte:yyyy-MM-dd}; vencimiento mensual día 15; " +
            "CLASE(5) y MEDIO MES(6) no se imputan automáticamente.");
        Console.WriteLine("ATENCION: los períodos en REVISAR no implican estar al día ni deber una cuota. " +
            "No habilitar indicadores financieros en producción hasta conciliar todos los activos.");
        Console.WriteLine($"Estado por alumno al corte: " +
            $"{estados.Count(x => x.Estado == "PENDIENTE_REVISION")} pendientes de revision; " +
            $"{estados.Count(x => x.Estado == "DEBE_UNA")} con 1 cuota vencida confirmada; " +
            $"{estados.Count(x => x.Estado == "DEBE_MAS_DE_UNA")} con 2+ cuotas vencidas confirmadas; " +
            $"{estados.Count(x => x.Estado == "AL_DIA_AL_CORTE")} sin deuda vencida conocida y sin periodos vencidos inciertos.");
        Console.WriteLine("IMPORTANTE: el reporte de estados NO actualiza la UI. " +
            "Hasta adaptar Turnera/Asistencia, NO interpretar la ausencia de cuota como 'Al dia'.");
        Console.WriteLine($"Reportes: {Path.GetFullPath(opt.ReportDirectory)}");
        if (!aplicar)
        {
            Console.WriteLine("VISTA PREVIA: no se modificó PostgreSQL.");
            return;
        }

        // Validacion previa: si hay pagos faltantes/diferentes, abortar sin iniciar escrituras.
        var pagosExistentes = await db.Pagos.AsNoTracking()
            .Where(x => x.ReferenciaExterna != null && x.ReferenciaExterna.StartsWith("LEGACY-PAGO-"))
            .Select(x => new { x.ReferenciaExterna, x.Importe }).ToListAsync(ct);
        var pMap = pagosExistentes.Where(x => x.ReferenciaExterna != null)
            .GroupBy(x => x.ReferenciaExterna!)
            .ToDictionary(x => x.Key, x => x.ToList());
        var diferencias = new List<string>();
        foreach (var p in aptos.SelectMany(x => x.Pagos).Where(x => x.Importe > 0m)
            .GroupBy(x => x.Id).Select(x => x.First()))
        {
            var key = $"LEGACY-PAGO-{p.Id}";
            if (!pMap.TryGetValue(key, out var found) || found.Count != 1)
                diferencias.Add($"{p.Id};{key};PAGO_AUSENTE_O_DUPLICADO");
            else if (found[0].Importe != p.Importe)
                diferencias.Add($"{p.Id};{key};IMPORTE_DISTINTO");
        }
        File.WriteAllLines(Path.Combine(opt.ReportDirectory, "finanzas_v48_preflight_pagos.csv"),
            new[] { "IDPAGO;REFERENCIA;INCIDENCIA" }.Concat(diferencias), Encoding.UTF8);
        if (diferencias.Count > 0)
            throw new InvalidOperationException($"Hay {diferencias.Count} pagos inconsistentes en PostgreSQL. " +
                "No se hizo COMMIT. Revisar finanzas_v48_preflight_pagos.csv.");

        var cuotasPrevias = await db.Cuotas.AsNoTracking()
            .Where(x => x.Periodo.Year == 2026)
            .Select(x => new { x.InscripcionId, x.Periodo }).ToListAsync(ct);
        var claves = aptos.Select(x => (x.Inscripcion, x.Mes)).ToHashSet();
        var superpuestas = cuotasPrevias.Count(x => claves.Contains((x.InscripcionId, x.Periodo.Month)));
        if (superpuestas > 0)
            throw new InvalidOperationException($"Existen {superpuestas} cuotas que se solapan con esta conciliacion. " +
                "No se ejecuta por segunda vez sobre una base ya modificada.");

        // Operacion atomica. Si detectamos una cuota preexistente ajena, revertimos todo.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = await db.Cuotas.AsNoTracking()
                .Where(x => x.Periodo.Year == 2026)
                .Select(x => new { x.Id, x.InscripcionId, x.Periodo }).ToListAsync(ct);
            var byKey = existing.ToDictionary(x => (x.InscripcionId, x.Periodo.Month));
            var currentPays = await db.Pagos.AsNoTracking()
                .Where(x => x.ReferenciaExterna != null && x.ReferenciaExterna.StartsWith("LEGACY-PAGO-"))
                .Select(x => new { x.Id, x.ReferenciaExterna, x.Importe }).ToListAsync(ct);
            var pgMap = currentPays.Where(x => x.ReferenciaExterna != null)
                .GroupBy(x => x.ReferenciaExterna!).ToDictionary(x => x.Key, x => x.First());
            var imputaciones = await db.ImputacionesPago.AsNoTracking().ToListAsync(ct);
            var imputedByPay = imputaciones.GroupBy(x => x.PagoId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Importe));
            var created = 0;
            var allocated = 0;
            foreach (var d in aptos)
            {
                var period = new DateOnly(2026, d.Mes, 1);
                var id = MigrationHelpers.StableGuid($"cuota:{d.Persona}:2026:{d.Mes}");
                if (byKey.TryGetValue((d.Inscripcion, d.Mes), out var old))
                {
                    if (old.Id != id)
                        throw new InvalidOperationException($"Cuota manual preexistente para {d.Persona} mes {d.Mes}; no sobrescribir.");
                    // Una reejecucion no debe omitir silenciosamente imputaciones faltantes.
                    throw new InvalidOperationException($"Cuota migrada ya existe para {d.Persona}/{d.Mes}. " +
                        "No repetir sobre una base modificada sin verificar estado y restaurar un respaldo.");
                }
                var paid = Math.Max(0m, d.TotalPagosCuota);
                var cuota = new Cuota
                {
                    Id = id, InscripcionId = d.Inscripcion,
                    Periodo = period, FechaVencimiento = new DateOnly(2026, d.Mes, 15),
                    Importe = d.ImporteCuota,
                    Estado = d.Saldo == 0m ? EstadoCuota.Pagada
                        : (paid > 0m ? EstadoCuota.Parcial
                            : (corte > new DateOnly(2026, d.Mes, 15) ? EstadoCuota.Vencida : EstadoCuota.Pendiente)),
                    Descripcion = "Reconstruida con evidencia MySQL. " + d.Motivo,
                    CreadoUtc = DateTimeOffset.UtcNow
                };
                db.Cuotas.Add(cuota);
                created++;
                // Imputar pagos existentes, sin crear pagos ni exceder el importe de la cuota.
                var remainder = d.ImporteCuota;
                foreach (var p in d.Pagos.Where(x => x.Importe > 0).OrderBy(x => x.FechaPago).ThenBy(x => x.Id))
                {
                    if (!pgMap.TryGetValue($"LEGACY-PAGO-{p.Id}", out var pg))
                        throw new InvalidOperationException($"Falta pago importado LEGACY-PAGO-{p.Id}.");
                    if (pg.Importe != p.Importe)
                        throw new InvalidOperationException($"Pago {p.Id}: importe PostgreSQL ({F(pg.Importe)}) " +
                            $"difiere de origen MySQL ({F(p.Importe)}). No aplicar.");
                    var free = pg.Importe - imputedByPay.GetValueOrDefault(pg.Id);
                    if (free <= 0) continue;
                    var value = Math.Min(remainder, Math.Min(free, p.Importe));
                    if (value <= 0) continue;
                    db.ImputacionesPago.Add(new ImputacionPago { PagoId = pg.Id, CuotaId = id, Importe = value });
                    imputedByPay[pg.Id] = imputedByPay.GetValueOrDefault(pg.Id) + value;
                    allocated++;
                    remainder -= value;
                }
                // Todo cobro declarado debe existir en PostgreSQL y quedar imputado.
                // Un saldo impago documentado se deja libre; no se inventan pagos.
                if (remainder != d.Saldo)
                    throw new InvalidOperationException($"Cuota {d.Persona}/{d.Mes}: saldo restante " +
                        $"{F(remainder)} diferente de saldo confirmado {F(d.Saldo)}. Se revierte toda la operación.");
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            Console.WriteLine($"COMMIT Finanzas: cuotas nuevas {created}; imputaciones nuevas {allocated}.");
            Console.WriteLine($"Pendientes de revisión: {revisar}. NO se consideran automáticamente al día.");
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch { /* Npgsql puede haber invalidado la transaccion tras el error. */ }
            throw;
        }
    }

    private static Decision Classify(long persona, Guid bebe, Guid ins, DateOnly start,
        int mes, decimal paid, decimal debt, decimal? tarifa,
        List<LegacyPago> payments, DateOnly corte)
    {
        Decision D(string accion, decimal importe, decimal saldo, string motivo) =>
            new(persona, bebe, ins, mes, paid, debt, tarifa, importe, saldo, accion, motivo, payments);

        if (new DateOnly(2026, mes, 1) > corte)
            return D("REVISAR_FUERA_CORTE", 0, 0, "Mes posterior al corte");
        if (start > new DateOnly(2026, mes, DateTime.DaysInMonth(2026, mes)))
            return D("REVISAR_ALTA", 0, 0, "Inscripción posterior al mes");
        if (payments.Any(x => x.Importe < 0) || paid < 0 || debt < 0)
            return D("REVISAR_AJUSTE", 0, 0, "Movimiento negativo; verificar reversión");
        if (tarifa is null)
            return D("REVISAR_TARIFA", 0, 0,
                "Tarifa neta del mes no confirmada; un pago no prueba cuota cancelada");
        if (tarifa <= 0)
            return D("REVISAR_TARIFA", 0, 0, "Tarifa inválida o bonificación total sin respaldo");
        if (payments.Count > 0 && paid == 0 && debt == 0)
            return D("REVISAR_SIN_EVIDENCIA", 0, 0, "Solo movimientos en cero");
        if (paid == 0 && debt == 0)
            return D("REVISAR_SIN_EVIDENCIA", 0, 0,
                "Sin cobro ni mora: puede haber exención, alta reciente o registro incompleto");
        if (paid > tarifa)
            return D("REVISAR_EXCEDENTE", 0, 0,
                "Pagos mayores que la tarifa neta; investigar anticipo o imputación");
        if (debt > tarifa)
            return D("REVISAR_DEUDA", 0, 0,
                "Saldo moroso superior a tarifa neta verificada");
        if (debt > 0 && paid + debt != tarifa)
            return D("REVISAR_DIFERENCIA", 0, 0,
                $"Cobro + deuda ({F(paid + debt)}) no coincide con tarifa ({F(tarifa.Value)})");
        if (debt == 0 && paid != tarifa)
            return D("REVISAR_PARCIAL_SIN_MORA", 0, 0,
                $"Pago ({F(paid)}) menor a tarifa ({F(tarifa.Value)}) sin saldo confirmado en moroso");

        return D("IMPORTAR_CUOTA", tarifa.Value, debt,
            debt > 0 ? "Tarifa verificada; saldo moroso explícito y pagos conciliados"
                     : "Tarifa verificada; cobro completo y sin mora registrada");
    }

    private static Dictionary<int, decimal> LoadMonthlyTariffs(string reportDir)
    {
        var path = Path.Combine(reportDir, "tarifas_validadas_2026.csv");
        EnsureTemplate(path, "MES;TARIFA_NETA", "11;120000");
        var result = new Dictionary<int, decimal>();
        foreach (var line in File.ReadLines(path, Encoding.UTF8).Skip(1))
        {
            var fields = line.Trim().TrimStart('\uFEFF').Split(';');
            if (fields.Length < 2 || string.IsNullOrWhiteSpace(fields[0])) continue;
            if (!int.TryParse(fields[0].Trim(), out var mes) || mes is < 2 or > 12 ||
                !decimal.TryParse(fields[1].Trim(), NumberStyles.Number, Invariant, out var importe) || importe <= 0)
                throw new InvalidOperationException($"Tarifa mensual inválida: {line}");
            if (!result.TryAdd(mes, importe)) throw new InvalidOperationException($"Mes repetido en tarifas: {mes}");
        }
        return result;
    }

    private static Dictionary<(long Persona, int Mes), decimal> LoadPersonalTariffs(string reportDir)
    {
        var path = Path.Combine(reportDir, "tarifas_excepciones_2026.csv");
        EnsureTemplate(path, "IDPERSONA;MES;TARIFA_NETA", "");
        var result = new Dictionary<(long, int), decimal>();
        foreach (var line in File.ReadLines(path, Encoding.UTF8).Skip(1))
        {
            var fields = line.Trim().TrimStart('\uFEFF').Split(';');
            if (fields.Length < 3 || string.IsNullOrWhiteSpace(fields[0])) continue;
            if (!long.TryParse(fields[0].Trim(), out var persona) || persona <= 0 ||
                !int.TryParse(fields[1].Trim(), out var mes) || mes is < 2 or > 12 ||
                !decimal.TryParse(fields[2].Trim(), NumberStyles.Number, Invariant, out var importe) || importe <= 0)
                throw new InvalidOperationException($"Tarifa individual inválida: {line}");
            if (!result.TryAdd((persona, mes), importe))
                throw new InvalidOperationException($"Excepción duplicada para {persona}/{mes}");
        }
        return result;
    }

    private static void EnsureTemplate(string path, string header, string sample)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) return; // Nunca sobrescribir decisiones validadas por la administrativa.
        var lines = string.IsNullOrEmpty(sample) ? new[] { header } : new[] { header, sample };
        File.WriteAllLines(path, lines, Encoding.UTF8);
    }
}
