using BebesNadadores.Application.ListaEspera;
using BebesNadadores.Application.ListaEspera.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.ListaEspera;

public sealed class ListaEsperaService(IDbContextFactory<AppDbContext> factory) : IListaEsperaService
{
    public async Task<IReadOnlyList<ListaEsperaItemDto>> ListarAsync(bool incluirFinalizadas = false, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.EsperaInscripciones.AsNoTracking().Include(x => x.Nivel).Include(x => x.Turno).AsQueryable();
        if (!incluirFinalizadas) query = query.Where(x => x.Estado == EstadoListaEspera.Activa || x.Estado == EstadoListaEspera.Notificada);
        var items = await query.OrderBy(x => x.Estado).ThenBy(x => x.FechaAltaUtc).ToListAsync(ct);
        return items.Select(x => new ListaEsperaItemDto(x.Id, x.NombreBebe, x.NombreResponsable,
            x.Telefono, x.Email, x.NivelId, x.Nivel.Nombre, x.TurnoId, x.Turno?.Nombre,
            x.DiaPreferido, x.FranjaHoraria, x.Estado, x.FechaAltaUtc, x.FechaNotificacionUtc,
            x.Observaciones, x.BebeInscriptoId)).ToList();
    }

    public async Task<IReadOnlyList<NivelEsperaDto>> ListarNivelesAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Niveles.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new NivelEsperaDto(x.Id, x.Nombre)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TurnoEsperaDto>> ListarTurnosAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Turnos.AsNoTracking().Where(x => x.Estado == EstadoTurno.Activo)
            .OrderBy(x => x.DiaSemana).ThenBy(x => x.HoraInicio)
            .Select(x => new TurnoEsperaDto(x.Id, x.NivelId, x.Nombre, x.DiaSemana, x.HoraInicio))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BebeInscritoEsperaDto>> BuscarInscritosAsync(string texto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        texto = (texto ?? "").Trim().ToLower();
        if (texto.Length < 2) return [];
        return await db.Inscripciones.AsNoTracking()
            .Where(x => x.Estado == EstadoInscripcion.Activa &&
                (x.Bebe.Nombre.ToLower().Contains(texto) || x.Bebe.Apellido.ToLower().Contains(texto)))
            .Select(x => new BebeInscritoEsperaDto(x.BebeId, x.Bebe.Nombre + " " + x.Bebe.Apellido))
            .Distinct().Take(20).ToListAsync(ct);
    }

    public async Task<Guid> AgregarAsync(CrearListaEsperaDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await ValidarAsync(db, dto, ct);
        var telefono = dto.Telefono.Trim();
        if (await db.EsperaInscripciones.AnyAsync(x => x.Telefono == telefono && x.NivelId == dto.NivelId &&
            (x.Estado == EstadoListaEspera.Activa || x.Estado == EstadoListaEspera.Notificada), ct))
            throw new InvalidOperationException("Ya hay una solicitud activa para ese teléfono y nivel. Editá la existente.");
        var item = new EsperaInscripcion { Id = Guid.NewGuid() };
        Copiar(item, dto);
        db.EsperaInscripciones.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task ActualizarAsync(Guid id, ActualizarListaEsperaDto dto, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.EsperaInscripciones.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");
        if (item.Estado is EstadoListaEspera.Asignada or EstadoListaEspera.Cancelada or EstadoListaEspera.Vencida)
            throw new InvalidOperationException("No podés editar una solicitud finalizada.");
        await ValidarAsync(db, dto, ct);
        if (await db.EsperaInscripciones.AnyAsync(x => x.Id != id && x.Telefono == dto.Telefono.Trim() &&
            x.NivelId == dto.NivelId && (x.Estado == EstadoListaEspera.Activa || x.Estado == EstadoListaEspera.Notificada), ct))
            throw new InvalidOperationException("Ya hay una solicitud activa para ese teléfono y nivel.");
        Copiar(item, dto);
        await db.SaveChangesAsync(ct);
    }

    public async Task CambiarEstadoAsync(Guid id, EstadoListaEspera estado, Guid? bebeInscriptoId = null, CancellationToken ct = default)
    {
        if (estado is not (EstadoListaEspera.Activa or EstadoListaEspera.Notificada or EstadoListaEspera.Asignada or EstadoListaEspera.Cancelada))
            throw new InvalidOperationException("Estado inválido.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var item = await db.EsperaInscripciones.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("La solicitud no existe.");
        if (item.Estado == EstadoListaEspera.Asignada && estado != EstadoListaEspera.Asignada)
            throw new InvalidOperationException("Una inscripción concretada no se puede reabrir.");
        if (estado == EstadoListaEspera.Asignada)
        {
            if (!bebeInscriptoId.HasValue || !await db.Bebes.AnyAsync(x => x.Id == bebeInscriptoId.Value, ct))
                throw new InvalidOperationException("Para marcar como inscripto, vinculá un bebé existente.");
            if (!await db.Inscripciones.AnyAsync(x => x.BebeId == bebeInscriptoId.Value && x.Estado == EstadoInscripcion.Activa, ct))
                throw new InvalidOperationException("El bebé seleccionado no tiene inscripción activa.");
            item.BebeInscriptoId = bebeInscriptoId;
        }
        item.Estado = estado;
        if (estado == EstadoListaEspera.Notificada) item.FechaNotificacionUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private static async Task ValidarAsync(AppDbContext db, CrearListaEsperaDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.NombreBebe) || dto.NombreBebe.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(dto.NombreResponsable) || dto.NombreResponsable.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(dto.Telefono) || dto.Telefono.Trim().Length > 60)
            throw new InvalidOperationException("Completá nombre del niño, responsable y teléfono (máximo 200/200/60 caracteres).");
        if (dto.Email?.Length > 200 || dto.Observaciones?.Length > 1000)
            throw new InvalidOperationException("Email u observaciones superan el largo permitido.");
        if (!await db.Niveles.AnyAsync(x => x.Id == dto.NivelId && x.Activo, ct))
            throw new InvalidOperationException("Seleccioná un nivel válido.");
        if (dto.TurnoId.HasValue && !await db.Turnos.AnyAsync(x => x.Id == dto.TurnoId.Value && x.NivelId == dto.NivelId && x.Estado == EstadoTurno.Activo, ct))
            throw new InvalidOperationException("El horario no corresponde al nivel seleccionado.");
        if (dto.DiaPreferido.HasValue && !Enum.IsDefined(dto.DiaPreferido.Value))
            throw new InvalidOperationException("Día inválido.");
        if (!Enum.IsDefined(dto.FranjaHoraria)) throw new InvalidOperationException("Franja horaria inválida.");
    }
    private static void Copiar(EsperaInscripcion x, CrearListaEsperaDto dto)
    {
        x.NombreBebe = dto.NombreBebe.Trim(); x.NombreResponsable = dto.NombreResponsable.Trim();
        x.Telefono = dto.Telefono.Trim(); x.Email = dto.Email?.Trim(); x.NivelId = dto.NivelId;
        x.TurnoId = dto.TurnoId; x.DiaPreferido = dto.DiaPreferido; x.FranjaHoraria = dto.FranjaHoraria;
        x.Observaciones = dto.Observaciones?.Trim();
    }
}
