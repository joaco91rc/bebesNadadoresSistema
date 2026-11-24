using BebesNadadores.Infrastructure.Services.Turnera;
using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bebes;

public sealed class BebeLegajoService(IDbContextFactory<AppDbContext> factory) : IBebeLegajoService
{
    public async Task<BebeLegajoDto?> ObtenerAsync(Guid bebeId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var hoy = BebesNadadores.Domain.Common.FechaArgentina.Hoy;

        // =====================================================
        // BEBÉ
        // =====================================================

        var bebe = await db
            .Bebes.AsNoTracking()
            .Include(x => x.NivelActual)
            .Include(x => x.Responsables)
                .ThenInclude(x => x.Responsable)
            .FirstOrDefaultAsync(x => x.Id == bebeId, ct);

        if (bebe is null)
            return null;

        // =====================================================
        // RESPONSABLES
        // =====================================================

        var responsables = bebe
            .Responsables.OrderByDescending(x => x.EsContactoPrincipal)
            .ThenBy(x => x.Responsable.Apellido)
            .ThenBy(x => x.Responsable.Nombre)
            .Select(x => new ResponsableLegajoDto(
                x.Responsable.Id,
                $"{x.Responsable.Nombre} {x.Responsable.Apellido}".Trim(),
                x.Responsable.Dni,
                x.Responsable.Telefono,
                x.Responsable.WhatsApp,
                x.Responsable.Email,
                x.Responsable.Domicilio,
                x.Relacion,
                x.EsContactoPrincipal,
                x.AutorizaComunicaciones
            ))
            .ToList();

        var responsablePrincipal =
            responsables.FirstOrDefault(x => x.EsContactoPrincipal)
            ?? responsables.FirstOrDefault();

        // =====================================================
        // INSCRIPCIONES
        // =====================================================

        var inscripciones = await db
            .Inscripciones.AsNoTracking()
            .Include(x => x.Plan)
                .ThenInclude(x => x.Tarifas)
            .Include(x => x.Turnos)
                .ThenInclude(x => x.Turno)
                    .ThenInclude(x => x.Nivel)
            .Include(x => x.Turnos)
                .ThenInclude(x => x.Turno)
                    .ThenInclude(x => x.Profesores)
                        .ThenInclude(x => x.Profesor)
            .Where(x => x.BebeId == bebeId)
            .OrderByDescending(x => x.FechaInicio)
            .ToListAsync(ct);

        var inscripcion = inscripciones.FirstOrDefault(x =>
            x.Estado == EstadoInscripcion.Activa
            && (!x.FechaFin.HasValue || x.FechaFin.Value >= hoy)
        );

        var inscripcionDto = inscripcion is null ? null : MapearInscripcion(inscripcion, hoy);
        // La inscripcion preliminar existe aunque todavia no tenga turnos.

        // =====================================================
        // PRÓXIMA CLASE
        // =====================================================

        var proximaClase = await ObtenerProximaClaseRealAsync(db, bebeId, inscripciones, hoy, ct);
        var inscripcionId = inscripcion?.Id ?? Guid.Empty;
        var tieneClaseRealizada = await db.Asistencias.AsNoTracking().AnyAsync(x =>
            x.InscripcionId == inscripcionId && x.BebeId == bebeId && x.Estado == EstadoAsistencia.Presente, ct);
        var primeraRealizada = await db.ReservasSesionesClase.AsNoTracking()
            .Where(x => x.InscripcionId == inscripcionId && x.BebeId == bebeId && x.Tipo == TipoReservaSesion.InscripcionInicial
                && db.Asistencias.Any(a => a.InscripcionId == inscripcionId && a.BebeId == bebeId
                    && a.SesionClaseId == x.SesionClaseId && a.Estado == EstadoAsistencia.Presente))
            .OrderBy(x => x.SesionClase.Fecha).ThenBy(x => x.SesionClase.HoraInicio)
            .Select(x => (DateOnly?)x.SesionClase.Fecha).FirstOrDefaultAsync(ct);
        var pendientePrimera = await PrimeraClasePendiente.Consultar(db)
            .Where(x => x.InscripcionId == inscripcionId && x.BebeId == bebeId)
            .OrderByDescending(x => x.SesionClase.Fecha).ThenByDescending(x => x.SesionClase.HoraInicio)
            .Select(x => new PrimeraClasePendienteDto(x.SesionClaseId, x.SesionClase.Fecha, x.SesionClase.HoraInicio, x.SesionClase.Nivel.Nombre))
            .FirstOrDefaultAsync(ct);
        var primera = await db.ReservasSesionesClase.AsNoTracking()
            .Where(x => x.InscripcionId == inscripcionId && x.BebeId == bebeId && x.Bebe.Estado == EstadoBebe.Activo
                && x.Tipo == TipoReservaSesion.InscripcionInicial && x.Estado == EstadoReservaSesion.Activa
                && x.SesionClase.Estado == EstadoSesionClase.Programada
                && !db.Asistencias.Any(a => a.InscripcionId == inscripcionId && a.Estado == EstadoAsistencia.Presente)
                && !db.Asistencias.Any(a => a.BebeId == bebeId && a.SesionClaseId == x.SesionClaseId && a.Estado != EstadoAsistencia.Pendiente))
            .OrderBy(x => x.SesionClase.Fecha).ThenBy(x => x.SesionClase.HoraInicio)
            .Select(x => new { x.SesionClaseId, x.SesionClase.TurnoId, x.SesionClase.Fecha, x.SesionClase.HoraInicio, x.SesionClase.HoraFin, Nivel = x.SesionClase.Nivel.Nombre, x.SesionClase.Sector })
            .FirstOrDefaultAsync(ct);
        var primeraDto = primera is null ? null : new ProximaClaseLegajoDto(primera.TurnoId, primera.Fecha,
            ConvertirDiaSemana(primera.Fecha.DayOfWeek), primera.HoraInicio, primera.HoraFin, primera.Nivel, primera.Sector, Array.Empty<string>());

        // =====================================================
        // APTOS MÉDICOS
        // =====================================================

        var aptos = await db
            .AptosMedicos.AsNoTracking()
            .Where(x => x.BebeId == bebeId)
            .OrderByDescending(x => x.FechaRevision)
            .ToListAsync(ct);

        var aptosDto = aptos
            .Select(x => new AptoMedicoLegajoDto(
                x.Id,
                x.FechaRevision,
                x.VigenciaHasta,
                x.Resultado,
                x.Observaciones,
                x.MedicoUsuarioId,
                x.DocumentoStorageKey,
                x.DocumentoNombreOriginal,
                x.EstaVigente(hoy)
            ))
            .ToList();

        var aptoActual = aptosDto.FirstOrDefault();

        // =====================================================
        // ASISTENCIA - ÚLTIMOS 90 DÍAS
        // =====================================================

        var desde = hoy.AddDays(-90);

        var asistencias = await db
            .Asistencias.AsNoTracking()
            .Include(x => x.SesionClase)
                .ThenInclude(x => x.Nivel)
            .Where(x => x.BebeId == bebeId && x.SesionClase.Fecha >= desde)
            .OrderByDescending(x => x.SesionClase.Fecha)
            .ThenByDescending(x => x.SesionClase.HoraInicio)
            .ToListAsync(ct);

        var asistenciasDto = asistencias
            .Select(x => new AsistenciaLegajoDto(
                x.Id,
                x.SesionClaseId,
                x.SesionClase.Fecha,
                x.SesionClase.HoraInicio,
                x.SesionClase.Nivel.Nombre,
                x.Estado,
                x.EsRecupero,
                x.Observaciones
            ))
            .ToList();

        var marcadas = asistencias.Count(x => x.Estado != EstadoAsistencia.Pendiente);

        var presentes = asistencias.Count(x => x.Estado == EstadoAsistencia.Presente);

        var ausentesConAviso = asistencias.Count(x => x.Estado == EstadoAsistencia.AusenteConAviso);

        var ausentes = asistencias.Count(x => x.Estado == EstadoAsistencia.Ausente);

        var pendientes = asistencias.Count(x => x.Estado == EstadoAsistencia.Pendiente);

        var asistenciasRecupero = asistencias.Count(x => x.EsRecupero);

        var porcentajeAsistencia = marcadas == 0 ? 0 : (int)Math.Round(presentes * 100d / marcadas);

        var asistenciaResumen = new AsistenciaResumenLegajoDto(
            asistencias.Count,
            presentes,
            ausentesConAviso,
            ausentes,
            pendientes,
            asistenciasRecupero,
            porcentajeAsistencia
        );

        // =====================================================
        // RECUPEROS
        // =====================================================

        var recuperaciones = await db
            .Recuperaciones.AsNoTracking()
            .Include(x => x.SesionOrigen)
                .ThenInclude(x => x.Nivel)
            .Include(x => x.NivelOrigen)
            .Include(x => x.SesionUso)
                .ThenInclude(x => x!.Nivel)
            .Where(x => x.BebeId == bebeId)
            .OrderByDescending(x => x.FechaGeneracion)
            .ToListAsync(ct);

        var recuperacionesDto = recuperaciones
            .Select(x =>
            {
                var vencida = x.EstaVencida(hoy);

                var estadoEfectivo = vencida ? EstadoRecuperacion.Vencida : x.Estado;

                return new RecuperacionLegajoDto(
                    x.Id,
                    x.Tipo,
                    estadoEfectivo,
                    x.FechaGeneracion,
                    x.FechaVencimiento,
                    x.SinVencimiento,
                    vencida,
                    x.SesionOrigen?.Fecha ?? x.FechaGeneracion,
                    x.SesionOrigen?.Nivel.Nombre ?? x.NivelOrigen?.Nombre ?? "Sin nivel de origen",
                    x.SesionUsoId,
                    x.SesionUso?.Fecha,
                    x.SesionUso?.Nivel.Nombre
                );
            })
            .ToList();

        // =====================================================
        // PAGOS
        // =====================================================

        var pagos = await db
            .Pagos.AsNoTracking()
            .Include(x => x.Responsable)
            .Where(x => x.BebeId == bebeId && !x.AnuladoUtc.HasValue)
            .OrderByDescending(x => x.FechaUtc)
            .Take(100)
            .ToListAsync(ct);

        var pagadoMesActual = pagos
            .Where(x => x.FechaUtc.Year == hoy.Year && x.FechaUtc.Month == hoy.Month)
            .Sum(x => x.Importe);

        var pagosDto = pagos
            .Select(x => new PagoLegajoDto(
                x.Id,
                x.FechaUtc,
                x.Importe,
                x.MedioPago,
                x.ReferenciaExterna,
                x.Observaciones,
                x.Responsable is null
                    ? null
                    : $"{x.Responsable.Nombre} {x.Responsable.Apellido}".Trim()
            ))
            .ToList();

        // =====================================================
        // RESULTADO
        // =====================================================

        var edadMeses = bebe.EdadEnMeses(hoy);

        return new BebeLegajoDto(
            bebe.Id,
            bebe.Nombre,
            bebe.Apellido,
            $"{bebe.Nombre} {bebe.Apellido}".Trim(),
            bebe.FechaNacimiento,
            bebe.Sexo,
            bebe.Dni,
            bebe.PermanecioHospitalMasDe5Dias,
            bebe.DetalleHospitalizacion,
            bebe.RecibeTratamiento,
            bebe.DetalleTratamiento,
            bebe.FechaAlta,
            bebe.Estado,
            bebe.FechaBaja,
            bebe.MotivoBaja,
            bebe.NivelActualId,
            bebe.NivelActual?.Nombre,
            edadMeses,
            edadMeses / 12,
            bebe.AlcanzoEdadMaxima(hoy),
            responsablePrincipal,
            responsables,
            inscripcionDto,
            proximaClase,
            aptoActual,
            aptosDto,
            asistenciaResumen,
            asistenciasDto,
            recuperacionesDto,
            pagadoMesActual,
            pagosDto
        ) { PrimeraClaseReservada = primeraDto, PrimeraClaseRealizada = primeraRealizada,
            TieneClaseRealizada = tieneClaseRealizada, PrimeraClaseReservadaSesionId = primera?.SesionClaseId, PrimeraClasePorReprogramar = pendientePrimera };
    }

