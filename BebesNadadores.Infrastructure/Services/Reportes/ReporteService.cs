using BebesNadadores.Application.AptosMedicos;
using BebesNadadores.Application.AptosMedicos.Dtos;
using BebesNadadores.Application.Reportes;
using BebesNadadores.Application.Reportes.Dtos;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Reportes;

public sealed class ReporteService(
    IDbContextFactory<AppDbContext> factory,
    ITurneraService turneraService,
    IAptoMedicoService aptoMedicoService)
    : IReporteService
{
    private const int MaximoDiasConsulta = 366;

    public async Task<ReporteGeneralDto> ObtenerAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
    {
        ValidarPeriodo(
            desde,
            hasta);

        /*
         * Usamos TurneraService para ocupación porque ahí ya vive
         * la lógica real de cupos:
         *
         * - inscripciones regulares;
         * - reservas de primera clase;
         * - recuperos reservados/utilizados;
         * - sesiones canceladas;
         * - cupo efectivo de cada sesión.
         *
         * Así Reportes no duplica esa lógica.
         */
        var agenda =
            await turneraService.ObtenerAgendaAsync(
                desde,
                hasta,
                ct);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        // =========================================================
        // BEBÉS
        // =========================================================

        var bebesActivos =
            await db.Bebes
                .AsNoTracking()
                .CountAsync(
                    x => x.Estado == EstadoBebe.Activo,
                    ct);

        var altasPeriodo =
            await db.Bebes
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.FechaAlta >= desde &&
                        x.FechaAlta <= hasta,
                    ct);

        // =========================================================
        // ASISTENCIA
        // =========================================================

        var asistencias =
            await (
                from asistencia in db.Asistencias.AsNoTracking()
                join sesion in db.SesionesClase.AsNoTracking()
                    on asistencia.SesionClaseId equals sesion.Id
                where
                    sesion.Fecha >= desde &&
                    sesion.Fecha <= hasta
                select new
                {
                    sesion.Fecha,
                    asistencia.Estado
                })
                .ToListAsync(ct);

        var asistenciaPorDia =
            asistencias
                .GroupBy(x => x.Fecha)
                .OrderBy(x => x.Key)
                .Select(grupo =>
                {
                    var presentes =
                        grupo.Count(x =>
                            x.Estado ==
                            EstadoAsistencia.Presente);

                    var ausentes =
                        grupo.Count(x =>
                            x.Estado ==
                            EstadoAsistencia.Ausente);

                    var conAviso =
                        grupo.Count(x =>
                            x.Estado ==
                            EstadoAsistencia.AusenteConAviso);

                    var pendientes =
                        grupo.Count(x =>
                            x.Estado ==
                            EstadoAsistencia.Pendiente);

                    /*
                     * Los pendientes no entran en el porcentaje:
                     * todavía no representan una asistencia
                     * efectivamente registrada.
                     */
                    var marcados =
                        presentes +
                        ausentes +
                        conAviso;

                    var porcentaje =
                        marcados == 0
                            ? 0m
                            : Math.Round(
                                presentes * 100m / marcados,
                                1);

                    return new AsistenciaDiaReporteDto(
                        grupo.Key,
                        presentes,
                        ausentes,
                        conAviso,
                        pendientes,
                        grupo.Count(),
                        porcentaje);
                })
                .ToList();

        var totalPresentes =
            asistencias.Count(x =>
                x.Estado ==
                EstadoAsistencia.Presente);

        var totalAusentes =
            asistencias.Count(x =>
                x.Estado ==
                EstadoAsistencia.Ausente);

        var totalConAviso =
            asistencias.Count(x =>
                x.Estado ==
                EstadoAsistencia.AusenteConAviso);

        var totalMarcados =
            totalPresentes +
            totalAusentes +
            totalConAviso;

        var asistenciaPromedio =
            totalMarcados == 0
                ? 0m
                : Math.Round(
                    totalPresentes * 100m / totalMarcados,
                    1);

        // =========================================================
        // OCUPACIÓN
        // =========================================================

        var clasesDictables =
            agenda
                .Where(x =>
                    x.Estado !=
                    EstadoSesionClase.Cancelada)
                .ToList();

        var clasesCanceladas =
            agenda.Count(x =>
                x.Estado ==
                EstadoSesionClase.Cancelada);

        var ocupacionPorNivel =
            clasesDictables
                .GroupBy(x =>
                    new
                    {
                        x.NivelId,
                        x.Nivel
                    })
                .Select(grupo =>
                {
                    var cuposTotales =
                        grupo.Sum(x =>
                            x.Cupo);

                    var ocupados =
                        grupo.Sum(x =>
                            x.Inscriptos);

                    var vacantes =
                        grupo.Sum(x =>
                            x.Vacantes);

                    var porcentaje =
                        cuposTotales == 0
                            ? 0m
                            : Math.Round(
                                ocupados * 100m /
                                cuposTotales,
                                1);

                    return new OcupacionNivelReporteDto(
                        grupo.Key.NivelId,
                        grupo.Key.Nivel,
                        grupo.Count(),
                        cuposTotales,
                        ocupados,
                        vacantes,
                        porcentaje);
                })
                .OrderByDescending(x =>
                    x.PorcentajeOcupacion)
                .ThenBy(x =>
                    x.Nivel)
                .ToList();

        var cuposGenerales =
            clasesDictables.Sum(x =>
                x.Cupo);

        var ocupadosGenerales =
            clasesDictables.Sum(x =>
                x.Inscriptos);

        var ocupacionPromedio =
            cuposGenerales == 0
                ? 0m
                : Math.Round(
                    ocupadosGenerales * 100m /
                    cuposGenerales,
                    1);

        // =========================================================
        // RECUPEROS
        // =========================================================

        var recuperos =
            await db.Recuperaciones
                .AsNoTracking()
                .Where(x =>
                    x.FechaGeneracion >= desde &&
                    x.FechaGeneracion <= hasta)
                .Select(x =>
                    new
                    {
                        x.Estado,
                        x.Tipo
                    })
                .ToListAsync(ct);

        var recuperosDto =
            new RecuperosReporteDto(
                recuperos.Count,
                recuperos.Count(x =>
                    x.Estado ==
                    EstadoRecuperacion.Disponible),
                recuperos.Count(x =>
                    x.Estado ==
                    EstadoRecuperacion.Reservada),
                recuperos.Count(x =>
                    x.Estado ==
                    EstadoRecuperacion.Utilizada),
                recuperos.Count(x =>
                    x.Estado ==
                    EstadoRecuperacion.Vencida),
                recuperos.Count(x =>
                    x.Estado ==
                    EstadoRecuperacion.Anulada),
                recuperos
                    .GroupBy(x =>
                        x.Tipo)
                    .Select(grupo =>
                        new RecuperoTipoReporteDto(
                            NombreTipoRecupero(
                                grupo.Key),
                            grupo.Count()))
                    .OrderByDescending(x =>
                        x.Cantidad)
                    .ToList());

        // =========================================================
        // FINANZAS
        // =========================================================

        var (desdeUtc, hastaUtcExclusivo) =
            ObtenerRangoUtcArgentina(
                desde,
                hasta);

        var pagos =
            await db.Pagos
                .AsNoTracking()
                .Where(x =>
                    x.FechaUtc >= desdeUtc &&
                    x.FechaUtc < hastaUtcExclusivo &&
                    // Los pagos anulados no son recaudación.
                    !x.AnuladoUtc.HasValue)
                .Select(x =>
                    new
                    {
                        x.MedioPago,
                        x.Importe
                    })
                .ToListAsync(ct);

        var cobradoPeriodo =
            pagos.Sum(x =>
                x.Importe);

        var porMedio =
            pagos
                .GroupBy(x =>
                    x.MedioPago)
                .Select(grupo =>
                    new PagoMedioReporteDto(
                        NombreMedioPago(
                            grupo.Key),
                        grupo.Count(),
                        grupo.Sum(x =>
                            x.Importe)))
                .OrderByDescending(x =>
                    x.Importe)
                .ToList();

        var cuotasPeriodo =
            await db.Cuotas
                .AsNoTracking()
                .Where(x =>
                    x.FechaVencimiento >= desde &&
                    x.FechaVencimiento <= hasta &&
                    x.Estado != EstadoCuota.Anulada)
                .Select(x =>
                    x.Importe)
                .ToListAsync(ct);

        var facturadoPeriodo =
            cuotasPeriodo.Sum();

        /*
         * Deuda ACTUAL.
         *
         * No es deuda "generada en el período": muestra la foto
         * vigente de lo que todavía debe cada bebé.
         */
        var cuotasDeuda =
            await db.Cuotas
                .AsNoTracking()
                .Where(x =>
                    x.Estado !=
                    EstadoCuota.Anulada)
                .Select(x =>
                    new
                    {
                        BebeId =
                            x.Inscripcion.BebeId,

                        Bebe =
                            (x.Inscripcion.Bebe.Nombre +
                             " " +
                             x.Inscripcion.Bebe.Apellido)
                            .Trim(),

                        x.Importe,

                        Pagado =
                            x.Imputaciones
                                .Select(i =>
                                    (decimal?)i.Importe)
                                .Sum()
                            ?? 0m
                    })
                .ToListAsync(ct);

        var deudaPorBebe =
            cuotasDeuda
                .Select(x =>
                    new
                    {
                        x.BebeId,
                        x.Bebe,
                        Saldo =
                            Math.Max(
                                0m,
                                x.Importe -
                                x.Pagado)
                    })
                .Where(x =>
                    x.Saldo > 0m)
                .GroupBy(x =>
                    new
                    {
                        x.BebeId,
                        x.Bebe
                    })
                .Select(grupo =>
                    new DeudorReporteDto(
                        grupo.Key.BebeId,
                        grupo.Key.Bebe,
                        grupo.Count(),
                        grupo.Sum(x =>
                            x.Saldo)))
                .OrderByDescending(x =>
                    x.Deuda)
                .ThenBy(x =>
                    x.Bebe)
                .ToList();

        var deudaActual =
            deudaPorBebe.Sum(x =>
                x.Deuda);

        var finanzasDto =
            new FinanzasReporteDto(
                cuotasPeriodo.Count,
                facturadoPeriodo,
                pagos.Count,
                cobradoPeriodo,
                deudaActual,
                deudaPorBebe.Count,
                porMedio,
                deudaPorBebe
                    .Take(10)
                    .ToList());

        // =========================================================
        // CAJA
        // =========================================================

        /*
         * MovimientoCaja ya representa movimientos de efectivo.
         * En el modelo actual se agrupa por Concepto.
         */
        var movimientosCaja =
            await db.MovimientosCaja
                .AsNoTracking()
                .Where(x =>
                    x.FechaUtc >= desdeUtc &&
                    x.FechaUtc < hastaUtcExclusivo &&
                    // Un movimiento anulado y su contra-asiento se cancelan:
                    // no se informan como ingreso ni egreso.
                    !x.AnuladoUtc.HasValue &&
                    !x.AnulaMovimientoId.HasValue)
                .Select(x =>
                    new
                    {
                        x.Tipo,
                        x.Concepto,
                        x.Importe
                    })
                .ToListAsync(ct);

        var ingresosCaja =
            movimientosCaja
                .Where(x =>
                    x.Tipo ==
                    TipoMovimientoCaja.Ingreso)
                .Sum(x =>
                    x.Importe);

        var egresosCaja =
            movimientosCaja
                .Where(x =>
                    x.Tipo ==
                    TipoMovimientoCaja.Egreso)
                .Sum(x =>
                    x.Importe);

        var cajaPorCategoria =
            movimientosCaja
                .GroupBy(x =>
                    string.IsNullOrWhiteSpace(
                        x.Concepto)
                        ? "Sin concepto"
                        : x.Concepto.Trim())
                .Select(grupo =>
                    new CajaCategoriaReporteDto(
                        grupo.Key,
                        grupo.Count(),

                        grupo
                            .Where(x =>
                                x.Tipo ==
                                TipoMovimientoCaja.Ingreso)
                            .Sum(x =>
                                x.Importe),

                        grupo
                            .Where(x =>
                                x.Tipo ==
                                TipoMovimientoCaja.Egreso)
                            .Sum(x =>
                                x.Importe)))
                .OrderByDescending(x =>
                    x.Ingresos +
                    x.Egresos)
                .ToList();

        var cajaDto =
            new CajaReporteDto(
                movimientosCaja.Count,
                ingresosCaja,
                egresosCaja,
                ingresosCaja -
                egresosCaja,
                cajaPorCategoria);

        // =========================================================
        // BANCOS
        // =========================================================

        /*
         * Movimientos por cada cuenta bancaria dentro del período.
         * Se muestran también las cuentas sin actividad.
         */
        var cuentasBancarias =
            await db.CuentasBancarias
                .AsNoTracking()
                .Select(x =>
                    new
                    {
                        x.Id,
                        Cuenta = x.Nombre,
                        Banco = x.Banco.Nombre
                    })
                .OrderBy(x =>
                    x.Banco)
                .ThenBy(x =>
                    x.Cuenta)
                .ToListAsync(ct);

        var movimientosBancarios =
            await db.MovimientosBancarios
                .AsNoTracking()
                .Where(x =>
                    x.FechaUtc >= desdeUtc &&
                    x.FechaUtc < hastaUtcExclusivo &&
                    // Un movimiento anulado y su contra-asiento se cancelan:
                    // no se informan como ingreso ni egreso.
                    !x.AnuladoUtc.HasValue &&
                    !x.AnulaMovimientoId.HasValue)
                .Select(x =>
                    new
                    {
                        x.Id,
                        x.CuentaBancariaId,
                        x.FechaUtc,
                        x.Tipo,
                        x.Origen,
                        x.Importe,
                        x.Concepto,
                        x.ReferenciaExterna,
                        x.Observaciones
                    })
                .OrderByDescending(x =>
                    x.FechaUtc)
                .ThenByDescending(x =>
                    x.Id)
                .ToListAsync(ct);

        var cuentasReporte =
            cuentasBancarias
                .Select(cuenta =>
                {
                    var movimientosCuenta =
                        movimientosBancarios
                            .Where(x =>
                                x.CuentaBancariaId ==
                                cuenta.Id)
                            .ToList();

                    var ingresos =
                        movimientosCuenta
                            .Where(x =>
                                x.Tipo ==
                                TipoMovimientoBancario.Ingreso)
                            .Sum(x =>
                                x.Importe);

                    var egresos =
                        movimientosCuenta
                            .Where(x =>
                                x.Tipo ==
                                TipoMovimientoBancario.Egreso)
                            .Sum(x =>
                                x.Importe);

                    var detalle =
                        movimientosCuenta
                            .Select(x =>
                                new MovimientoBancarioReporteDto(
                                    x.Id,
                                    x.FechaUtc,

                                    x.Tipo ==
                                    TipoMovimientoBancario.Ingreso
                                        ? "Ingreso"
                                        : "Egreso",

                                    SepararPascalCase(
                                        x.Origen.ToString()),

                                    x.Concepto,
                                    x.ReferenciaExterna,
                                    x.Observaciones,
                                    x.Importe))
                            .ToList();

                    return new CuentaBancariaReporteDto(
                        cuenta.Id,
                        cuenta.Banco,
                        cuenta.Cuenta,
                        movimientosCuenta.Count,
                        ingresos,
                        egresos,
                        ingresos - egresos,
                        detalle);
                })
                .ToList();

        var ingresosBancos =
            movimientosBancarios
                .Where(x =>
                    x.Tipo ==
                    TipoMovimientoBancario.Ingreso)
                .Sum(x =>
                    x.Importe);

        var egresosBancos =
            movimientosBancarios
                .Where(x =>
                    x.Tipo ==
                    TipoMovimientoBancario.Egreso)
                .Sum(x =>
                    x.Importe);

        var bancosDto =
            new BancosReporteDto(
                movimientosBancarios.Count,
                ingresosBancos,
                egresosBancos,
                ingresosBancos -
                egresosBancos,
                cuentasReporte);

        // =========================================================
        // SALUD - FOTO ACTUAL
        // =========================================================

        /*
         * La salud es un snapshot actual, no histórico.
         * El AptoMedicoService ya aplica la regla configurada
         * de "por vencer".
         */
        var aptos =
            await aptoMedicoService
                .ListarBebesAsync(
                    ct: ct);

        var saludDto =
            new SaludReporteDto(
                aptos.Count(x =>
                    x.Situacion ==
                    SituacionAptoMedico.SinApto),
                aptos.Count(x =>
                    x.Situacion ==
                    SituacionAptoMedico.Vigente),
                aptos.Count(x =>
                    x.Situacion ==
                    SituacionAptoMedico.PorVencer),
                aptos.Count(x =>
                    x.Situacion ==
                    SituacionAptoMedico.Vencido),
                aptos.Count(x =>
                    x.Situacion ==
                    SituacionAptoMedico.Observado),
                aptos.Count(x =>
                    x.Situacion ==
                    SituacionAptoMedico.NoApto));

        // =========================================================
        // RESULTADO
        // =========================================================

        var resumen =
            new ReporteResumenDto(
                bebesActivos,
                altasPeriodo,
                clasesDictables.Count,
                clasesCanceladas,
                asistenciaPromedio,
                ocupacionPromedio,
                cobradoPeriodo,
                deudaActual);

        return new ReporteGeneralDto(
            desde,
            hasta,
            resumen,
            asistenciaPorDia,
            ocupacionPorNivel,
            recuperosDto,
            finanzasDto,
            cajaDto,
            bancosDto,
            saludDto);
    }

    // =============================================================
    // VALIDACIÓN
    // =============================================================

    private static void ValidarPeriodo(
        DateOnly desde,
        DateOnly hasta)
    {
        if (hasta < desde)
        {
            throw new InvalidOperationException(
                "La fecha Hasta no puede ser anterior a Desde.");
        }

        var cantidadDias =
            hasta.DayNumber -
            desde.DayNumber +
            1;

        if (cantidadDias >
            MaximoDiasConsulta)
        {
            throw new InvalidOperationException(
                $"El reporte admite un período máximo de {MaximoDiasConsulta} días.");
        }
    }

    // =============================================================
    // UTC / ARGENTINA
    // =============================================================

    private static (
        DateTimeOffset DesdeUtc,
        DateTimeOffset HastaUtcExclusivo)
        ObtenerRangoUtcArgentina(
            DateOnly desde,
            DateOnly hasta)
    {
        var zona =
            ObtenerZonaArgentina();

        var inicioLocal =
            desde.ToDateTime(
                TimeOnly.MinValue,
                DateTimeKind.Unspecified);

        var finLocalExclusivo =
            hasta
                .AddDays(1)
                .ToDateTime(
                    TimeOnly.MinValue,
                    DateTimeKind.Unspecified);

        var inicioUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                inicioLocal,
                zona);

        var finUtc =
            TimeZoneInfo.ConvertTimeToUtc(
                finLocalExclusivo,
                zona);

        return (
            new DateTimeOffset(
                inicioUtc,
                TimeSpan.Zero),
            new DateTimeOffset(
                finUtc,
                TimeSpan.Zero));
    }

    private static TimeZoneInfo ObtenerZonaArgentina()
    {
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
    }

    // =============================================================
    // TEXTOS
    // =============================================================

    private static string NombreTipoRecupero(
        TipoRecuperacion tipo)
    {
        return tipo.ObtenerTexto();
    }

    private static string NombreMedioPago(
        MedioPago medio)
    {
        return medio switch
        {
            MedioPago.Efectivo =>
                "Efectivo",

            MedioPago.Transferencia =>
                "Transferencia",

            _ =>
                SepararPascalCase(
                    medio.ToString())
        };
    }


    private static string SepararPascalCase(
        string texto)
    {
        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return string.Empty;
        }

        var resultado =
            new System.Text.StringBuilder();

        for (var i = 0;
             i < texto.Length;
             i++)
        {
            if (
                i > 0 &&
                char.IsUpper(
                    texto[i]) &&
                !char.IsWhiteSpace(
                    texto[i - 1]))
            {
                resultado.Append(
                    ' ');
            }

            resultado.Append(
                texto[i]);
        }

        return resultado.ToString();
    }
}
