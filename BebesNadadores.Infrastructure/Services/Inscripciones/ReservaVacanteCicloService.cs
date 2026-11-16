using System.Data;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Pagos;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Inscripciones;

public sealed class ReservaVacanteCicloService(
    IDbContextFactory<AppDbContext> factory,
    RegistradorCobros cobrador) : IReservaVacanteCicloService
{
    private const decimal ImporteReserva = 40000m;

    public async Task<Guid> RegistrarAsync(RegistrarReservaVacanteDto datos, CancellationToken ct = default)
    {
        if (datos.UsuarioId == Guid.Empty) throw new InvalidOperationException("No se identificó el usuario del cobro.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var hoy = BebesNadadores.Domain.Common.FechaArgentina.Hoy;
        var ciclo = await db.CiclosLectivos.FirstOrDefaultAsync(x => x.Id == datos.CicloLectivoId, ct)
            ?? throw new InvalidOperationException("El ciclo lectivo no existe.");
        if (ciclo.Anio <= hoy.Year || ciclo.FechaInicio <= hoy || ciclo.Estado == EstadoCicloLectivo.Cerrado)
            throw new InvalidOperationException("Las reservas sólo pueden registrarse para un ciclo FUTURO.");
        var turno = await db.Turnos.FirstOrDefaultAsync(x => x.Id == datos.TurnoId, ct)
            ?? throw new InvalidOperationException("El horario del ciclo no existe.");
        if (turno.CicloLectivoId != ciclo.Id || turno.Estado != EstadoTurno.Activo)
            throw new InvalidOperationException("Seleccioná un turno activo de ese ciclo futuro.");
        var bebe = await db.Bebes.Include(x => x.Responsables).FirstOrDefaultAsync(x => x.Id == datos.BebeId, ct)
            ?? throw new InvalidOperationException("El alumno no existe.");
        if (bebe.Estado != EstadoBebe.Activo)
            throw new InvalidOperationException("El alumno está inactivo.");
        if (await db.ReservasVacantesCiclo.AnyAsync(x => x.BebeId == bebe.Id && x.CicloLectivoId == ciclo.Id, ct))
            throw new InvalidOperationException("Ya existe una reserva para este alumno y ciclo.");

        var idsFijos = await db.InscripcionesTurnos.AsNoTracking()
            .Where(x => x.TurnoId == turno.Id && x.Inscripcion.Estado == EstadoInscripcion.Activa
                && x.Inscripcion.FechaInicio <= ciclo.FechaInicio
                && (!x.Inscripcion.FechaFin.HasValue || x.Inscripcion.FechaFin >= ciclo.FechaInicio))
            .Select(x => x.Inscripcion.BebeId).Distinct().ToListAsync(ct);
        var idsReservados = await db.ReservasVacantesCiclo.AsNoTracking()
            .Where(x => x.TurnoId == turno.Id && x.Estado != EstadoReservaVacanteCiclo.Cancelada)
            .Select(x => x.BebeId).ToListAsync(ct);
        if (idsFijos.Concat(idsReservados).Distinct().Count() >= turno.Cupo)
            throw new InvalidOperationException("El turno elegido ya no tiene cupos disponibles.");

        var responsableId = bebe.Responsables.OrderByDescending(x => x.EsContactoPrincipal)
            .Select(x => (Guid?)x.ResponsableId).FirstOrDefault();
        var detalle = $"Reserva de vacante · Ciclo {ciclo.Anio} · {turno.Nombre}";
        var cobro = await cobrador.RegistrarAsync(db,
            new NuevoCobro(bebe, responsableId, ImporteReserva, datos.MedioPago,
                datos.CuentaBancariaId, null,
                $"Reserva vacante ciclo {ciclo.Anio} — anticipo para primera cuota",
                Array.Empty<Cuota>(), detalle, datos.UsuarioId, EsReservaVacante: true), ct);
        var reserva = new ReservaVacanteCiclo {
            Id = Guid.NewGuid(), BebeId = bebe.Id, CicloLectivoId = ciclo.Id,
            TurnoId = turno.Id, PagoId = cobro.Pago.Id, Importe = ImporteReserva,
            Estado = EstadoReservaVacanteCiclo.Confirmada, FechaReservaUtc = DateTimeOffset.UtcNow,
            CreadoPorUsuarioId = datos.UsuarioId
        };
        db.ReservasVacantesCiclo.Add(reserva);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return reserva.Id;
    }

    public async Task<IReadOnlyList<ReservaVacanteItemDto>> ListarAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var reservas = await db.ReservasVacantesCiclo.AsNoTracking().Include(x => x.Bebe)
            .Include(x => x.CicloLectivo).Include(x => x.Turno)
            .OrderByDescending(x => x.CicloLectivo.Anio).ThenBy(x => x.Bebe.Apellido)
            .ToListAsync(ct);
        return reservas.Select(x => new ReservaVacanteItemDto(x.Id, x.BebeId,
            $"{x.Bebe.Nombre} {x.Bebe.Apellido}".Trim(), x.CicloLectivo.Anio,
            x.TurnoId, x.Turno.Nombre, x.Importe, x.Estado, x.FechaReservaUtc, x.PagoId,
            x.InscripcionId, x.FechaFormalizacionUtc)).ToList();
    }


    /// <summary>
    /// Vista previa de solamente lecturas. La validación se repite dentro de
    /// la transacción del COMMIT para evitar asociar reservas obsoletas.
    /// </summary>
    public async Task<IReadOnlyList<ReservaFormalizacionItemDto>> PrevisualizarFormalizacionAsync(
        Guid cicloLectivoId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var datos = await PrepararAsync(db, cicloLectivoId, ct);
        return datos.Evaluaciones.Select(x => new ReservaFormalizacionItemDto(
            x.Reserva.Id, Nombre(x.Reserva.Bebe),
            x.Reserva.Turno.Nombre, x.Reserva.Importe,
            x.Reserva.InscripcionId.HasValue, x.Error is null && !x.Reserva.InscripcionId.HasValue,
            x.Reserva.InscripcionId.HasValue ? "Inscripción ya asociada" : x.Error ?? "Lista para asociar"
        )).ToList();
    }

    public async Task<FormalizacionCicloResultadoDto> FormalizarInscripcionesAsync(
        Guid cicloLectivoId, Guid usuarioId, CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty)
            throw new InvalidOperationException("No se identificó al usuario que formaliza inscripciones.");

        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var datos = await PrepararAsync(db, cicloLectivoId, ct);
        var errores = new List<string>();
        var asociadas = 0;
        var previas = 0;
        var plan = datos.Plan;
        var hoyUtc = DateTimeOffset.UtcNow;

        foreach (var item in datos.Evaluaciones)
        {
            var reserva = item.Reserva;
            if (reserva.InscripcionId.HasValue)
            {
                previas++;
                continue;
            }
            if (item.Error is not null)
            {
                errores.Add($"{Nombre(reserva.Bebe)}: {item.Error}");
                continue;
            }
            if (plan is null)
            {
                errores.Add($"{Nombre(reserva.Bebe)}: no existe un plan activo de una vez por semana.");
                continue;
            }

            var inicio = datos.Ciclo.FechaInicio;
            var inicioAnterior = inicio.AddDays(-1);
            var anteriores = datos.Inscripciones
                .Where(x => x.BebeId == reserva.BebeId
                    && x.Estado == EstadoInscripcion.Activa
                    && x.FechaInicio < inicio
                    && (!x.FechaFin.HasValue || x.FechaFin.Value >= inicio))
                .ToList();

            // Cerrar el período viejo sin alterar cuotas ni asistencias ya registradas.
            foreach (var anterior in anteriores)
            {
                anterior.FechaFin = inicioAnterior;
                anterior.ModificadoUtc = hoyUtc;
                anterior.ModificadoPorUsuarioId = usuarioId;
            }

            var inscripcion = new Inscripcion
            {
                Id = Guid.NewGuid(),
                BebeId = reserva.BebeId,
                PlanId = plan.Id,
                FechaInicio = inicio,
                FechaFin = datos.Ciclo.FechaFin,
                Estado = EstadoInscripcion.Activa,
                MedioPagoPreferido = anteriores.OrderByDescending(x => x.FechaInicio)
                    .FirstOrDefault()?.MedioPagoPreferido ?? reserva.Pago.MedioPago,
                Observaciones = $"Ciclo {datos.Ciclo.Anio}. Asociada desde reserva de vacante {reserva.Id}.",
                CreadoPorUsuarioId = usuarioId
            };
            inscripcion.Turnos.Add(new InscripcionTurno
            {
                InscripcionId = inscripcion.Id,
                TurnoId = reserva.TurnoId,
                EsPrincipal = true
            });
            db.Inscripciones.Add(inscripcion);
            reserva.InscripcionId = inscripcion.Id;
            reserva.FechaFormalizacionUtc = hoyUtc;
            reserva.ModificadoUtc = hoyUtc;
            reserva.ModificadoPorUsuarioId = usuarioId;
            asociadas++;
        }

        if (asociadas > 0) await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new FormalizacionCicloResultadoDto(asociadas, previas, errores.Count, errores);
    }

    /// <summary>
    /// Antes del inicio del ciclo un cambio de horario no afecta clases históricas:
    /// se mueve la reserva y, si ya existe, su inscripción futura.
    /// </summary>
    public async Task CambiarTurnoAsync(Guid reservaId, Guid turnoDestinoId, Guid usuarioId,
        CancellationToken ct = default)
    {
        if (usuarioId == Guid.Empty) throw new InvalidOperationException("No se identificó el usuario.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var reserva = await db.ReservasVacantesCiclo
            .Include(x => x.CicloLectivo)
            .Include(x => x.Inscripcion).ThenInclude(x => x!.Turnos)
            .FirstOrDefaultAsync(x => x.Id == reservaId, ct)
            ?? throw new InvalidOperationException("La reserva no existe.");

        if (reserva.Estado == EstadoReservaVacanteCiclo.Cancelada)
            throw new InvalidOperationException("No se puede mover una reserva cancelada.");
        if (BebesNadadores.Domain.Common.FechaArgentina.Hoy >= reserva.CicloLectivo.FechaInicio)
            throw new InvalidOperationException(
                "Este ciclo ya comenzó. El cambio de turno requiere registrar una fecha de aplicación para conservar la asistencia histórica.");
        if (reserva.TurnoId == turnoDestinoId) return;

        var destino = await db.Turnos.FirstOrDefaultAsync(x => x.Id == turnoDestinoId, ct)
            ?? throw new InvalidOperationException("El turno elegido no existe.");
        if (destino.CicloLectivoId != reserva.CicloLectivoId || destino.Estado != EstadoTurno.Activo
            || destino.VigenteDesde > reserva.CicloLectivo.FechaInicio
            || (destino.VigenteHasta.HasValue && destino.VigenteHasta.Value < reserva.CicloLectivo.FechaInicio))
            throw new InvalidOperationException("El turno no está vigente al inicio del mismo ciclo lectivo.");

        var cupo = await CupoDisponibleAsync(db, destino, reserva.CicloLectivo.FechaInicio,
            reserva.BebeId, ct);
        if (!cupo) throw new InvalidOperationException("El nuevo turno no tiene vacantes disponibles.");

        if (reserva.InscripcionId.HasValue)
        {
            var inscripcion = reserva.Inscripcion
                ?? throw new InvalidOperationException("La inscripción vinculada a la reserva no existe.");
            var relaciones = inscripcion.Turnos.ToList();
            if (relaciones.Count != 1 || relaciones[0].TurnoId != reserva.TurnoId)
                throw new InvalidOperationException("La inscripción tiene otra configuración de turnos; revisala manualmente.");
            db.InscripcionesTurnos.Remove(relaciones[0]);
            db.InscripcionesTurnos.Add(new InscripcionTurno
            {
                InscripcionId = inscripcion.Id,
                TurnoId = destino.Id,
                EsPrincipal = true
            });
        }
        reserva.TurnoId = destino.Id;
        reserva.ModificadoUtc = DateTimeOffset.UtcNow;
        reserva.ModificadoPorUsuarioId = usuarioId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    private sealed record Evaluacion(ReservaVacanteCiclo Reserva, string? Error);

    private sealed record DatosFormalizacion(
        CicloLectivo Ciclo, Plan? Plan,
        List<Inscripcion> Inscripciones, List<Evaluacion> Evaluaciones);

    private static string Nombre(Bebe bebe) => $"{bebe.Nombre} {bebe.Apellido}".Trim();

    private static async Task<bool> CupoDisponibleAsync(
        AppDbContext db, Turno turno, DateOnly fecha, Guid bebeExcluir,
        CancellationToken ct)
    {
        var regulares = await db.InscripcionesTurnos.AsNoTracking()
            .Where(x => x.TurnoId == turno.Id
                && x.Inscripcion.Estado == EstadoInscripcion.Activa
                && x.Inscripcion.FechaInicio <= fecha
                && (!x.Inscripcion.FechaFin.HasValue || x.Inscripcion.FechaFin.Value >= fecha))
            .Select(x => x.Inscripcion.BebeId).ToListAsync(ct);
        var reservados = await db.ReservasVacantesCiclo.AsNoTracking()
            .Where(x => x.TurnoId == turno.Id && x.Estado != EstadoReservaVacanteCiclo.Cancelada)
            .Select(x => x.BebeId).ToListAsync(ct);
        return regulares.Concat(reservados).Where(x => x != bebeExcluir).Distinct().Count() < turno.Cupo;
    }

    private static async Task<DatosFormalizacion> PrepararAsync(
        AppDbContext db, Guid cicloId, CancellationToken ct)
    {
        var ciclo = await db.CiclosLectivos.FirstOrDefaultAsync(x => x.Id == cicloId, ct)
            ?? throw new InvalidOperationException("El ciclo lectivo seleccionado no existe.");
        var hoy = BebesNadadores.Domain.Common.FechaArgentina.Hoy;
        if (ciclo.Estado == EstadoCicloLectivo.Cerrado || ciclo.FechaFin < hoy)
            throw new InvalidOperationException("No se pueden asociar inscripciones a un ciclo cerrado o finalizado.");

        var reservas = await db.ReservasVacantesCiclo
            .Include(x => x.Bebe).Include(x => x.Turno).Include(x => x.Pago)
            .Where(x => x.CicloLectivoId == cicloId)
            .OrderBy(x => x.Bebe.Apellido).ThenBy(x => x.Bebe.Nombre)
            .ToListAsync(ct);

        var planes = await db.Planes.AsNoTracking()
            .Where(x => x.Activo && x.VecesPorSemana == 1).ToListAsync(ct);
        var plan = planes.Count == 1 ? planes[0] : null;
        var idsBebe = reservas.Select(x => x.BebeId).Distinct().ToList();
        var inscripciones = await db.Inscripciones
            .Include(x => x.Turnos).ThenInclude(x => x.Turno)
            .Where(x => idsBebe.Contains(x.BebeId) && x.Estado == EstadoInscripcion.Activa
                && x.FechaInicio <= ciclo.FechaFin
                && (!x.FechaFin.HasValue || x.FechaFin.Value >= ciclo.FechaInicio))
            .ToListAsync(ct);
        // No truncar inscripciones anteriores con cuotas ya generadas para el nuevo período.
        var inscripcionesAnterioresIds = inscripciones
            .Where(x => x.FechaInicio < ciclo.FechaInicio).Select(x => x.Id).ToList();
        var primerPeriodo = new DateOnly(ciclo.FechaInicio.Year, ciclo.FechaInicio.Month, 1);
        var cuotasPrevias = await db.Cuotas.AsNoTracking()
            .Where(x => inscripcionesAnterioresIds.Contains(x.InscripcionId)
                && x.Periodo >= primerPeriodo && x.Estado != EstadoCuota.Anulada)
            .Select(x => x.InscripcionId).Distinct().ToListAsync(ct);
        var cuotasPreviasIds = cuotasPrevias.ToHashSet();
        var turnoIds = reservas.Select(x => x.TurnoId).Distinct().ToList();
        var regularesTurno = await db.InscripcionesTurnos.AsNoTracking()
            .Where(x => turnoIds.Contains(x.TurnoId)
                && x.Inscripcion.Estado == EstadoInscripcion.Activa
                && x.Inscripcion.FechaInicio <= ciclo.FechaInicio
                && (!x.Inscripcion.FechaFin.HasValue || x.Inscripcion.FechaFin.Value >= ciclo.FechaInicio))
            .Select(x => new { x.TurnoId, x.Inscripcion.BebeId })
            .ToListAsync(ct);
        var todasReservas = await db.ReservasVacantesCiclo.AsNoTracking()
            .Where(x => turnoIds.Contains(x.TurnoId) && x.Estado != EstadoReservaVacanteCiclo.Cancelada)
            .Select(x => new { x.TurnoId, x.BebeId })
            .ToListAsync(ct);

        var evaluaciones = new List<Evaluacion>();
        foreach (var reserva in reservas)
        {
            string? error = null;
            if (reserva.InscripcionId.HasValue)
            {
                evaluaciones.Add(new Evaluacion(reserva, null));
                continue;
            }
            if (ciclo.FechaInicio.Day != 1)
                error = "El ciclo comienza a mitad de mes: revisar la primera cuota antes de asociar";
            else if (ciclo.Estado != EstadoCicloLectivo.Publicado)
                error = "Primero publicá la grilla del ciclo lectivo";
            else if (reserva.Estado == EstadoReservaVacanteCiclo.Cancelada)
                error = "Reserva cancelada";
            else if (reserva.Bebe.Estado != EstadoBebe.Activo)
                error = "Alumno inactivo";
            else if (reserva.Pago.AnuladoUtc.HasValue)
                error = "El pago de reserva está anulado";
            else if (reserva.Turno.CicloLectivoId != cicloId
                || reserva.Turno.Estado != EstadoTurno.Activo
                || reserva.Turno.VigenteDesde > ciclo.FechaInicio
                || (reserva.Turno.VigenteHasta.HasValue && reserva.Turno.VigenteHasta.Value < ciclo.FechaInicio))
                error = "El turno reservado no está vigente al inicio del ciclo";
            else if (planes.Count != 1)
                error = "Debe existir exactamente un plan ACTIVO de una vez por semana";
            else if (inscripciones.Any(x => x.BebeId == reserva.BebeId
                && x.FechaInicio < ciclo.FechaInicio && cuotasPreviasIds.Contains(x.Id)))
                error = "Ya hay cuotas futuras en la inscripción anterior; revisar antes de asociar";
            else if (inscripciones.Any(x => x.BebeId == reserva.BebeId
                && (x.FechaInicio >= ciclo.FechaInicio
                    || x.Turnos.Any(t => t.Turno.CicloLectivoId == cicloId))))
                error = "Ya existe una inscripción para este ciclo; no se creó otra";
            else if (regularesTurno.Where(x => x.TurnoId == reserva.TurnoId)
                .Select(x => x.BebeId)
                .Concat(todasReservas.Where(x => x.TurnoId == reserva.TurnoId)
                    .Select(x => x.BebeId)).Distinct().Count() > reserva.Turno.Cupo)
                error = "El turno tiene más ocupantes que su capacidad";

            evaluaciones.Add(new Evaluacion(reserva, error));
        }
        return new DatosFormalizacion(ciclo, plan, inscripciones, evaluaciones);
    }
}
