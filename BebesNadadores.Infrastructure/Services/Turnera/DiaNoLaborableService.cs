using BebesNadadores.Application.Turnera;
using BebesNadadores.Application.Turnera.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Application.Configuracion;

namespace BebesNadadores.Infrastructure.Services.Turnera;

public sealed class DiaNoLaborableService(
    IDbContextFactory<AppDbContext> factory,
    IConfiguracionSistemaService configuracionService)
    : IDiaNoLaborableService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<DiaNoLaborableDto>>
        ListarAsync(
            int anio,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var desde =
            new DateOnly(
                anio,
                1,
                1);

        var hasta =
            new DateOnly(
                anio,
                12,
                31);

        return await db.DiasNoLaborables
            .AsNoTracking()
            .Where(x =>
                x.Fecha >= desde &&
                x.Fecha <= hasta)
            .OrderBy(x => x.Fecha)
            .ThenBy(x => x.HoraDesde)
            .Select(x =>
                new DiaNoLaborableDto(
                    x.Id,
                    x.Fecha,
                    x.Tipo,
                    x.Descripcion,
                    x.TodoElDia,
                    x.HoraDesde,
                    x.HoraHasta,
                    x.GeneraRecupero,
                    x.RecuperoSinVencimiento,
                    x.Activo))
            .ToListAsync(ct);
    }


    // =========================================================
    // CREAR
    // =========================================================

    public async Task<Guid> CrearAsync(
        GuardarDiaNoLaborableDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        
            var existentes =
                await db.DiasNoLaborables
                    .AsNoTracking()
                    .Where(x =>
                        x.Activo &&
                        x.Fecha ==
                        dto.Fecha)
                    .ToListAsync(ct);

            if (dto.TodoElDia &&
                existentes.Count > 0)
            {
                throw new InvalidOperationException(
                    "Ya existe un día no laborable activo para esa fecha.");
            }

            if (existentes.Any(x =>
                x.TodoElDia))
            {
                throw new InvalidOperationException(
                    "La fecha ya está marcada como no laborable durante todo el día.");
            }

            if (!dto.TodoElDia)
            {
                foreach (var existente in existentes)
                {
                    if (!existente.HoraDesde.HasValue ||
                        !existente.HoraHasta.HasValue)
                    {
                        continue;
                    }

                    if (HorariosSeSuperponen(
                        dto.HoraDesde!.Value,
                        dto.HoraHasta!.Value,
                        existente.HoraDesde.Value,
                        existente.HoraHasta.Value))
                    {
                        throw new InvalidOperationException(
                            "Ya existe un cierre que se superpone con ese horario.");
                    }
                }
            }
        

        var dia =
            new DiaNoLaborable
            {
                Id =
                    Guid.NewGuid(),

                Fecha =
                    dto.Fecha,

                Tipo =
                    dto.Tipo,

                Descripcion =
                    dto.Descripcion.Trim(),

                TodoElDia =
                    dto.TodoElDia,

                HoraDesde =
                    dto.TodoElDia
                        ? null
                        : dto.HoraDesde,

                HoraHasta =
                    dto.TodoElDia
                        ? null
                        : dto.HoraHasta,

                GeneraRecupero =
                    dto.GeneraRecupero,

                RecuperoSinVencimiento =
                    dto.RecuperoSinVencimiento,

                Activo =
                    true
            };

        db.DiasNoLaborables.Add(dia);

        await db.SaveChangesAsync(ct);


        /*
         * Si la Turnera ya había generado
         * sesiones de esta fecha, las recalculamos.
         */
        await RecalcularSesionesFechaAsync(
            db,
            dto.Fecha,
            ct);

        return dia.Id;
    }


    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public async Task ActualizarAsync(
        Guid id,
        GuardarDiaNoLaborableDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var dia =
            await db.DiasNoLaborables
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El día no laborable indicado no existe.");


        await ValidarSinRecuperosUtilizadosAsync(
            db,
            dia.Id,
            ct);


        var fechaAnterior =
            dia.Fecha;


        await ValidarSuperposicionAsync(
            db,
            id,
            dto,
            ct);


        dia.Fecha =
            dto.Fecha;

        dia.Tipo =
            dto.Tipo;

        dia.Descripcion =
            dto.Descripcion.Trim();

        dia.TodoElDia =
            dto.TodoElDia;

        dia.HoraDesde =
            dto.TodoElDia
                ? null
                : dto.HoraDesde;

        dia.HoraHasta =
            dto.TodoElDia
                ? null
                : dto.HoraHasta;

        dia.GeneraRecupero =
            dto.GeneraRecupero;

        dia.RecuperoSinVencimiento =
            dto.RecuperoSinVencimiento;


        await db.SaveChangesAsync(ct);


        /*
         * Si se cambió de fecha,
         * recalculamos ambas.
         */
        await RecalcularSesionesFechaAsync(
            db,
            fechaAnterior,
            ct);


        if (dto.Fecha != fechaAnterior)
        {
            await RecalcularSesionesFechaAsync(
                db,
                dto.Fecha,
                ct);
        }
    }


    // =========================================================
    // ACTIVAR / DESACTIVAR
    // =========================================================

    public async Task CambiarEstadoAsync(
        Guid id,
        bool activo,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var dia =
            await db.DiasNoLaborables
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El día no laborable indicado no existe.");


        if (!activo)
        {
            await ValidarSinRecuperosUtilizadosAsync(
                db,
                dia.Id,
                ct);
        }


        dia.Activo =
            activo;

        await db.SaveChangesAsync(ct);


        await RecalcularSesionesFechaAsync(
            db,
            dia.Fecha,
            ct);
    }


    // =========================================================
    // RECALCULAR SESIONES
    // =========================================================

    private  async Task
        RecalcularSesionesFechaAsync(
            AppDbContext db,
            DateOnly fecha,
            CancellationToken ct)
    {
        var cierres =
            await db.DiasNoLaborables
                .AsNoTracking()
                .Where(x =>
                    x.Activo &&
                    x.Fecha == fecha)
                .OrderByDescending(x =>
                    x.TodoElDia)
                .ThenBy(x =>
                    x.HoraDesde)
                .ToListAsync(ct);


        var sesiones =
            await db.SesionesClase
                .Where(x =>
                    x.Fecha == fecha &&
                    x.Estado !=
                        EstadoSesionClase.Finalizada)
                .ToListAsync(ct);


        foreach (var sesion in sesiones)
        {
            var cierre =
                cierres.FirstOrDefault(x =>
                    AfectaSesion(
                        x,
                        sesion.HoraInicio,
                        sesion.DuracionMinutos));


            if (cierre is not null)
            {
                sesion.Estado =
                    EstadoSesionClase.Cancelada;

                sesion.DiaNoLaborableId =
                    cierre.Id;

                sesion.MotivoCancelacion =
                    cierre.Descripcion;

                continue;
            }


            /*
             * Reactivamos automáticamente solamente
             * si la cancelación provenía de un
             * DiaNoLaborable.
             *
             * Una cancelación manual no se toca.
             */
            if (sesion.DiaNoLaborableId.HasValue)
            {
                sesion.Estado =
                    EstadoSesionClase.Programada;

                sesion.DiaNoLaborableId =
                    null;

                sesion.MotivoCancelacion =
                    null;
            }
        }


        await db.SaveChangesAsync(ct);


        await ReconciliarRecuperosAsync(
            db,
            sesiones,
            cierres,
            ct);
    }


    // =========================================================
    // RECUPEROS
    // =========================================================

    private async Task ReconciliarRecuperosAsync(
            AppDbContext db,
            IReadOnlyCollection<SesionClase> sesiones,
            IReadOnlyCollection<DiaNoLaborable> cierres,
            CancellationToken ct)
    {

        var configuracion =
    await configuracionService.ObtenerAsync(ct);
        if (sesiones.Count == 0)
            return;


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);

        var sesionIds =
            sesiones
                .Select(x => x.Id)
                .ToList();


        var recuperosExistentes =
            await db.Recuperaciones
                .Where(x =>
                    sesionIds.Contains(
                        x.SesionOrigenId) &&
                    x.Tipo ==
                        TipoRecuperacion.ClaseNoDictada)
                .ToListAsync(ct);


        // -----------------------------------------------------
        // Anular recuperos si el cierre dejó de aplicar.
        // -----------------------------------------------------

        foreach (var recupero in recuperosExistentes)
        {
            var sesion =
                sesiones.First(x =>
                    x.Id ==
                    recupero.SesionOrigenId);

            var corresponde =
                sesion.Estado ==
                    EstadoSesionClase.Cancelada &&
                sesion.DiaNoLaborableId.HasValue;


            if (!corresponde &&
                recupero.Estado is
                    EstadoRecuperacion.Disponible or
                    EstadoRecuperacion.Reservada)
            {
                recupero.Estado =
                    EstadoRecuperacion.Anulada;
            }
        }


        var canceladas =
            sesiones
                .Where(x =>
                    x.Fecha <= hoy &&
                    x.Estado ==
                        EstadoSesionClase.Cancelada &&
                    x.DiaNoLaborableId.HasValue)
                .ToList();


        if (canceladas.Count == 0)
        {
            await db.SaveChangesAsync(ct);

            return;
        }


        var turnos =
            canceladas
                .Select(x => x.TurnoId)
                .Distinct()
                .ToList();


        var inscripciones =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Include(x => x.Inscripcion)
                .Where(x =>
                    turnos.Contains(
                        x.TurnoId))
                .ToListAsync(ct);


        var existentes =
            recuperosExistentes
                .Select(x =>
                    (
                        x.BebeId,
                        x.SesionOrigenId
                    ))
                .ToHashSet();


        foreach (var sesion in canceladas)
        {
            var cierre =
                cierres.FirstOrDefault(x =>
                    x.Id ==
                    sesion.DiaNoLaborableId);

            if (cierre is null ||
                !cierre.GeneraRecupero)
            {
                continue;
            }


            var inscriptos =
                inscripciones
                    .Where(x =>
                        x.TurnoId ==
                            sesion.TurnoId &&
                        InscripcionValidaEnFecha(
                            x.Inscripcion,
                            sesion.Fecha));


            foreach (var inscripto in inscriptos)
            {
                var bebeId =
                    inscripto
                        .Inscripcion
                        .BebeId;


                if (existentes.Contains(
                    (bebeId, sesion.Id)))
                {
                    continue;
                }


                db.Recuperaciones.Add(
                    new Recuperacion
                    {
                        Id =
                            Guid.NewGuid(),

                        BebeId =
                            bebeId,

                        InscripcionId =
                            inscripto.InscripcionId,

                        SesionOrigenId =
                            sesion.Id,

                        AsistenciaOrigenId =
                            null,

                        Tipo =
                            TipoRecuperacion
                                .ClaseNoDictada,

                        FechaGeneracion =
                            sesion.Fecha,

                        FechaVencimiento =
                            cierre.RecuperoSinVencimiento
                                ? null
                                : sesion.Fecha.AddDays(
    configuracion.DiasVigenciaRecupero),

                        Estado =
                            EstadoRecuperacion.Disponible
                    });


                existentes.Add(
                    (bebeId, sesion.Id));
            }
        }


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void Validar(
        GuardarDiaNoLaborableDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Descripcion))
        {
            throw new InvalidOperationException(
                "Debe ingresar una descripción.");
        }

        if (dto.Descripcion.Trim().Length > 300)
        {
            throw new InvalidOperationException(
                "La descripción no puede superar los 300 caracteres.");
        }

        if (dto.TodoElDia)
            return;


        if (!dto.HoraDesde.HasValue ||
            !dto.HoraHasta.HasValue)
        {
            throw new InvalidOperationException(
                "Debe indicar Hora Desde y Hora Hasta.");
        }


        if (dto.HoraHasta.Value <=
            dto.HoraDesde.Value)
        {
            throw new InvalidOperationException(
                "Hora Hasta debe ser posterior a Hora Desde.");
        }
    }


    private static async Task
        ValidarSuperposicionAsync(
            AppDbContext db,
            Guid idExcluir,
            GuardarDiaNoLaborableDto dto,
            CancellationToken ct)
    {
        var existentes =
            await db.DiasNoLaborables
                .AsNoTracking()
                .Where(x =>
                    x.Id != idExcluir &&
                    x.Activo &&
                    x.Fecha ==
                        dto.Fecha)
                .ToListAsync(ct);


        if (dto.TodoElDia &&
            existentes.Count > 0)
        {
            throw new InvalidOperationException(
                "Ya existe otro cierre activo para esa fecha.");
        }


        if (existentes.Any(x =>
            x.TodoElDia))
        {
            throw new InvalidOperationException(
                "La fecha ya posee un cierre de día completo.");
        }


        if (!dto.TodoElDia)
        {
            foreach (var existente in existentes)
            {
                if (!existente.HoraDesde.HasValue ||
                    !existente.HoraHasta.HasValue)
                {
                    continue;
                }


                if (HorariosSeSuperponen(
                    dto.HoraDesde!.Value,
                    dto.HoraHasta!.Value,
                    existente.HoraDesde.Value,
                    existente.HoraHasta.Value))
                {
                    throw new InvalidOperationException(
                        "El horario se superpone con otro cierre existente.");
                }
            }
        }
    }


    private static async Task
        ValidarSinRecuperosUtilizadosAsync(
            AppDbContext db,
            Guid diaNoLaborableId,
            CancellationToken ct)
    {
        var sesionIds =
            await db.SesionesClase
                .AsNoTracking()
                .Where(x =>
                    x.DiaNoLaborableId ==
                    diaNoLaborableId)
                .Select(x => x.Id)
                .ToListAsync(ct);


        if (sesionIds.Count == 0)
            return;


        var utilizado =
            await db.Recuperaciones
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        sesionIds.Contains(
                            x.SesionOrigenId) &&

                        x.Tipo ==
                            TipoRecuperacion
                                .ClaseNoDictada &&

                        x.Estado ==
                            EstadoRecuperacion
                                .Utilizada,
                    ct);


        if (utilizado)
        {
            throw new InvalidOperationException(
                "No puede modificar o desactivar este cierre porque uno de sus recuperos ya fue utilizado.");
        }
    }


    private static bool AfectaSesion(
        DiaNoLaborable dia,
        TimeOnly inicio,
        int duracion)
    {
        if (dia.TodoElDia)
            return true;


        if (!dia.HoraDesde.HasValue ||
            !dia.HoraHasta.HasValue)
        {
            return false;
        }


        var fin =
            inicio.AddMinutes(
                duracion);


        return
            inicio < dia.HoraHasta.Value &&
            dia.HoraDesde.Value < fin;
    }


    private static bool HorariosSeSuperponen(
        TimeOnly inicio1,
        TimeOnly fin1,
        TimeOnly inicio2,
        TimeOnly fin2)
    {
        return
            inicio1 < fin2 &&
            inicio2 < fin1;
    }


    private static bool InscripcionValidaEnFecha(
        Inscripcion inscripcion,
        DateOnly fecha)
    {
        if (inscripcion.Estado is
            EstadoInscripcion.Cancelada or
            EstadoInscripcion.Pausada)
        {
            return false;
        }


        if (inscripcion.FechaInicio >
            fecha)
        {
            return false;
        }


        if (inscripcion.FechaFin.HasValue &&
            inscripcion.FechaFin.Value <
            fecha)
        {
            return false;
        }


        return true;
    }
}