using BebesNadadores.Application.CiclosLectivos.Dtos;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Application.Turnera.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Application.Configuracion;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Recuperos;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Turnera;

public sealed class TurneraService(
    IDbContextFactory<AppDbContext> factory,
    GeneradorRecuperosClasesNoDictadas generadorRecuperos)
    : ITurneraService
{
    // =========================================================
    // TURNOS RECURRENTES
    // =========================================================

    public async Task<IReadOnlyList<TurnoDto>> ListarTurnosAsync(
        bool incluirBajas = false,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db
            .Turnos.AsNoTracking()
            .Include(x => x.Nivel)
            .Include(x => x.Profesores)
                .ThenInclude(x => x.Profesor)
            .AsQueryable();

        if (!incluirBajas)
        {
            query = query.Where(x => x.Estado != EstadoTurno.Baja);
        }

        var turnos = await query.ToListAsync(ct);

        return turnos
            .OrderBy(x => (int)x.DiaSemana)
            .ThenBy(x => x.HoraInicio)
            .Select(MapearTurno)
            .ToList();
    }

    public async Task<TurnoDto?> ObtenerTurnoAsync(Guid turnoId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var turno = await db
            .Turnos.AsNoTracking()
            .Include(x => x.Nivel)
            .Include(x => x.Profesores)
                .ThenInclude(x => x.Profesor)
            .FirstOrDefaultAsync(x => x.Id == turnoId, ct);

        return turno is null ? null : MapearTurno(turno);
    }

    public async Task<Guid> CrearTurnoAsync(CrearTurnoDto dto, CancellationToken ct = default)
    {
        ValidarDatosBasicos(dto);

        await using var db = await factory.CreateDbContextAsync(ct);

        // =========================================================
        // CICLO LECTIVO
        // =========================================================

        var ciclo =
            await db
                .CiclosLectivos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == dto.CicloLectivoId, ct)
            ?? throw new InvalidOperationException("El ciclo lectivo seleccionado no existe.");

        if (ciclo.Estado == EstadoCicloLectivo.Cerrado)
        {
            throw new InvalidOperationException(
                "No se pueden agregar horarios a un ciclo lectivo cerrado."
            );
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        DateOnly vigenteDesde;
        DateOnly vigenteHasta;

        // =========================================================
        // DEFINIR VIGENCIA REAL
        // =========================================================

        if (ciclo.Estado == EstadoCicloLectivo.Borrador)
        {
            /*
             * En borrador el horario forma parte
             * de todo el ciclo lectivo.
             */
            vigenteDesde = ciclo.FechaInicio;

            vigenteHasta = ciclo.FechaFin;
        }
        else
        {
            /*
             * El ciclo ya está publicado.
             *
             * Respetamos la fecha "Aplicar desde"
             * elegida desde la configuración de grilla.
             */
            vigenteDesde = dto.VigenteDesde;

            vigenteHasta = dto.VigenteHasta ?? ciclo.FechaFin;

            /*
             * Un horario nuevo de un ciclo ya publicado
             * no puede crear clases retrospectivamente.
             */
            if (vigenteDesde < hoy)
            {
                throw new InvalidOperationException(
                    "En un ciclo ya publicado, un horario nuevo no puede comenzar en una fecha pasada."
                );
            }
        }

        // =========================================================
        // VALIDAR VIGENCIA CONTRA EL CICLO
        // =========================================================

        if (vigenteDesde < ciclo.FechaInicio)
        {
            throw new InvalidOperationException(
                $"El horario no puede comenzar antes del inicio del ciclo ({ciclo.FechaInicio:dd/MM/yyyy})."
            );
        }

        if (vigenteDesde > ciclo.FechaFin)
        {
            throw new InvalidOperationException(
                $"La fecha de inicio está fuera del ciclo lectivo."
            );
        }

        if (vigenteHasta > ciclo.FechaFin)
        {
            throw new InvalidOperationException(
                $"El horario no puede finalizar después del fin del ciclo ({ciclo.FechaFin:dd/MM/yyyy})."
            );
        }

        if (vigenteHasta < vigenteDesde)
        {
            throw new InvalidOperationException(
                "La fecha Hasta no puede ser anterior a la fecha Desde."
            );
        }

        // =========================================================
        // NIVEL
        // =========================================================

        var nivel = await ObtenerNivelAsync(db, dto.NivelId, ct);

        // =========================================================
        // PROFESORES
        // =========================================================

        ValidarProfesoresDto(dto.Profesores, nivel);

        var profesorIds = dto.Profesores.Select(x => x.ProfesorId).Distinct().ToList();

        await ValidarProfesoresExistentesAsync(db, profesorIds, ct);

        // =========================================================
        // VALIDAR SUPERPOSICIÓN
        // =========================================================

        await ValidarSuperposicionProfesoresAsync(
            db,
            turnoIdExcluir: null,
            dto.DiaSemana,
            dto.HoraInicio,
            dto.DuracionMinutos,
            vigenteDesde,
            vigenteHasta,
            profesorIds,
            ct
        );

        // =========================================================
        // CREAR TURNO
        // =========================================================

        var turno = new Turno
        {
            Id = Guid.NewGuid(),

            CicloLectivoId = ciclo.Id,

            Nombre = dto.Nombre.Trim(),

            DiaSemana = dto.DiaSemana,

            HoraInicio = dto.HoraInicio,

            DuracionMinutos = dto.DuracionMinutos,

            NivelId = dto.NivelId,

            Cupo = dto.Cupo,

            Sector = Normalizar(dto.Sector),

            VigenteDesde = vigenteDesde,

            VigenteHasta = vigenteHasta,

            Estado = EstadoTurno.Activo,
        };

        // =========================================================
        // PROFESORES DEL TURNO
        // =========================================================

        foreach (var profesor in dto.Profesores)
        {
            turno.Profesores.Add(
                new TurnoProfesor
                {
                    TurnoId = turno.Id,

                    ProfesorId = profesor.ProfesorId,

                    EsPrincipal = profesor.EsPrincipal,
                }
            );
        }

        db.Turnos.Add(turno);

        await db.SaveChangesAsync(ct);

        // =========================================================
        // CICLO YA PUBLICADO
        // =========================================================

        if (ciclo.Estado == EstadoCicloLectivo.Publicado)
        {
            /*
             * El nuevo horario ya entra en una grilla
             * que está funcionando.
             *
             * Generamos automáticamente todas sus
             * clases desde AplicarDesde hasta su fin.
             *
             * GenerarSesionesAsync evita duplicados.
             */
            await GenerarSesionesCicloAsync(ciclo.Id, vigenteDesde, vigenteHasta, ct);
        }

        return turno.Id;
    }

    public async Task ActualizarTurnoAsync(
        Guid turnoId,
        ActualizarTurnoDto dto,
        CancellationToken ct = default
    )
    {
        ValidarDatosBasicos(dto);

        await using var db = await factory.CreateDbContextAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var turno =
            await db.Turnos.Include(x => x.Profesores).FirstOrDefaultAsync(x => x.Id == turnoId, ct)
            ?? throw new InvalidOperationException("El turno indicado no existe.");

        if (dto.AplicarDesde < turno.VigenteDesde)
        {
            throw new InvalidOperationException(
                $"Los cambios no pueden aplicarse antes del {turno.VigenteDesde:dd/MM/yyyy}."
            );
        }

        if (turno.VigenteHasta.HasValue && dto.AplicarDesde > turno.VigenteHasta.Value)
        {
            throw new InvalidOperationException(
                "La fecha de aplicación está fuera de la vigencia del turno."
            );
        }

        var nivel = await ObtenerNivelAsync(db, dto.NivelId, ct);

        ValidarProfesoresDto(dto.Profesores, nivel);

        var profesorIds = dto.Profesores.Select(x => x.ProfesorId).Distinct().ToList();

        await ValidarProfesoresExistentesAsync(db, profesorIds, ct);

        if (dto.Estado == EstadoTurno.Activo)
        {
            await ValidarSuperposicionProfesoresAsync(
                db,
                turnoId,
                dto.DiaSemana,
                dto.HoraInicio,
                dto.DuracionMinutos,
                dto.AplicarDesde,
                dto.VigenteHasta,
                profesorIds,
                ct
            );
        }

        // =========================================================
        // SESIONES YA GENERADAS DESDE LA FECHA DE CAMBIO
        // =========================================================

        var sesionesAReemplazar = await db
            .SesionesClase.Where(x =>
                x.TurnoId == turnoId && x.Fecha >= dto.AplicarDesde && !x.EsModificadaManualmente
            )
            .ToListAsync(ct);

        if (sesionesAReemplazar.Count > 0)
        {
            var sesionIds = sesionesAReemplazar.Select(x => x.Id).ToList();

            var tieneAsistencias = await db.Asistencias.AnyAsync(
                x => sesionIds.Contains(x.SesionClaseId),
                ct
            );

            var tieneInvitaciones = await db.InvitacionesVacante.AnyAsync(
                x => sesionIds.Contains(x.SesionClaseId),
                ct
            );

            var tieneRecuperaciones = await db.Recuperaciones.AnyAsync(
                x =>
                    (x.SesionOrigenId.HasValue && sesionIds.Contains(x.SesionOrigenId.Value))
                    || (x.SesionUsoId.HasValue && sesionIds.Contains(x.SesionUsoId.Value)),
                ct
            );

            if (tieneAsistencias || tieneInvitaciones || tieneRecuperaciones)
            {
                throw new InvalidOperationException(
                    "No se puede aplicar el cambio desde esa fecha porque existen clases con asistencia, invitaciones o recuperos asociados. Elegí una fecha posterior."
                );
            }

            /*
             * Son sesiones futuras todavía limpias.
             * Las eliminamos y se volverán a generar
             * con la nueva versión del turno.
             */
            db.SesionesClase.RemoveRange(sesionesAReemplazar);
        }

        // =========================================================
        // SI EL CAMBIO APLICA DESDE EL COMIENZO MISMO DEL TURNO
        // PODEMOS MODIFICAR LA VERSIÓN ACTUAL.
        // =========================================================

        if (dto.AplicarDesde == turno.VigenteDesde)
        {
            turno.Nombre = dto.Nombre.Trim();

            turno.DiaSemana = dto.DiaSemana;

            turno.HoraInicio = dto.HoraInicio;

            turno.DuracionMinutos = dto.DuracionMinutos;

            turno.NivelId = dto.NivelId;

            turno.Cupo = dto.Cupo;

            turno.Sector = Normalizar(dto.Sector);

            turno.VigenteHasta = dto.VigenteHasta;

            turno.Estado = dto.Estado;

            SincronizarProfesores(db, turno, dto.Profesores);

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            // Si pertenece a un ciclo publicado,
            // reconstruimos las clases que eliminamos.
            if (turno.CicloLectivoId.HasValue)
            {
                var ciclo = await db
                    .CiclosLectivos.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == turno.CicloLectivoId.Value, ct);

                if (ciclo is not null && ciclo.Estado == EstadoCicloLectivo.Publicado)
                {
                    var hasta =
                        turno.VigenteHasta.HasValue && turno.VigenteHasta.Value < ciclo.FechaFin
                            ? turno.VigenteHasta.Value
                            : ciclo.FechaFin;

                 await GenerarSesionesCicloAsync(
    turno.CicloLectivoId.Value,
    dto.AplicarDesde,
    hasta,
    ct);
                }
            }

            return;
        }

        // =========================================================
        // VERSIONADO
        // =========================================================
        //
        // Ej:
        //
        // viejo: 01/09 -> 31/10
        // nuevo: 01/11 -> ...
        //
        // Las sesiones históricas siguen apuntando al turno viejo.
        // =========================================================

        turno.VigenteHasta = dto.AplicarDesde.AddDays(-1);

        /*
         * La versión anterior sigue Activa como registro histórico.
         * Su VigenteHasta impide que vuelva a generar clases.
         */
        turno.Estado = EstadoTurno.Activo;

        var nuevaVersion = new Turno
        {
            Id = Guid.NewGuid(),

            // MUY IMPORTANTE:
            // la nueva versión sigue perteneciendo
            // al mismo ciclo lectivo.
            CicloLectivoId = turno.CicloLectivoId,

            Nombre = dto.Nombre.Trim(),

            DiaSemana = dto.DiaSemana,

            HoraInicio = dto.HoraInicio,

            DuracionMinutos = dto.DuracionMinutos,

            NivelId = dto.NivelId,

            Cupo = dto.Cupo,

            Sector = Normalizar(dto.Sector),

            VigenteDesde = dto.AplicarDesde,

            VigenteHasta = dto.VigenteHasta,

            Estado = dto.Estado,
        };

        foreach (var profesor in dto.Profesores)
        {
            nuevaVersion.Profesores.Add(
                new TurnoProfesor
                {
                    TurnoId = nuevaVersion.Id,

                    ProfesorId = profesor.ProfesorId,

                    EsPrincipal = profesor.EsPrincipal,
                }
            );
        }

        db.Turnos.Add(nuevaVersion);

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        /*
         * Al modificar la configuración recurrente,
         * eliminamos las sesiones futuras anteriores
         * y ahora debemos volver a generarlas usando
         * la nueva versión del turno.
         */
        if (nuevaVersion.CicloLectivoId.HasValue)
        {
            var ciclo = await db
                .CiclosLectivos.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == nuevaVersion.CicloLectivoId.Value, ct);

            if (ciclo is not null && ciclo.Estado == EstadoCicloLectivo.Publicado)
            {
                var hasta =
                    nuevaVersion.VigenteHasta.HasValue
                    && nuevaVersion.VigenteHasta.Value < ciclo.FechaFin
                        ? nuevaVersion.VigenteHasta.Value
                        : ciclo.FechaFin;

              await GenerarSesionesCicloAsync(
    nuevaVersion.CicloLectivoId.Value,
    nuevaVersion.VigenteDesde,
    hasta,
    ct);
            }
        }
    }

    private static void SincronizarProfesores(
        AppDbContext db,
        Turno turno,
        IReadOnlyCollection<ProfesorTurnoInputDto> profesores
    )
    {
        var nuevos = profesores.ToDictionary(x => x.ProfesorId);

        var eliminar = turno.Profesores.Where(x => !nuevos.ContainsKey(x.ProfesorId)).ToList();

        db.TurnosProfesores.RemoveRange(eliminar);

        foreach (var actual in turno.Profesores)
        {
            if (nuevos.TryGetValue(actual.ProfesorId, out var nuevo))
            {
                actual.EsPrincipal = nuevo.EsPrincipal;
            }
        }

        var actualesIds = turno.Profesores.Select(x => x.ProfesorId).ToHashSet();

        foreach (var nuevo in profesores)
        {
            if (actualesIds.Contains(nuevo.ProfesorId))
            {
                continue;
            }

            turno.Profesores.Add(
                new TurnoProfesor
                {
                    TurnoId = turno.Id,

                    ProfesorId = nuevo.ProfesorId,

                    EsPrincipal = nuevo.EsPrincipal,
                }
            );
        }
    }

    private static void ValidarDatosBasicos(ActualizarTurnoDto dto)
    {
        ValidarDatosBasicos(
            dto.Nombre,
            dto.HoraInicio,
            dto.DuracionMinutos,
            dto.Cupo,
            dto.Sector,
            dto.AplicarDesde,
            dto.VigenteHasta
        );
    }

    private static void ValidarDatosBasicos(
        string nombre,
        TimeOnly horaInicio,
        int duracionMinutos,
        int cupo,
        string? sector,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta
    )
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new InvalidOperationException("Debe ingresar el nombre del turno.");
        }

        if (nombre.Trim().Length > 150)
        {
            throw new InvalidOperationException(
                "El nombre del turno no puede superar los 150 caracteres."
            );
        }

        if (duracionMinutos <= 0 || duracionMinutos > 240)
        {
            throw new InvalidOperationException("La duración del turno no es válida.");
        }

        var horaFin = horaInicio.AddMinutes(duracionMinutos);

        if (horaFin <= horaInicio)
        {
            throw new InvalidOperationException("El turno no puede finalizar al día siguiente.");
        }

        if (cupo <= 0)
        {
            throw new InvalidOperationException("El cupo debe ser mayor a cero.");
        }

        if (vigenteHasta.HasValue && vigenteHasta.Value < vigenteDesde)
        {
            throw new InvalidOperationException(
                "La fecha Hasta no puede ser anterior a la fecha Desde."
            );
        }

        if (sector?.Length > 150)
        {
            throw new InvalidOperationException("El sector no puede superar los 150 caracteres.");
        }
    }

    public async Task CambiarEstadoTurnoAsync(
        Guid turnoId,
        EstadoTurno estado,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var turno =
            await db.Turnos.Include(x => x.Profesores).FirstOrDefaultAsync(x => x.Id == turnoId, ct)
            ?? throw new InvalidOperationException("El turno indicado no existe.");

        if (estado == EstadoTurno.Activo)
        {
            var profesorIds = turno.Profesores.Select(x => x.ProfesorId).ToList();

            await ValidarSuperposicionProfesoresAsync(
                db,
                turno.Id,
                turno.DiaSemana,
                turno.HoraInicio,
                turno.DuracionMinutos,
                turno.VigenteDesde,
                turno.VigenteHasta,
                profesorIds,
                ct
            );
        }

        turno.Estado = estado;

        await db.SaveChangesAsync(ct);
    }

    // =========================================================
    // QUITAR TURNO DE LA GRILLA SEMANAL
    // =========================================================

    public async Task QuitarTurnoDeGrillaAsync(
        Guid turnoId,
        DateOnly aplicarDesde,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // =========================================================
        // TURNO
        // =========================================================

        var turno =
            await db.Turnos.Include(x => x.Profesores).FirstOrDefaultAsync(x => x.Id == turnoId, ct)
            ?? throw new InvalidOperationException("El horario indicado no existe.");

        if (turno.Estado == EstadoTurno.Baja)
        {
            throw new InvalidOperationException("El horario ya se encuentra dado de baja.");
        }

        if (aplicarDesde < turno.VigenteDesde)
        {
            throw new InvalidOperationException(
                $"El horario comienza a tener vigencia el " + $"{turno.VigenteDesde:dd/MM/yyyy}."
            );
        }

        if (turno.VigenteHasta.HasValue && aplicarDesde > turno.VigenteHasta.Value)
        {
            throw new InvalidOperationException(
                "La fecha indicada está fuera de la vigencia del horario."
            );
        }

        // =========================================================
        // INSCRIPCIONES QUE TODAVÍA UTILIZAN EL TURNO
        // =========================================================
        //
        // No podemos quitar el horario si todavía existen bebés
        // que deberían asistir a partir de la fecha elegida.
        // Primero hay que reubicarlos.
        // =========================================================

        var inscripcionesActivas = await db
            .InscripcionesTurnos.AsNoTracking()
            .Where(x =>
                x.TurnoId == turnoId
                && x.Inscripcion.Estado != EstadoInscripcion.Cancelada
                && x.Inscripcion.Estado != EstadoInscripcion.Pausada
                && x.Inscripcion.FechaInicio <= aplicarDesde
                && (
                    !x.Inscripcion.FechaFin.HasValue || x.Inscripcion.FechaFin.Value >= aplicarDesde
                )
            )
            .Select(x => x.Inscripcion.BebeId)
            .Distinct()
            .CountAsync(ct);

        if (inscripcionesActivas > 0)
        {
            throw new InvalidOperationException(
                $"Este horario todavía tiene "
                    + $"{inscripcionesActivas} "
                    + $"{(inscripcionesActivas == 1 ? "bebé asignado" : "bebés asignados")}. "
                    + "Reubicá sus inscripciones antes de quitarlo de la grilla."
            );
        }

        // =========================================================
        // LISTA DE ESPERA ACTIVA PARA ESTE TURNO
        // =========================================================

        var solicitudesEsperaActivas = await db
            .ListaEspera.AsNoTracking()
            .AnyAsync(
                x =>
                    x.TurnoId == turnoId
                    && (
                        x.Estado == EstadoListaEspera.Activa
                        || x.Estado == EstadoListaEspera.Notificada
                    ),
                ct
            );

        if (solicitudesEsperaActivas)
        {
            throw new InvalidOperationException(
                "Este horario posee solicitudes activas en lista de espera. "
                    + "Revisalas antes de quitarlo de la grilla."
            );
        }

        // =========================================================
        // SESIONES DESDE LA FECHA DE BAJA
        // =========================================================

        var sesionesDesdeFecha = await db
            .SesionesClase.Where(x => x.TurnoId == turnoId && x.Fecha >= aplicarDesde)
            .ToListAsync(ct);

        if (sesionesDesdeFecha.Count > 0)
        {
            var sesionIds = sesionesDesdeFecha.Select(x => x.Id).ToList();

            // -----------------------------------------------------
            // ASISTENCIAS
            // -----------------------------------------------------

            var tieneAsistencias = await db
                .Asistencias.AsNoTracking()
                .AnyAsync(x => sesionIds.Contains(x.SesionClaseId), ct);

            // -----------------------------------------------------
            // INVITACIONES
            // -----------------------------------------------------

            var tieneInvitaciones = await db
                .InvitacionesVacante.AsNoTracking()
                .AnyAsync(x => sesionIds.Contains(x.SesionClaseId), ct);

            // -----------------------------------------------------
            // RECUPEROS
            // -----------------------------------------------------

            var tieneRecuperaciones = await db
                .Recuperaciones.AsNoTracking()
                .AnyAsync(
                    x =>
                        (x.SesionOrigenId.HasValue && sesionIds.Contains(x.SesionOrigenId.Value))
                        || (x.SesionUsoId.HasValue && sesionIds.Contains(x.SesionUsoId.Value)),
                    ct
                );

            if (tieneAsistencias || tieneInvitaciones || tieneRecuperaciones)
            {
                throw new InvalidOperationException(
                    "No se puede quitar el horario desde esa fecha porque "
                        + "existen clases con asistencia, invitaciones o recuperos asociados. "
                        + "Elegí una fecha posterior."
                );
            }

            /*
             * Las sesiones son futuras y todavía están limpias.
             * Las eliminamos porque el turno dejará de generarlas.
             */
            db.SesionesClase.RemoveRange(sesionesDesdeFecha);
        }

        // =========================================================
        // ¿TIENE HISTORIA?
        // =========================================================

        var tieneSesionesAnteriores = await db
            .SesionesClase.AsNoTracking()
            .AnyAsync(x => x.TurnoId == turnoId && x.Fecha < aplicarDesde, ct);

        var tieneInscripcionesHistoricas = await db
            .InscripcionesTurnos.AsNoTracking()
            .AnyAsync(x => x.TurnoId == turnoId, ct);

        var tieneListaEsperaHistorica = await db
            .ListaEspera.AsNoTracking()
            .AnyAsync(x => x.TurnoId == turnoId, ct);

        var tieneHistoria =
            tieneSesionesAnteriores || tieneInscripcionesHistoricas || tieneListaEsperaHistorica;

        // =========================================================
        // TURNO NUEVO SIN USO → BORRADO FÍSICO
        // =========================================================
        //
        // Esto sirve especialmente durante la configuración inicial
        // de la grilla.
        //
        // Si la administrativa creó mal un horario y todavía no
        // existen bebés ni actividad, realmente lo eliminamos.
        // =========================================================

        if (aplicarDesde <= hoy && !tieneHistoria)
        {
            if (turno.Profesores.Count > 0)
            {
                db.TurnosProfesores.RemoveRange(turno.Profesores);
            }

            db.Turnos.Remove(turno);

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);

            return;
        }

        // =========================================================
        // TURNO CON HISTORIA → TERMINAR VIGENCIA
        // =========================================================
        //
        // NO eliminamos el turno porque las clases anteriores deben
        // seguir pudiendo consultarse.
        // =========================================================

        if (aplicarDesde <= turno.VigenteDesde)
        {
            throw new InvalidOperationException(
                "Este horario ya posee información histórica y no puede eliminarse "
                    + "desde el comienzo de su vigencia. Elegí una fecha posterior."
            );
        }

        turno.VigenteHasta = aplicarDesde.AddDays(-1);

        /*
         * IMPORTANTE:
         *
         * permanece ACTIVO históricamente.
         *
         * La vigencia es la que determina hasta cuándo genera
         * clases. No necesitamos marcarlo Baja.
         */
        turno.Estado = EstadoTurno.Activo;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    // =========================================================
    // GENERACIÓN DE SESIONES
    // =========================================================

    public async Task<GeneracionSesionesDto> GenerarSesionesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default
    )
    {
        // Se conserva por compatibilidad con ITurneraService.
        // Generación global/manual.
        return await GenerarSesionesInternoAsync(cicloLectivoId: null, desde, hasta, ct);
    }

    // =========================================================
    // GENERACIÓN DE SESIONES DE UN CICLO LECTIVO
    // =========================================================

    

    // =========================================================
    // GENERADOR INTERNO
    // =========================================================

    private async Task<GeneracionSesionesDto> GenerarSesionesInternoAsync(
        Guid? cicloLectivoId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct
    )
    {
        ValidarRango(desde, hasta);

        await using var db = await factory.CreateDbContextAsync(ct);

        // =====================================================
        // TURNOS
        // =====================================================

        var query = db
            .Turnos.AsNoTracking()
            .Include(x => x.Profesores)
            .Where(x =>
                x.Estado == EstadoTurno.Activo
                && x.VigenteDesde <= hasta
                && (x.VigenteHasta == null || x.VigenteHasta >= desde)
            );

        /*
         * Cuando la generación proviene de un ciclo lectivo
         * sólo debemos materializar clases pertenecientes
         * a ese ciclo.
         *
         * Esto evita mezclar horarios de distintos ciclos
         * que tengan fechas superpuestas.
         */
        if (cicloLectivoId.HasValue)
        {
            query = query.Where(x => x.CicloLectivoId == cicloLectivoId.Value);
        }

        var turnos = await query.ToListAsync(ct);

        if (turnos.Count == 0)
        {
            return new GeneracionSesionesDto(0, 0);
        }

        // =====================================================
        // SESIONES EXISTENTES
        // =====================================================

        var turnoIds = turnos.Select(x => x.Id).ToList();

        var existentes = await db
            .SesionesClase.AsNoTracking()
            .Where(x => turnoIds.Contains(x.TurnoId) && x.Fecha >= desde && x.Fecha <= hasta)
            .Select(x => new { x.TurnoId, x.Fecha })
            .ToListAsync(ct);

        var claves = existentes.Select(x => (x.TurnoId, x.Fecha)).ToHashSet();

        // =====================================================
        // DÍAS NO LABORABLES
        // =====================================================

        var diasNoLaborables = await db
            .DiasNoLaborables.AsNoTracking()
            .Where(x => x.Activo && x.Fecha >= desde && x.Fecha <= hasta)
            .ToListAsync(ct);

        var generadas = 0;

        var yaExistentes = 0;

        // =====================================================
        // GENERAR SESIONES
        // =====================================================

        foreach (var turno in turnos)
        {
            var inicio = turno.VigenteDesde > desde ? turno.VigenteDesde : desde;

            var fin =
                turno.VigenteHasta.HasValue && turno.VigenteHasta.Value < hasta
                    ? turno.VigenteHasta.Value
                    : hasta;

            for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
            {
                // -------------------------------------------------
                // DÍA DE SEMANA
                // -------------------------------------------------

                if (ObtenerDiaSemana(fecha) != turno.DiaSemana)
                {
                    continue;
                }

                // -------------------------------------------------
                // YA EXISTE
                // -------------------------------------------------

                if (claves.Contains((turno.Id, fecha)))
                {
                    yaExistentes++;

                    continue;
                }

                // -------------------------------------------------
                // FERIADO / CIERRE
                // -------------------------------------------------

                var cierre = diasNoLaborables
                    .Where(x => x.Fecha == fecha)
                    .FirstOrDefault(x => AfectaSesion(x, turno.HoraInicio, turno.DuracionMinutos));

                // -------------------------------------------------
                // CREAR SESIÓN
                // -------------------------------------------------

                var sesion = new SesionClase
                {
                    Id = Guid.NewGuid(),

                    TurnoId = turno.Id,

                    Fecha = fecha,

                    HoraInicio = turno.HoraInicio,

                    DuracionMinutos = turno.DuracionMinutos,

                    NivelId = turno.NivelId,

                    Cupo = turno.Cupo,

                    Sector = turno.Sector,

                    Estado = cierre is null
                        ? EstadoSesionClase.Programada
                        : EstadoSesionClase.Cancelada,

                    MotivoCancelacion = cierre is null ? null : cierre.Descripcion,

                    DiaNoLaborableId = cierre?.Id,
                };

                // -------------------------------------------------
                // PROFESORES
                // -------------------------------------------------

                foreach (var profesor in turno.Profesores)
                {
                    sesion.Profesores.Add(
                        new SesionClaseProfesor
                        {
                            SesionClaseId = sesion.Id,

                            ProfesorId = profesor.ProfesorId,

                            EsPrincipal = profesor.EsPrincipal,
                        }
                    );
                }

                db.SesionesClase.Add(sesion);

                claves.Add((turno.Id, fecha));

                generadas++;
            }
        }

        // =====================================================
        // GUARDAR SESIONES
        // =====================================================

        if (generadas > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        /*
         * Si ya llegó la fecha del feriado/cierre,
         * generamos los recuperos correspondientes.
         *
         * No generamos recuperos futuros antes de que
         * efectivamente llegue la clase perdida.
         */
        await generadorRecuperos.GenerarAsync(db, desde, hasta, ct);

        return new GeneracionSesionesDto(generadas, yaExistentes);
    }

    // =========================================================
    // AGENDA
    // =========================================================

    public async Task<IReadOnlyList<SesionClaseDto>> ObtenerAgendaAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default
    )
    {
        ValidarRango(desde, hasta);

        await using var db = await factory.CreateDbContextAsync(ct);

        var sesiones = await db
            .SesionesClase.AsNoTracking()
            .Include(x => x.Nivel)
            .Include(x => x.Turno)
            .Include(x => x.Profesores)
                .ThenInclude(x => x.Profesor)
            .Where(x => x.Fecha >= desde && x.Fecha <= hasta)
            .OrderBy(x => x.Fecha)
            .ThenBy(x => x.HoraInicio)
            .ToListAsync(ct);

        return await MapearSesionesAsync(db, sesiones, ct);
    }

    // =========================================================
    // DETALLE DE SESIÓN
    // =========================================================

    public async Task<SesionClaseDetalleDto?> ObtenerSesionAsync(
        Guid sesionId,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sesion = await db
            .SesionesClase.AsNoTracking()
            .Include(x => x.Nivel)
            .Include(x => x.Turno)
            .Include(x => x.Profesores)
                .ThenInclude(x => x.Profesor)
            .FirstOrDefaultAsync(x => x.Id == sesionId, ct);

        if (sesion is null)
            return null;

        var inscripciones = await db
            .InscripcionesTurnos.AsNoTracking()
            .Include(x => x.Inscripcion)
            .Where(x => x.TurnoId == sesion.TurnoId)
            .ToListAsync(ct);

        var reservasIniciales = await db
            .ReservasSesionesClase.AsNoTracking()
            .Where(x => x.SesionClaseId == sesion.Id && x.Estado == EstadoReservaSesion.Activa)
            .ToListAsync(ct);

        var recuperos = await db
            .Recuperaciones.AsNoTracking()
            .Where(x =>
                x.SesionUsoId == sesion.Id
                && (
                    x.Estado == EstadoRecuperacion.Reservada
                    || x.Estado == EstadoRecuperacion.Utilizada
                )
            )
            .ToListAsync(ct);

        var esperados = new Dictionary<Guid, (Guid InscripcionId, bool EsRecupero)>();

        foreach (var relacion in inscripciones)
        {
            if (!InscripcionValidaEnFecha(relacion.Inscripcion, sesion.Fecha))
            {
                continue;
            }

            esperados[relacion.Inscripcion.BebeId] = (relacion.InscripcionId, false);
        }

        // =========================================================
        // RESERVAS DE PRIMERA CLASE
        // =========================================================

        foreach (var reserva in reservasIniciales)
        {
            /*
             * Si ya aparece como alumno regular,
             * no lo duplicamos.
             */
            if (esperados.ContainsKey(reserva.BebeId))
            {
                continue;
            }

            esperados[reserva.BebeId] = (reserva.InscripcionId, false);
        }

        foreach (var recupero in recuperos)
        {
            if (!esperados.ContainsKey(recupero.BebeId))
            {
                esperados[recupero.BebeId] = (recupero.InscripcionId, true);
            }
        }

        var bebeIds = esperados.Keys.ToList();

        var bebes = await db
            .Bebes.AsNoTracking()
            .Where(x => bebeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var asistencias = await db
            .Asistencias.AsNoTracking()
            .Where(x => x.SesionClaseId == sesion.Id)
            .ToDictionaryAsync(x => x.BebeId, ct);

        var detalleBebes = new List<BebeSesionDto>();

        foreach (var esperado in esperados)
        {
            if (!bebes.TryGetValue(esperado.Key, out var bebe))
            {
                continue;
            }

            asistencias.TryGetValue(bebe.Id, out var asistencia);

            detalleBebes.Add(
                new BebeSesionDto(
                    bebe.Id,
                    esperado.Value.InscripcionId,
                    $"{bebe.Nombre} {bebe.Apellido}",
                    asistencia?.Estado,
                    esperado.Value.EsRecupero
                )
            );
        }

        detalleBebes = detalleBebes.OrderBy(x => x.NombreCompleto).ToList();

        var ocupacion = await CalculadoraCupoSesion.CalcularAsync(db, sesion, ct);

        var sesionDto = MapearSesion(sesion, ocupacion);

        return new SesionClaseDetalleDto(sesionDto, detalleBebes);
    }

    // =========================================================
    // CANCELAR UNA SESIÓN PARTICULAR
    // =========================================================

    public async Task CancelarSesionAsync(
        Guid sesionId,
        string motivo,
        bool generaRecupero = true,
        CancellationToken ct = default
    )
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("Debe indicar el motivo de la cancelación.");
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var sesion =
            await db.SesionesClase.FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("La clase indicada no existe.");

        if (sesion.Estado == EstadoSesionClase.Finalizada)
        {
            throw new InvalidOperationException("No puede cancelar una clase finalizada.");
        }

        if (sesion.Estado == EstadoSesionClase.Cancelada)
        {
            throw new InvalidOperationException("La clase ya se encuentra cancelada.");
        }

        sesion.Estado = EstadoSesionClase.Cancelada;

        sesion.MotivoCancelacion = motivo.Trim();

        /*
         * Es una cancelación manual,
         * no asociada a un DíaNoLaborable.
         */
        sesion.DiaNoLaborableId = null;

        sesion.CancelacionGeneraRecupero = generaRecupero;

        /*
         * Quien había reservado un recupero para usarlo en esta
         * clase lo recupera para elegir otra.
         */
        await GeneradorRecuperosClasesNoDictadas.LiberarReservasEnSesionesAsync(
            db,
            [sesion.Id],
            ct);

        await db.SaveChangesAsync(ct);

        /*
         * Si la clase es de hoy o ya pasó, el recupero se genera
         * ahora. Si es futura, lo genera el job diario cuando
         * llegue el día.
         */
        if (generaRecupero)
        {
            await generadorRecuperos.GenerarAsync(
                db,
                sesion.Fecha,
                sesion.Fecha,
                ct);
        }
    }

    // =========================================================
    // REACTIVAR SESIÓN
    // =========================================================

    public async Task ReactivarSesionAsync(Guid sesionId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sesion =
            await db.SesionesClase.FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("La clase indicada no existe.");

        if (sesion.Estado == EstadoSesionClase.Finalizada)
        {
            throw new InvalidOperationException("Una clase finalizada no puede reactivarse.");
        }

        var cierres = await db
            .DiasNoLaborables.AsNoTracking()
            .Where(x => x.Activo && x.Fecha == sesion.Fecha)
            .ToListAsync(ct);

        var cierre = cierres.FirstOrDefault(x =>
            AfectaSesion(x, sesion.HoraInicio, sesion.DuracionMinutos)
        );

        if (cierre is not null)
        {
            throw new InvalidOperationException(
                $"La clase no puede reactivarse porque está afectada por: {cierre.Descripcion}."
            );
        }

        /*
         * Si la clase vuelve a dictarse, los recuperos que generó
         * su cancelación dejan de corresponder. Si alguno ya se
         * usó, no se puede reactivar (igual que con los cierres).
         */
        var recuperosGenerados = await db
            .Recuperaciones.Where(x =>
                x.SesionOrigenId == sesion.Id
                && x.Tipo == TipoRecuperacion.ClaseNoDictada
                && x.Estado != EstadoRecuperacion.Anulada
            )
            .ToListAsync(ct);

        if (recuperosGenerados.Any(x => x.Estado == EstadoRecuperacion.Utilizada))
        {
            throw new InvalidOperationException(
                "La clase no puede reactivarse porque uno de los recuperos que generó ya fue utilizado."
            );
        }

        foreach (var recupero in recuperosGenerados)
        {
            recupero.Estado = EstadoRecuperacion.Anulada;
            recupero.SesionUsoId = null;
            recupero.UtilizadaUtc = null;
        }

        sesion.Estado = EstadoSesionClase.Programada;

        sesion.MotivoCancelacion = null;

        sesion.DiaNoLaborableId = null;

        sesion.CancelacionGeneraRecupero = false;

        await db.SaveChangesAsync(ct);
    }

    // =========================================================
    // FINALIZAR
    // =========================================================

    public async Task FinalizarSesionAsync(Guid sesionId, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sesion =
            await db.SesionesClase.FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("La clase indicada no existe.");

        if (sesion.Estado == EstadoSesionClase.Cancelada)
        {
            throw new InvalidOperationException("Una clase cancelada no puede finalizarse.");
        }

        sesion.Estado = EstadoSesionClase.Finalizada;

        await db.SaveChangesAsync(ct);
    }

    // =========================================================
    // CLASE ACTUAL
    // =========================================================

    public async Task<IReadOnlyList<SesionClaseDto>> ObtenerClasesEnCursoAsync(
        DateTime fechaHora,
        CancellationToken ct = default
    )
    {
        var fecha = DateOnly.FromDateTime(fechaHora);

        var hora = TimeOnly.FromDateTime(fechaHora);

        await using var db = await factory.CreateDbContextAsync(ct);

        var sesionesDelDia = await db
            .SesionesClase.AsNoTracking()
            .Include(x => x.Nivel)
            .Include(x => x.Turno)
                .ThenInclude(x => x.Profesores)
                    .ThenInclude(x => x.Profesor)
            .Where(x => x.Fecha == fecha && x.Estado == EstadoSesionClase.Programada)
            .ToListAsync(ct);

        /*
         * Filtramos en memoria porque HoraFin es
         * una propiedad calculada y no se persiste.
         */
        var enCurso = sesionesDelDia
            .Where(x => x.HoraInicio <= hora && x.HoraFin > hora)
            .OrderBy(x => x.HoraInicio)
            .ToList();

        return await MapearSesionesAsync(db, enCurso, ct);
    }

    // =========================================================
    // MAPEO SESIONES CON CUPO REAL
    // =========================================================

    private static async Task<IReadOnlyList<SesionClaseDto>> MapearSesionesAsync(
        AppDbContext db,
        IReadOnlyCollection<SesionClase> sesiones,
        CancellationToken ct
    )
    {
        if (sesiones.Count == 0)
            return [];

        /*
         * Misma cuenta que Recuperos y el alta de bebé
         * (ver CalculadoraCupoSesion).
         */
        var ocupacion = await CalculadoraCupoSesion.CalcularAsync(db, sesiones, ct);

        return sesiones
            .Select(x => MapearSesion(x, ocupacion[x.Id]))
            .ToList();
    }

    private static TurnoDto MapearTurno(Turno turno)
    {
        return new TurnoDto(
            turno.Id,
            turno.Nombre,
            turno.DiaSemana,
            turno.HoraInicio,
            turno.DuracionMinutos,
            turno.HoraFin,
            turno.NivelId,
            turno.Nivel.Nombre,
            turno.Cupo,
            turno.Sector,
            turno.VigenteDesde,
            turno.VigenteHasta,
            turno.Estado,
            turno
                .Profesores.OrderByDescending(x => x.EsPrincipal)
                .ThenBy(x => x.Profesor.Apellido)
                .Select(x => new ProfesorTurnoDto(
                    x.ProfesorId,
                    $"{x.Profesor.Nombre} {x.Profesor.Apellido}".Trim(),
                    x.EsPrincipal
                ))
                .ToList()
        );
    }

    private static SesionClaseDto MapearSesion(SesionClase sesion, OcupacionSesion ocupacion)
    {
        return MapearSesion(
            sesion,
            ocupacion.Esperados,
            ocupacion.Libres);
    }

    private static SesionClaseDto MapearSesion(SesionClase sesion, int inscriptos, int disponibles)
    {
        return new SesionClaseDto(
            sesion.Id,
            sesion.TurnoId,
            sesion.Turno.Nombre,
            sesion.Fecha,
            sesion.HoraInicio,
            sesion.DuracionMinutos,
            sesion.HoraFin,
            sesion.NivelId,
            sesion.Nivel.Nombre,
            sesion.Cupo,
            inscriptos,
            disponibles,
            sesion.Sector,
            sesion.Estado,
            sesion.MotivoCancelacion,
            sesion.DiaNoLaborableId,
            sesion
                .Profesores.OrderByDescending(x => x.EsPrincipal)
                .ThenBy(x => x.Profesor.Apellido)
                .Select(x => new ProfesorTurnoDto(
                    x.ProfesorId,
                    $"{x.Profesor.Nombre} {x.Profesor.Apellido}".Trim(),
                    x.EsPrincipal
                ))
                .ToList()
        );
    }

    // =========================================================
    // EDITAR UNA CLASE PUNTUAL
    // =========================================================

    public async Task ActualizarSesionClaseAsync(
        Guid sesionId,
        ActualizarSesionClaseDto dto,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var sesion =
            await db
                .SesionesClase.Include(x => x.Nivel)
                .Include(x => x.Profesores)
                .FirstOrDefaultAsync(x => x.Id == sesionId, ct)
            ?? throw new InvalidOperationException("La clase indicada no existe.");

        if (sesion.Estado != EstadoSesionClase.Programada)
        {
            throw new InvalidOperationException(
                "Sólo se pueden modificar profesores de una clase programada."
            );
        }

        // =====================================================
        // VALIDAR PROFESORES SEGÚN NIVEL
        // =====================================================

        ValidarProfesoresDto(dto.Profesores, sesion.Nivel);

        var profesorIds = dto.Profesores.Select(x => x.ProfesorId).Distinct().ToList();

        await ValidarProfesoresExistentesAsync(db, profesorIds, ct);

        // =====================================================
        // VALIDAR QUE NO ESTÉN EN OTRA CLASE A ESA HORA
        // =====================================================

        await ValidarSuperposicionProfesoresSesionAsync(
            db,
            sesion.Id,
            sesion.Fecha,
            sesion.HoraInicio,
            sesion.DuracionMinutos,
            profesorIds,
            ct
        );

        // =====================================================
        // SINCRONIZAR
        // =====================================================

        SincronizarProfesoresSesion(db, sesion, dto.Profesores);

        /*
         * Marca esta sesión como una excepción.
         * Un cambio futuro de la grilla recurrente
         * no debe sobreescribirla.
         */
        sesion.EsModificadaManualmente = true;

        await db.SaveChangesAsync(ct);
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void ValidarDatosBasicos(CrearTurnoDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            throw new InvalidOperationException("Debe ingresar el nombre del turno.");
        }

        if (dto.Nombre.Trim().Length > 150)
        {
            throw new InvalidOperationException(
                "El nombre del turno no puede superar los 150 caracteres."
            );
        }

        if (dto.DuracionMinutos <= 0)
        {
            throw new InvalidOperationException("La duración debe ser mayor a cero.");
        }

        if (dto.DuracionMinutos > 240)
        {
            throw new InvalidOperationException("La duración del turno no es válida.");
        }

        /*
         * Evita clases que atraviesen medianoche.
         */
        if (dto.HoraInicio.AddMinutes(dto.DuracionMinutos) <= dto.HoraInicio)
        {
            throw new InvalidOperationException("El turno no puede finalizar al día siguiente.");
        }

        if (dto.Cupo <= 0)
        {
            throw new InvalidOperationException("El cupo debe ser mayor a cero.");
        }

        if (dto.VigenteHasta.HasValue && dto.VigenteHasta.Value < dto.VigenteDesde)
        {
            throw new InvalidOperationException("La vigencia Hasta no puede ser anterior a Desde.");
        }

        if (dto.Sector?.Length > 100)
        {
            throw new InvalidOperationException("El sector no puede superar los 100 caracteres.");
        }
    }

    private static async Task<Nivel> ObtenerNivelAsync(
        AppDbContext db,
        Guid nivelId,
        CancellationToken ct
    )
    {
        var nivel = await db
            .Niveles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == nivelId && x.Activo, ct);

        return nivel
            ?? throw new InvalidOperationException(
                "El nivel seleccionado no existe o está inactivo."
            );
    }

    private static void ValidarProfesoresDto(
        IReadOnlyCollection<ProfesorTurnoInputDto> profesores,
        Nivel nivel
    )
    {
        var cantidad = profesores.Select(x => x.ProfesorId).Distinct().Count();

        if (cantidad < nivel.ProfesoresMinimos)
        {
            throw new InvalidOperationException(
                $"El nivel requiere al menos {nivel.ProfesoresMinimos} profesor/es."
            );
        }

        if (cantidad > nivel.ProfesoresMaximos)
        {
            throw new InvalidOperationException(
                $"El nivel admite como máximo {nivel.ProfesoresMaximos} profesor/es."
            );
        }

        if (profesores.Count != cantidad)
        {
            throw new InvalidOperationException(
                "No puede asignar el mismo profesor más de una vez."
            );
        }

        var principales = profesores.Count(x => x.EsPrincipal);

        if (principales != 1)
        {
            throw new InvalidOperationException("Debe existir exactamente un profesor principal.");
        }
    }

    private static async Task ValidarProfesoresExistentesAsync(
        AppDbContext db,
        IReadOnlyCollection<Guid> profesorIds,
        CancellationToken ct
    )
    {
        var cantidad = await db
            .Profesores.AsNoTracking()
            .CountAsync(x => profesorIds.Contains(x.Id) && x.Activo, ct);

        if (cantidad != profesorIds.Count)
        {
            throw new InvalidOperationException(
                "Uno o más profesores no existen o están inactivos."
            );
        }
    }

    private static async Task ValidarSuperposicionProfesoresAsync(
        AppDbContext db,
        Guid? turnoIdExcluir,
        DiaSemana diaSemana,
        TimeOnly horaInicio,
        int duracionMinutos,
        DateOnly vigenteDesde,
        DateOnly? vigenteHasta,
        IReadOnlyCollection<Guid> profesorIds,
        CancellationToken ct
    )
    {
        if (profesorIds.Count == 0)
            return;

        var candidatos = await db
            .Turnos.AsNoTracking()
            .Include(x => x.Profesores)
            .Where(x =>
                x.Estado == EstadoTurno.Activo
                && x.DiaSemana == diaSemana
                && x.Profesores.Any(p => profesorIds.Contains(p.ProfesorId))
            )
            .ToListAsync(ct);

        if (turnoIdExcluir.HasValue)
        {
            candidatos = candidatos.Where(x => x.Id != turnoIdExcluir.Value).ToList();
        }

        var horaFin = horaInicio.AddMinutes(duracionMinutos);

        foreach (var existente in candidatos)
        {
            if (
                !PeriodosSeSuperponen(
                    vigenteDesde,
                    vigenteHasta,
                    existente.VigenteDesde,
                    existente.VigenteHasta
                )
            )
            {
                continue;
            }

            if (!HorariosSeSuperponen(horaInicio, horaFin, existente.HoraInicio, existente.HoraFin))
            {
                continue;
            }

            var profesoresConflicto = existente
                .Profesores.Where(x => profesorIds.Contains(x.ProfesorId))
                .Select(x => x.ProfesorId)
                .ToList();

            if (profesoresConflicto.Count > 0)
            {
                throw new InvalidOperationException(
                    "Uno de los profesores ya tiene otro turno superpuesto en ese horario."
                );
            }
        }
    }

    private static bool PeriodosSeSuperponen(
        DateOnly inicio1,
        DateOnly? fin1,
        DateOnly inicio2,
        DateOnly? fin2
    )
    {
        var final1 = fin1 ?? DateOnly.MaxValue;

        var final2 = fin2 ?? DateOnly.MaxValue;

        return inicio1 <= final2 && inicio2 <= final1;
    }

    private static bool HorariosSeSuperponen(
        TimeOnly inicio1,
        TimeOnly fin1,
        TimeOnly inicio2,
        TimeOnly fin2
    )
    {
        return inicio1 < fin2 && inicio2 < fin1;
    }

    private static bool InscripcionValidaEnFecha(Inscripcion inscripcion, DateOnly fecha)
    {
        if (inscripcion.Estado is EstadoInscripcion.Cancelada or EstadoInscripcion.Pausada)
        {
            return false;
        }

        if (inscripcion.FechaInicio > fecha)
        {
            return false;
        }

        if (inscripcion.FechaFin.HasValue && inscripcion.FechaFin.Value < fecha)
        {
            return false;
        }

        return true;
    }

    private static bool AfectaSesion(DiaNoLaborable dia, TimeOnly horaInicio, int duracion)
    {
        if (dia.TodoElDia)
            return true;

        if (!dia.HoraDesde.HasValue || !dia.HoraHasta.HasValue)
        {
            return false;
        }

        var horaFin = horaInicio.AddMinutes(duracion);

        return horaInicio < dia.HoraHasta.Value && dia.HoraDesde.Value < horaFin;
    }

    private static DiaSemana ObtenerDiaSemana(DateOnly fecha)
    {
        return fecha.DayOfWeek switch
        {
            DayOfWeek.Monday => DiaSemana.Lunes,

            DayOfWeek.Tuesday => DiaSemana.Martes,

            DayOfWeek.Wednesday => DiaSemana.Miercoles,

            DayOfWeek.Thursday => DiaSemana.Jueves,

            DayOfWeek.Friday => DiaSemana.Viernes,

            DayOfWeek.Saturday => DiaSemana.Sabado,

            DayOfWeek.Sunday => DiaSemana.Domingo,

            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private static void ValidarRango(DateOnly desde, DateOnly hasta)
    {
        if (hasta < desde)
        {
            throw new InvalidOperationException("La fecha Hasta no puede ser anterior a Desde.");
        }

        if (hasta.DayNumber - desde.DayNumber > 366)
        {
            throw new InvalidOperationException("No puede consultar más de un año por vez.");
        }
    }

    private static string? Normalizar(string? valor)
    {
        return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
    }

    public async Task<IReadOnlyList<NivelTurneraDto>> ListarNivelesAsync(
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db
            .Niveles.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Orden)
            .ThenBy(x => x.Nombre)
            .Select(x => new NivelTurneraDto(
                x.Id,
                x.Nombre,
                x.DuracionMinutos,
                x.CupoPredeterminado,
                x.ProfesoresMinimos,
                x.ProfesoresMaximos
            ))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProfesorDisponibleDto>> ListarProfesoresAsync(
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        return await db
            .Profesores.AsNoTracking()
            .Where(x => x.Activo)
            .OrderBy(x => x.Apellido)
            .ThenBy(x => x.Nombre)
            .Select(x => new ProfesorDisponibleDto(x.Id, (x.Nombre + " " + x.Apellido).Trim()))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TurnoDto>> ListarTurnosCicloAsync(
        Guid cicloLectivoId,
        bool incluirBajas = false,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db
            .Turnos.AsNoTracking()
            .Include(x => x.Nivel)
            .Include(x => x.Profesores)
                .ThenInclude(x => x.Profesor)
            .Where(x => x.CicloLectivoId == cicloLectivoId)
            .AsQueryable();

        if (!incluirBajas)
        {
            query = query.Where(x => x.Estado != EstadoTurno.Baja);
        }

        var turnos = await query.ToListAsync(ct);

        return turnos
            .OrderBy(x => (int)x.DiaSemana)
            .ThenBy(x => x.HoraInicio)
            .Select(MapearTurno)
            .ToList();
    }

    public async Task<PublicarCicloLectivoResultadoDto> PublicarCicloLectivoAsync(
        Guid cicloLectivoId,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var ciclo =
            await db.CiclosLectivos.FirstOrDefaultAsync(x => x.Id == cicloLectivoId, ct)
            ?? throw new InvalidOperationException("El ciclo lectivo indicado no existe.");

        if (ciclo.Estado == EstadoCicloLectivo.Cerrado)
        {
            throw new InvalidOperationException("El ciclo lectivo está cerrado.");
        }

        var cantidadTurnos = await db.Turnos.CountAsync(
            x => x.CicloLectivoId == ciclo.Id && x.Estado == EstadoTurno.Activo,
            ct
        );

        if (cantidadTurnos == 0)
        {
            throw new InvalidOperationException("No hay horarios configurados para publicar.");
        }

        /*
         * Genera todas las clases del ciclo.
         *
         * GenerarSesionesAsync ya evita duplicados,
         * por lo que si alguna sesión ya existe
         * simplemente no vuelve a crearla.
         */
      var generacion =
    await GenerarSesionesCicloAsync(
        ciclo.Id,
        ciclo.FechaInicio,
        ciclo.FechaFin,
        ct);

        ciclo.Estado = EstadoCicloLectivo.Publicado;

        /*
         * Conservamos la fecha de la primera publicación.
         */
        ciclo.FechaPublicacionUtc ??= DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return new PublicarCicloLectivoResultadoDto(
            cantidadTurnos,
            generacion.Generadas,
            generacion.YaExistentes
        );
    }

    public async Task<GeneracionSesionesDto>
    GenerarSesionesFaltantesCicloAsync(
        Guid cicloLectivoId,
        CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);

    var ciclo =
        await db.CiclosLectivos
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == cicloLectivoId,
                ct)
        ?? throw new InvalidOperationException(
            "El ciclo lectivo indicado no existe.");

    if (ciclo.Estado == EstadoCicloLectivo.Cerrado)
    {
        throw new InvalidOperationException(
            "No se pueden generar sesiones de un ciclo cerrado.");
    }

    return await GenerarSesionesCicloAsync(
        ciclo.Id,
        ciclo.FechaInicio,
        ciclo.FechaFin,
        ct);
}

private async Task<GeneracionSesionesDto>
    GenerarSesionesCicloAsync(
        Guid cicloLectivoId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken ct = default)
{
    ValidarRango(
        desde,
        hasta);

    await using var db =
        await factory.CreateDbContextAsync(ct);

    var turnos =
        await db.Turnos
            .AsNoTracking()
            .Include(x => x.Profesores)
            .Where(x =>
                x.CicloLectivoId == cicloLectivoId
                &&
                x.Estado == EstadoTurno.Activo
                &&
                x.VigenteDesde <= hasta
                &&
                (
                    x.VigenteHasta == null
                    ||
                    x.VigenteHasta >= desde
                ))
            .ToListAsync(ct);

    if (turnos.Count == 0)
    {
        return new GeneracionSesionesDto(
            0,
            0);
    }

    var turnoIds =
        turnos
            .Select(x => x.Id)
            .ToList();

    var existentes =
        await db.SesionesClase
            .AsNoTracking()
            .Where(x =>
                turnoIds.Contains(x.TurnoId)
                &&
                x.Fecha >= desde
                &&
                x.Fecha <= hasta)
            .Select(x => new
            {
                x.TurnoId,
                x.Fecha
            })
            .ToListAsync(ct);

    var claves =
        existentes
            .Select(x =>
                (x.TurnoId, x.Fecha))
            .ToHashSet();

    var diasNoLaborables =
        await db.DiasNoLaborables
            .AsNoTracking()
            .Where(x =>
                x.Activo
                &&
                x.Fecha >= desde
                &&
                x.Fecha <= hasta)
            .ToListAsync(ct);

    var generadas = 0;
    var yaExistentes = 0;

    foreach (var turno in turnos)
    {
        var inicio =
            turno.VigenteDesde > desde
                ? turno.VigenteDesde
                : desde;

        var fin =
            turno.VigenteHasta.HasValue &&
            turno.VigenteHasta.Value < hasta
                ? turno.VigenteHasta.Value
                : hasta;

        for (
            var fecha = inicio;
            fecha <= fin;
            fecha = fecha.AddDays(1))
        {
            if (ObtenerDiaSemana(fecha) !=
                turno.DiaSemana)
            {
                continue;
            }

            if (claves.Contains(
                (turno.Id, fecha)))
            {
                yaExistentes++;

                continue;
            }

            var cierre =
                diasNoLaborables
                    .Where(x =>
                        x.Fecha == fecha)
                    .FirstOrDefault(x =>
                        AfectaSesion(
                            x,
                            turno.HoraInicio,
                            turno.DuracionMinutos));

            var sesion =
                new SesionClase
                {
                    Id = Guid.NewGuid(),

                    TurnoId =
                        turno.Id,

                    Fecha =
                        fecha,

                    HoraInicio =
                        turno.HoraInicio,

                    DuracionMinutos =
                        turno.DuracionMinutos,

                    NivelId =
                        turno.NivelId,

                    Cupo =
                        turno.Cupo,

                    Sector =
                        turno.Sector,

                    Estado =
                        cierre is null
                            ? EstadoSesionClase.Programada
                            : EstadoSesionClase.Cancelada,

                    MotivoCancelacion =
                        cierre is null
                            ? null
                            : cierre.Descripcion,

                    DiaNoLaborableId =
                        cierre?.Id
                };

            foreach (var profesor in
                turno.Profesores)
            {
                sesion.Profesores.Add(
                    new SesionClaseProfesor
                    {
                        SesionClaseId =
                            sesion.Id,

                        ProfesorId =
                            profesor.ProfesorId,

                        EsPrincipal =
                            profesor.EsPrincipal
                    });
            }

            db.SesionesClase.Add(
                sesion);

            claves.Add(
                (turno.Id, fecha));

            generadas++;
        }
    }

    if (generadas > 0)
    {
        await db.SaveChangesAsync(ct);
    }

    await generadorRecuperos.GenerarAsync(
        db,
        desde,
        hasta,
        ct);

    return new GeneracionSesionesDto(
        generadas,
        yaExistentes);
}

    private static void SincronizarProfesoresSesion(
        AppDbContext db,
        SesionClase sesion,
        IReadOnlyCollection<ProfesorTurnoInputDto> profesores
    )
    {
        var nuevos = profesores.ToDictionary(x => x.ProfesorId);

        var actuales = sesion.Profesores.ToList();

        foreach (var actual in actuales)
        {
            if (!nuevos.TryGetValue(actual.ProfesorId, out var nuevo))
            {
                db.SesionesClaseProfesores.Remove(actual);

                continue;
            }

            actual.EsPrincipal = nuevo.EsPrincipal;
        }

        var actualesIds = actuales.Select(x => x.ProfesorId).ToHashSet();

        foreach (var nuevo in profesores)
        {
            if (actualesIds.Contains(nuevo.ProfesorId))
            {
                continue;
            }

            sesion.Profesores.Add(
                new SesionClaseProfesor
                {
                    SesionClaseId = sesion.Id,

                    ProfesorId = nuevo.ProfesorId,

                    EsPrincipal = nuevo.EsPrincipal,

                    EsReemplazo = true,

                    ControlConfirmado = false,

                    DictoClase = false,
                }
            );
        }
    }

    private static async Task ValidarSuperposicionProfesoresSesionAsync(
        AppDbContext db,
        Guid sesionIdExcluir,
        DateOnly fecha,
        TimeOnly horaInicio,
        int duracionMinutos,
        IReadOnlyCollection<Guid> profesorIds,
        CancellationToken ct
    )
    {
        if (profesorIds.Count == 0)
            return;

        var sesiones = await db
            .SesionesClase.AsNoTracking()
            .Include(x => x.Profesores)
            .Where(x =>
                x.Id != sesionIdExcluir
                && x.Fecha == fecha
                && x.Estado != EstadoSesionClase.Cancelada
                && x.Profesores.Any(p => profesorIds.Contains(p.ProfesorId))
            )
            .ToListAsync(ct);

        var horaFin = horaInicio.AddMinutes(duracionMinutos);

        foreach (var otra in sesiones)
        {
            if (!HorariosSeSuperponen(horaInicio, horaFin, otra.HoraInicio, otra.HoraFin))
            {
                continue;
            }

            throw new InvalidOperationException(
                "Uno de los profesores ya está asignado a otra clase en ese horario."
            );
        }
    }
}