    // =========================================================
    // INSCRIPCIÓN
    // =========================================================

    private static InscripcionLegajoDto MapearInscripcion(Inscripcion inscripcion, DateOnly hoy)
    {
        var tarifa = inscripcion
            .Plan.Tarifas.Where(x =>
                x.VigenciaDesde <= hoy
                && (!x.VigenciaHasta.HasValue || x.VigenciaHasta.Value >= hoy)
            )
            .OrderByDescending(x => x.VigenciaDesde)
            .FirstOrDefault();

        var turnos = inscripcion
            .Turnos.Where(x =>
                x.Turno.Estado == EstadoTurno.Activo
                && x.Turno.VigenteDesde <= hoy
                && (!x.Turno.VigenteHasta.HasValue || x.Turno.VigenteHasta.Value >= hoy)
            )
            .OrderBy(x => x.Turno.DiaSemana)
            .ThenBy(x => x.Turno.HoraInicio)
            .Select(x => new TurnoLegajoDto(
                x.Turno.Id,
                x.Turno.Nombre,
                x.Turno.DiaSemana,
                x.Turno.HoraInicio,
                x.Turno.HoraFin,
                x.Turno.Nivel.Nombre,
                x.Turno.Sector,
                x.EsPrincipal,
                x.Turno.Profesores.OrderByDescending(p => p.EsPrincipal)
                    .ThenBy(p => p.Profesor.Apellido)
                    .ThenBy(p => p.Profesor.Nombre)
                    .Select(p => $"{p.Profesor.Nombre} {p.Profesor.Apellido}".Trim())
                    .ToList()
            ))
            .ToList();

        return new InscripcionLegajoDto(
            inscripcion.Id,
            inscripcion.PlanId,
            inscripcion.Plan.Nombre,
            inscripcion.Plan.VecesPorSemana,
            tarifa?.PrecioMensual,
            inscripcion.FechaInicio,
            inscripcion.FechaFin,
            inscripcion.Estado,
            inscripcion.MedioPagoPreferido,
            inscripcion.TipoDescuento,
            inscripcion.ValorDescuento,
            inscripcion.MotivoDescuento,
            inscripcion.Observaciones,
            turnos
        );
    }

