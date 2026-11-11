using System.Data;
using System.Security.Claims;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Web.Services.Recuperos;

public sealed record AlumnoRegularizacionDto(Guid BebeId, Guid InscripcionId, string Nombre, Guid NivelId, string Nivel, int CreditosExistentes);

public sealed class RegularizacionHistoricaService(
    IDbContextFactory<AppDbContext> factory,
    AuthenticationStateProvider authentication)
{
    public async Task<List<AlumnoRegularizacionDto>> BuscarAsync(string texto, CancellationToken ct = default)
    {
        await ExigirAdministradorAsync();
        if (string.IsNullOrWhiteSpace(texto) || texto.Trim().Length < 3) return [];
        var filtro = $"%{texto.Trim()}%";
        await using var db = await factory.CreateDbContextAsync(ct);
        var filas = await (from i in db.Inscripciones.AsNoTracking()
                           join it in db.InscripcionesTurnos.AsNoTracking() on i.Id equals it.InscripcionId
                           join t in db.Turnos.AsNoTracking() on it.TurnoId equals t.Id
                           join n in db.Niveles.AsNoTracking() on t.NivelId equals n.Id
                           where t.Nombre.StartsWith("Oficial 2026") &&
                                 (EF.Functions.ILike(i.Bebe.Nombre, filtro) || EF.Functions.ILike(i.Bebe.Apellido, filtro))
                           select new { i.BebeId, InscripcionId = i.Id, Nombre = i.Bebe.Nombre + " " + i.Bebe.Apellido, NivelId = n.Id, Nivel = n.Nombre })
                           .Distinct().Take(40).ToListAsync(ct);
        var ids = filas.Select(x => x.BebeId).Distinct().ToArray();
        var contadores = await db.Recuperaciones.AsNoTracking()
            .Where(r => ids.Contains(r.BebeId) && r.Tipo == TipoRecuperacion.SaldoHistoricoVerificado)
            .GroupBy(r => r.BebeId)
            .Select(g => new { Id = g.Key, Cantidad = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Cantidad, ct);
        return filas.Select(x => new AlumnoRegularizacionDto(x.BebeId,x.InscripcionId,x.Nombre,x.NivelId,x.Nivel,contadores.GetValueOrDefault(x.BebeId))).ToList();
    }

    public async Task AcreditarAsync(AlumnoRegularizacionDto alumno, int cantidad,
        DateOnly vencimiento, string observaciones, bool verificado, CancellationToken ct = default)
    {
        var usuario = await ExigirAdministradorAsync();
        if (!verificado) throw new InvalidOperationException("Confirmá la verificación del saldo en el sistema anterior.");
        if (cantidad < 1 || cantidad > 10) throw new InvalidOperationException("La cantidad permitida es de 1 a 10 recuperos. Para otros casos solicitá revisión.");
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));
        if (vencimiento < hoy) throw new InvalidOperationException("No se puede acreditar un recupero ya vencido.");
        if (vencimiento > hoy.AddDays(365)) throw new InvalidOperationException("El vencimiento excede un año; requiere revisión.");
        if (string.IsNullOrWhiteSpace(observaciones) || observaciones.Trim().Length < 12)
            throw new InvalidOperationException("Detallá la fuente y el saldo consultado en el sistema anterior (mínimo 12 caracteres).");

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        bool asignacion = await (from i in db.Inscripciones
                                 join it in db.InscripcionesTurnos on i.Id equals it.InscripcionId
                                 join t in db.Turnos on it.TurnoId equals t.Id
                                 where i.Id == alumno.InscripcionId && i.BebeId == alumno.BebeId &&
                                       t.NivelId == alumno.NivelId && t.Nombre.StartsWith("Oficial 2026")
                                 select i.Id).AnyAsync(ct);
        if (!asignacion) throw new InvalidOperationException("El alumno ya no tiene una asignación oficial compatible.");
        if (await db.Recuperaciones.AnyAsync(r => r.BebeId == alumno.BebeId && r.Tipo == TipoRecuperacion.SaldoHistoricoVerificado, ct))
            throw new InvalidOperationException("Este alumno ya tiene una regularización histórica. No se permite acreditar dos veces.");

        for (var numero = 1; numero <= cantidad; numero++)
        {
            db.Recuperaciones.Add(new Recuperacion
            {
                Id = Guid.NewGuid(), BebeId = alumno.BebeId, InscripcionId = alumno.InscripcionId,
                SesionOrigenId = null, NivelOrigenId = alumno.NivelId,
                NumeroCreditoHistorico = numero, FechaVerificacionHistorica = hoy,
                ObservacionesRegularizacion = observaciones.Trim()[..Math.Min(500, observaciones.Trim().Length)],
                Tipo = TipoRecuperacion.SaldoHistoricoVerificado, Estado = EstadoRecuperacion.Disponible,
                FechaGeneracion = hoy, FechaVencimiento = vencimiento,
                CreadoPorUsuarioId = usuario, CreadoUtc = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private async Task<Guid> ExigirAdministradorAsync()
    {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        if (principal.Identity?.IsAuthenticated != true || !principal.IsInRole(RolesSistema.Administrador))
            throw new UnauthorizedAccessException("Solo un administrador puede regularizar recuperos históricos.");
        var id = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(id, out var userId)) throw new UnauthorizedAccessException("No se pudo identificar al administrador.");
        return userId;
    }
}
