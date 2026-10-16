using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Recuperos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Pagos;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Recuperos;

/*
 * =============================================================
 * RECUPERO POR INICIO A MITAD DE MES
 * =============================================================
 *
 * Inscripciones marcadas con BonificaRecuperoInicio (arrancaron
 * del 15 en adelante y pagaron media cuota) reciben UN recupero.
 *
 * Origen del recupero = primera clase del bebé: la de su clase
 * inicial reservada o la primera clase de sus turnos desde la
 * fecha de inicio. Si la turnera todavía no tiene generadas esas
 * clases, queda pendiente y lo crea el job diario más adelante.
 *
 * Idempotente: nunca más de un recupero de este tipo por
 * inscripción.
 */
public sealed class GeneradorRecuperosInicioMitadMes(
    IDbContextFactory<AppDbContext> factory,
    IConfiguracionSistemaService configuracionService)
    : IRecuperosInicioMitadMesService
{
    public async Task<int> GenerarPendientesAsync(
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var pendientes =
            await db.Inscripciones
                .AsNoTracking()
                .Include(x => x.Turnos)
                .Where(x =>
                    x.BonificaRecuperoInicio
                    && x.Estado == EstadoInscripcion.Activa
                    && x.Bebe.Estado == EstadoBebe.Activo
                    && !db.Recuperaciones.Any(r =>
                        r.InscripcionId == x.Id
                        && r.Tipo == TipoRecuperacion.InicioMitadDeMes))
                .ToListAsync(ct);

        if (pendientes.Count == 0)
            return 0;

        var configuracion =
            await configuracionService.ObtenerAsync(ct);

        var hoy =
            RegistradorCobros.ObtenerFechaArgentina();

        var creados = 0;

        foreach (var inscripcion in pendientes)
        {
            var origen =
                await ObtenerPrimeraClaseAsync(
                    db,
                    inscripcion,
                    ct);

            if (origen is null)
                continue;

            var desde =
                origen.Fecha > hoy
                    ? origen.Fecha
                    : hoy;

            db.Recuperaciones.Add(
                new Recuperacion
                {
                    Id = Guid.NewGuid(),
                    BebeId = inscripcion.BebeId,
                    InscripcionId = inscripcion.Id,
                    SesionOrigenId = origen.Id,
                    AsistenciaOrigenId = null,
                    Tipo = TipoRecuperacion.InicioMitadDeMes,
                    FechaGeneracion = hoy,
                    FechaVencimiento = desde.AddDays(configuracion.DiasVigenciaRecupero),
                    Estado = EstadoRecuperacion.Disponible
                });

            creados++;
        }

        if (creados > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return creados;
    }


    private static async Task<SesionClase?> ObtenerPrimeraClaseAsync(
        AppDbContext db,
        Inscripcion inscripcion,
        CancellationToken ct)
    {
        var turnoIds =
            inscripcion.Turnos
                .Select(x => x.TurnoId)
                .ToList();

        var primeraRegular =
            await db.SesionesClase
                .AsNoTracking()
                .Where(x =>
                    turnoIds.Contains(x.TurnoId)
                    && x.Fecha >= inscripcion.FechaInicio
                    && x.Estado != EstadoSesionClase.Cancelada)
                .OrderBy(x => x.Fecha)
                .ThenBy(x => x.HoraInicio)
                .FirstOrDefaultAsync(ct);

        var claseInicial =
            await db.ReservasSesionesClase
                .AsNoTracking()
                .Where(x =>
                    x.InscripcionId == inscripcion.Id
                    && x.Estado == EstadoReservaSesion.Activa)
                .Select(x => x.SesionClase)
                .OrderBy(x => x.Fecha)
                .ThenBy(x => x.HoraInicio)
                .FirstOrDefaultAsync(ct);

        if (primeraRegular is null)
            return claseInicial;

        if (claseInicial is null)
            return primeraRegular;

        return (claseInicial.Fecha, claseInicial.HoraInicio)
               .CompareTo((primeraRegular.Fecha, primeraRegular.HoraInicio)) <= 0
            ? claseInicial
            : primeraRegular;
    }
}