    // =========================================================
    // PRÓXIMA CLASE RECURRENTE
    // =========================================================

    private static async Task<ProximaClaseLegajoDto?> ObtenerProximaClaseRealAsync(AppDbContext db, Guid bebeId, IReadOnlyList<Inscripcion> inscripciones, DateOnly hoy, CancellationToken ct)
    {
        var ahora = TimeOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)).DateTime);
        var vigentes = inscripciones.Where(x => x.Estado == EstadoInscripcion.Activa && (x.FechaFin == null || x.FechaFin >= hoy)).ToList();
        var turnos = vigentes.SelectMany(x => x.Turnos).Select(x => x.TurnoId).Distinct().ToArray();
        var primeras = await PrimeraClaseExcepcional.ObtenerFechasAsync(db, vigentes.Select(x => x.Id), hoy, ct, bloquearPendientesSinFecha: true);
        var reservas = await db.ReservasSesionesClase.AsNoTracking().Where(x => x.BebeId == bebeId && x.Estado == EstadoReservaSesion.Activa && x.SesionClase.Fecha >= hoy
            && !db.Asistencias.Any(a => a.BebeId == x.BebeId && a.InscripcionId == x.InscripcionId && a.SesionClaseId == x.SesionClaseId && a.Estado == EstadoAsistencia.AusenteConAviso)).Select(x => x.SesionClaseId).ToListAsync(ct);
        var recuperos = await db.Recuperaciones.AsNoTracking().Where(x => x.BebeId == bebeId && x.Estado == EstadoRecuperacion.Reservada && x.SesionUsoId != null && x.SesionUso!.Fecha >= hoy).Select(x => x.SesionUsoId!.Value).ToListAsync(ct);
        var especiales = reservas.Concat(recuperos).ToHashSet();
        var sesiones = await db.SesionesClase.AsNoTracking().Include(x => x.Nivel).Include(x => x.Profesores).ThenInclude(x => x.Profesor)
            .Where(x => x.Estado == EstadoSesionClase.Programada && x.Turno.Estado != EstadoTurno.Baja && x.Fecha >= hoy && (x.Fecha > hoy || x.HoraInicio >= ahora) && (turnos.Contains(x.TurnoId) || especiales.Contains(x.Id)))
            .OrderBy(x => x.Fecha).ThenBy(x => x.HoraInicio).ToListAsync(ct);
        var sesion = sesiones.FirstOrDefault(s => especiales.Contains(s.Id) || vigentes.Any(i => i.FechaInicio <= s.Fecha && (i.FechaFin == null || i.FechaFin >= s.Fecha) && i.Turnos.Any(t => t.TurnoId == s.TurnoId) && !PrimeraClaseExcepcional.TodaviaNoComenzo(primeras, i.Id, s.Fecha)));
        if (sesion is null) return null;
        return new ProximaClaseLegajoDto(sesion.TurnoId, sesion.Fecha, ConvertirDiaSemana(sesion.Fecha.DayOfWeek), sesion.HoraInicio, sesion.HoraFin, sesion.Nivel.Nombre, sesion.Sector, sesion.Profesores.OrderByDescending(p => p.EsPrincipal).Select(p => $"{p.Profesor.Nombre} {p.Profesor.Apellido}".Trim()).ToList());
    }

    private static DiaSemana ConvertirDiaSemana(DayOfWeek dia)
    {
        return dia switch
        {
            DayOfWeek.Monday => DiaSemana.Lunes,

            DayOfWeek.Tuesday => DiaSemana.Martes,

            DayOfWeek.Wednesday => DiaSemana.Miercoles,

            DayOfWeek.Thursday => DiaSemana.Jueves,

            DayOfWeek.Friday => DiaSemana.Viernes,

            DayOfWeek.Saturday => DiaSemana.Sabado,

            DayOfWeek.Sunday => DiaSemana.Domingo,

            _ => throw new ArgumentOutOfRangeException(nameof(dia)),
        };
    }
}
