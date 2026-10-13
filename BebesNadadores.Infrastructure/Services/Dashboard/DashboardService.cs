using System.Globalization;
using BebesNadadores.Application.Asistencia;
using BebesNadadores.Application.Caja;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Dashboard;
using BebesNadadores.Application.Dashboard.Dtos;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Dashboard;

public sealed class DashboardService(
    IDbContextFactory<AppDbContext> factory,
    ITurneraService turneraService,
    IRecuperosService recuperosService,
    IAsistenciaService asistenciaService,
    ICajaService cajaService,
    IConfiguracionSistemaService configuracionService)
    : IDashboardService
{
    private const int DiasAlertaCumpleSiete = 30;

    private static readonly CultureInfo CulturaArgentina =
        new("es-AR");


    public async Task<DashboardDto> ObtenerAsync(
        CancellationToken ct = default)
    {
        var configuracion =
            await configuracionService.ObtenerAsync(ct);

        var ahoraArgentina =
            ObtenerAhoraArgentina();

        var hoy =
            DateOnly.FromDateTime(
                ahoraArgentina.DateTime);


        // =====================================================
        // BEBÉS + CUOTAS + APTOS
        // =====================================================

        await using var db =
            await factory.CreateDbContextAsync(ct);


        var bebesActivos =
            await db.Bebes
                .AsNoTracking()
                .Where(x =>
                    x.Estado ==
                        EstadoBebe.Activo)
                .Select(x => new
                {
                    x.Id,
                    x.FechaAlta,
                    x.FechaNacimiento
                })
                .ToListAsync(ct);


        var altasMes =
            bebesActivos.Count(x =>
                x.FechaAlta.Year == hoy.Year
                &&
                x.FechaAlta.Month == hoy.Month);


        var bebeIdsActivos =
            bebesActivos
                .Select(x => x.Id)
                .ToList();


        var cuotas =
            await db.Cuotas
                .AsNoTracking()
                .Where(x =>
                    x.Estado !=
                        EstadoCuota.Anulada)
                .Select(x => new
                {
                    x.Importe,
                    Pagado =
                        x.Imputaciones
                            .Sum(i =>
                                (decimal?)i.Importe)
                        ?? 0m
                })
                .ToListAsync(ct);


        var saldosCuotas =
            cuotas
                .Select(x =>
                    Math.Max(
                        0m,
                        x.Importe - x.Pagado))
                .Where(x =>
                    x > 0m)
                .ToList();


        var cuotasPendientes =
            saldosCuotas.Count;

        var importePendiente =
            saldosCuotas.Sum();


        var resumenMedico =
            await ObtenerResumenMedicoAsync(
                db,
                bebeIdsActivos,
                hoy,
                configuracion.DiasAlertaApto,
                ct);


        var bebesProximosSiete =
            bebesActivos.Count(x =>
            {
                var cumpleSiete =
                    x.FechaNacimiento
                        .AddMonths(
                            configuracion.EdadMaximaMeses);

                return
                    cumpleSiete >= hoy
                    &&
                    cumpleSiete <=
                        hoy.AddDays(
                            DiasAlertaCumpleSiete);
            });


        // =====================================================
        // TURNERA + RECUPEROS
        // =====================================================

        var agendaHoy =
            await turneraService
                .ObtenerAgendaAsync(
                    hoy,
                    hoy,
                    ct);


        var panelRecuperos =
            await recuperosService
                .ObtenerPanelAsync(
                    hoy,
                    hoy.AddDays(7),
                    ct);


        var clasesHoy =
            agendaHoy
                .OrderBy(x =>
                    x.HoraInicio)
                .ThenBy(x =>
                    x.Nivel)
                .Select(x =>
                {
                    var profesores =
                        x.Profesores.Count == 0
                            ? "Sin profesor"
                            : string.Join(
                                " + ",
                                x.Profesores
                                    .Select(p =>
                                        p.NombreCompleto));


                    string ocupacion;
                    string estadoCss;


                    if (x.Estado ==
                        EstadoSesionClase.Cancelada)
                    {
                        ocupacion =
                            "Cancelada";

                        estadoCss =
                            "warning";
                    }
                    else
                    {
                        ocupacion =
                            $"{x.Inscriptos} / {x.Cupo}";

                        estadoCss =
                            x.Vacantes <= 0
                                ? "full"
                                : x.Vacantes <= 2
                                    ? "warning"
                                    : "available";
                    }


                    return new DashboardClaseDto(
                        x.Id,
                        x.HoraInicio.ToString("HH:mm"),
                        $"{x.DuracionMinutos} min",
                        x.Nivel,
                        profesores,
                        ocupacion,
                        estadoCss);
                })
                .ToList();


        var vacantesHoy =
            panelRecuperos
                .Vacantes
                .Where(x =>
                    x.Fecha == hoy)
                .Sum(x =>
                    x.VacantesParaRecupero);


        var clasesConVacantesHoy =
            panelRecuperos
                .Vacantes
                .Count(x =>
                    x.Fecha == hoy
                    &&
                    x.VacantesParaRecupero > 0);


        var vacantes =
            panelRecuperos
                .Vacantes
                .Where(x =>
                    x.Fecha >= hoy
                    &&
                    x.VacantesParaRecupero > 0)
                .OrderBy(x =>
                    x.Fecha)
                .ThenBy(x =>
                    x.HoraInicio)
                .Take(5)
                .Select(x =>
                    new DashboardVacanteDto(
                        x.SesionClaseId,
                        ObtenerTextoDia(
                            x.Fecha,
                            hoy),
                        x.HoraInicio.ToString("HH:mm"),
                        x.Nivel,
                        x.VacantesParaRecupero))
                .ToList();


        // =====================================================
        // CAJA
        // =====================================================

        var cajaHoy =
            await cajaService
                .ObtenerHoyAsync(ct);


        var caja =
            cajaHoy is null
                ? new DashboardCajaDto()
                : new DashboardCajaDto
                {
                    Existe = true,
                    Estado = cajaHoy.Estado,
                    SaldoInicial = cajaHoy.SaldoInicial,
                    Ingresos = cajaHoy.IngresosEfectivo,
                    Egresos = cajaHoy.EgresosEfectivo,
                    SaldoEsperado = cajaHoy.SaldoEsperado
                };


        // =====================================================
        // ASISTENCIA - ÚLTIMOS 7 DÍAS
        // =====================================================

        var asistenciaSemanal =
            new List<DashboardAsistenciaDiaDto>();

        var presentesSemana = 0;
        var marcadosSemana = 0;


        for (var i = 6; i >= 0; i--)
        {
            var fecha =
                hoy.AddDays(-i);


            var sesiones =
                await asistenciaService
                    .ListarSesionesDiaAsync(
                        fecha,
                        ct);


            var sesionesValidas =
                sesiones
                    .Where(x =>
                        x.EstadoSesion !=
                            EstadoSesionClase.Cancelada)
                    .ToList();


            if (sesionesValidas.Count == 0)
            {
                continue;
            }


            var presentes =
                sesionesValidas.Sum(x =>
                    x.Presentes);


            var marcados =
                sesionesValidas.Sum(x =>
                    x.Presentes
                    + x.Ausentes
                    + x.AusentesConAviso);


            var porcentaje =
                marcados == 0
                    ? 0
                    : (int)Math.Round(
                        presentes * 100d /
                        marcados);


            asistenciaSemanal.Add(
                new DashboardAsistenciaDiaDto(
                    ObtenerDiaAbreviado(fecha),
                    porcentaje));


            presentesSemana +=
                presentes;

            marcadosSemana +=
                marcados;
        }


        var porcentajeAsistenciaSemanal =
            marcadosSemana == 0
                ? 0
                : (int)Math.Round(
                    presentesSemana * 100d /
                    marcadosSemana);


        // =====================================================
        // RESULTADO
        // =====================================================

        return new DashboardDto
        {
            BebesActivos =
                bebesActivos.Count,

            AltasMes =
                altasMes,

            CuotasPendientes =
                cuotasPendientes,

            ImportePendiente =
                importePendiente,

            RecuperosDisponibles =
                panelRecuperos.Resumen.Disponibles,

            RecuperosVencenProximos =
                panelRecuperos.Resumen.VencenProximos,

            AptosVencidos =
                resumenMedico.Vencidos,

            AptosSinApto =
                resumenMedico.SinApto,

            AptosPorVencer =
                resumenMedico.PorVencer,

            AptosObservados =
                resumenMedico.Observados,

            AptosNoAptos =
                resumenMedico.NoAptos,

            VacantesHoy =
                vacantesHoy,

            ClasesConVacantesHoy =
                clasesConVacantesHoy,

            BebesProximosSiete =
                bebesProximosSiete,

            PorcentajeAsistenciaSemanal =
                porcentajeAsistenciaSemanal,

            ClasesHoy =
                clasesHoy,

            Vacantes =
                vacantes,

            AsistenciaSemanal =
                asistenciaSemanal,

            Caja =
                caja
        };
    }


    // =========================================================
    // APTOS MÉDICOS - SOLO BEBÉS ACTIVOS
    // =========================================================

    private static async Task<ResumenMedicoInterno>
        ObtenerResumenMedicoAsync(
            AppDbContext db,
            IReadOnlyCollection<Guid> bebeIds,
            DateOnly hoy,
            int diasAlertaApto,
            CancellationToken ct)
    {
        if (bebeIds.Count == 0)
        {
            return new ResumenMedicoInterno(
                0,
                0,
                0,
                0,
                0);
        }


        var aptos =
            await db.AptosMedicos
                .AsNoTracking()
                .Where(x =>
                    bebeIds.Contains(
                        x.BebeId))
                .OrderByDescending(x =>
                    x.FechaRevision)
                .ThenByDescending(x =>
                    x.ModificadoUtc ??
                    x.CreadoUtc)
                .ToListAsync(ct);


        var ultimos =
            aptos
                .GroupBy(x =>
                    x.BebeId)
                .ToDictionary(
                    x => x.Key,
                    x => x.First());


        var sinApto = 0;
        var vencidos = 0;
        var porVencer = 0;
        var observados = 0;
        var noAptos = 0;


        foreach (var bebeId in bebeIds)
        {
            if (!ultimos.TryGetValue(
                bebeId,
                out var apto))
            {
                sinApto++;
                continue;
            }


            if (apto.Resultado ==
                ResultadoAptoMedico.NoApto)
            {
                noAptos++;
                continue;
            }


            if (apto.Resultado ==
                ResultadoAptoMedico.Observado)
            {
                observados++;
                continue;
            }


            if (apto.VigenciaHasta < hoy)
            {
                vencidos++;
                continue;
            }


            if (apto.VigenciaHasta <=
                hoy.AddDays(
                    diasAlertaApto))
            {
                porVencer++;
            }
        }


        return new ResumenMedicoInterno(
            sinApto,
            vencidos,
            porVencer,
            observados,
            noAptos);
    }


    // =========================================================
    // FECHAS
    // =========================================================

    private static string ObtenerTextoDia(
        DateOnly fecha,
        DateOnly hoy)
    {
        if (fecha == hoy)
            return "Hoy";

        if (fecha == hoy.AddDays(1))
            return "Mañana";

        return ObtenerDiaAbreviado(
            fecha);
    }


    private static string ObtenerDiaAbreviado(
        DateOnly fecha)
    {
        var texto =
            fecha
                .ToDateTime(
                    TimeOnly.MinValue)
                .ToString(
                    "ddd",
                    CulturaArgentina)
                .TrimEnd('.');


        if (string.IsNullOrWhiteSpace(
            texto))
        {
            return string.Empty;
        }


        return char.ToUpper(
                   texto[0],
                   CulturaArgentina)
               + texto[1..];
    }


    private static DateTimeOffset
        ObtenerAhoraArgentina()
    {
        return TimeZoneInfo.ConvertTime(
            DateTimeOffset.UtcNow,
            ObtenerZonaHorariaArgentina());
    }


    private static TimeZoneInfo
        ObtenerZonaHorariaArgentina()
    {
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
    }


    private sealed record ResumenMedicoInterno(
        int SinApto,
        int Vencidos,
        int PorVencer,
        int Observados,
        int NoAptos);
}
