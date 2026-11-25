using BebesNadadores.Application.Cuotas;
using BebesNadadores.Application.Cuotas.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Pagos;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Cuotas;

public sealed class BonificacionCuotaService(IDbContextFactory<AppDbContext> factory) : IBonificacionCuotaService
{
    public Task BonificarAsync(Guid cuotaId, decimal importe, string motivo, Guid usuarioId, CancellationToken ct = default)
        => ReintentoTransaccional.EjecutarAsync(() => BonificarInternoAsync(cuotaId, importe, motivo, usuarioId, ct), ct);

    private async Task BonificarInternoAsync(Guid cuotaId, decimal importe, string motivo, Guid usuarioId, CancellationToken ct)
    {
        if (cuotaId == Guid.Empty || usuarioId == Guid.Empty) throw new InvalidOperationException("Cuota o usuario inválido.");
        if (importe <= 0 || decimal.Round(importe, 2) != importe) throw new InvalidOperationException("La bonificación debe ser mayor a cero y tener como máximo dos decimales.");
        motivo = (motivo ?? string.Empty).Trim();
        if (motivo.Length > 500) throw new InvalidOperationException("El motivo no puede superar 500 caracteres.");

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var trx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var cuota = await db.Cuotas.Include(x => x.Imputaciones)
            .FirstOrDefaultAsync(x => x.Id == cuotaId, ct)
            ?? throw new InvalidOperationException("La cuota no existe.");
        if (cuota.Estado == EstadoCuota.Anulada) throw new InvalidOperationException("No se puede bonificar una cuota anulada.");
        var pagado = cuota.Imputaciones.Sum(x => x.Importe);
        var saldo = Math.Max(0m, cuota.Importe - pagado);
        if (importe > saldo) throw new InvalidOperationException($"La bonificación no puede superar el saldo pendiente ({saldo:N2}).");

        cuota.Importe -= importe;
        cuota.ModificadoUtc = DateTimeOffset.UtcNow;
        cuota.ModificadoPorUsuarioId = usuarioId;
        RegistradorCobros.ActualizarEstadoCuota(cuota);
        db.BonificacionesCuota.Add(new BonificacionCuota
        {
            CuotaId = cuota.Id, Importe = importe, Motivo = motivo,
            UsuarioId = usuarioId, FechaUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);
    }

    public Task RevertirAsync(Guid bonificacionId, string motivo, Guid usuarioId, CancellationToken ct = default)
        => ReintentoTransaccional.EjecutarAsync(() => RevertirInternoAsync(bonificacionId, motivo, usuarioId, ct), ct);

    private async Task RevertirInternoAsync(Guid bonificacionId, string motivo, Guid usuarioId, CancellationToken ct)
    {
        if (usuarioId == Guid.Empty) throw new InvalidOperationException("Usuario inválido.");
        motivo = (motivo ?? string.Empty).Trim();
        if (motivo.Length < 3 || motivo.Length > 500) throw new InvalidOperationException("Indicá el motivo de la reversión (3 a 500 caracteres).");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var trx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var bonificacion = await db.BonificacionesCuota.Include(x => x.Cuota)
            .ThenInclude(x => x.Imputaciones).FirstOrDefaultAsync(x => x.Id == bonificacionId, ct)
            ?? throw new InvalidOperationException("La bonificación no existe.");
        if (bonificacion.AnuladaUtc.HasValue) throw new InvalidOperationException("Esta bonificación ya fue revertida.");
        if (bonificacion.Cuota.Estado == EstadoCuota.Anulada) throw new InvalidOperationException("Primero hay que reactivar la cuota anulada.");
        bonificacion.Cuota.Importe += bonificacion.Importe;
        bonificacion.Cuota.ModificadoUtc = DateTimeOffset.UtcNow;
        bonificacion.Cuota.ModificadoPorUsuarioId = usuarioId;
        bonificacion.AnuladaUtc = DateTimeOffset.UtcNow;
        bonificacion.AnuladaPorUsuarioId = usuarioId;
        bonificacion.MotivoAnulacion = motivo;
        RegistradorCobros.ActualizarEstadoCuota(bonificacion.Cuota);
        await db.SaveChangesAsync(ct);
        await trx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<BonificacionCuotaDto>> ListarAsync(DateOnly? periodo = null, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var consulta = db.BonificacionesCuota.AsNoTracking().Include(x => x.Cuota)
            .ThenInclude(x => x.Inscripcion).ThenInclude(x => x.Bebe).AsQueryable();
        if (periodo.HasValue)
        {
            var mes = new DateOnly(periodo.Value.Year, periodo.Value.Month, 1);
            consulta = consulta.Where(x => x.Cuota.Periodo == mes);
        }
        var filas = await consulta.OrderByDescending(x => x.FechaUtc).ToListAsync(ct);
        var usuariosIds = filas.Select(x => x.UsuarioId).Distinct().ToList();
        var usuarios = await db.Users.AsNoTracking().Where(x => usuariosIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.NombreCompleto, ct);
        return filas.Select(x => new BonificacionCuotaDto(
            x.Id, x.Cuota.Inscripcion.BebeId,
            $"{x.Cuota.Inscripcion.Bebe.Apellido}, {x.Cuota.Inscripcion.Bebe.Nombre}",
            x.Cuota.Periodo, x.Importe, x.Motivo, x.FechaUtc,
            usuarios.GetValueOrDefault(x.UsuarioId) ?? "Usuario desconocido",
            x.AnuladaUtc.HasValue, x.MotivoAnulacion)).ToList();
    }
}
