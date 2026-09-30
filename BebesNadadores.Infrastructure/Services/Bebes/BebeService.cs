using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bebes;

public sealed class BebeService(
    IDbContextFactory<AppDbContext> factory)
    : IBebeService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<BebeDto>> ListarAsync(
        bool incluirBajas = false,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var query =
            db.Bebes
                .AsNoTracking()
                .Include(x => x.NivelActual)
                .Include(x => x.Responsables)
                    .ThenInclude(x => x.Responsable)
                .AsQueryable();


        if (!incluirBajas)
        {
            query =
                query.Where(x =>
                    x.Estado ==
                    EstadoBebe.Activo);
        }


        var bebes =
            await query
                .OrderBy(x => x.Apellido)
                .ThenBy(x => x.Nombre)
                .ToListAsync(ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        return bebes
            .Select(x =>
                MapearBebe(
                    x,
                    hoy))
            .ToList();
    }


    // =========================================================
    // OBTENER
    // =========================================================

    public async Task<BebeDto?> ObtenerAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var bebe =
            await db.Bebes
                .AsNoTracking()
                .Include(x => x.NivelActual)
                .Include(x => x.Responsables)
                    .ThenInclude(x => x.Responsable)
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct);


        if (bebe is null)
            return null;


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        return MapearBebe(
            bebe,
            hoy);
    }


    // =========================================================
    // CREAR
    // =========================================================

   public async Task<Guid> CrearAsync(
    GuardarBebeDto dto,
    CancellationToken ct = default)
{
    ValidarDto(dto);


    await using var db =
        await factory.CreateDbContextAsync(ct);


    /*
     * Serializable evita que dos administrativas puedan
     * quedarse simultáneamente con el último cupo disponible.
     */
    await using var transaction =
        await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct);


    // =========================================================
    // FECHAS BÁSICAS
    // =========================================================

    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    if (dto.FechaNacimiento > hoy)
    {
        throw new InvalidOperationException(
            "La fecha de nacimiento no puede ser futura.");
    }


    if (dto.FechaInicioInscripcion <
        dto.FechaNacimiento)
    {
        throw new InvalidOperationException(
            "La fecha de inicio de la inscripción no puede ser anterior al nacimiento.");
    }


    // =========================================================
    // NIVEL
    // =========================================================

    if (!dto.NivelActualId.HasValue)
    {
        throw new InvalidOperationException(
            "Debe seleccionar el nivel del bebé.");
    }


    var nivel =
        await db.Niveles
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        dto.NivelActualId.Value
                    &&
                    x.Activo,
                ct)
        ?? throw new InvalidOperationException(
            "El nivel seleccionado no existe o se encuentra inactivo.");


    // =========================================================
    // VALIDAR EDAD PARA EL NIVEL
    // =========================================================

    var edadMesesAlInicio =
        CalcularEdadMeses(
            dto.FechaNacimiento,
            dto.FechaInicioInscripcion);


    if (
        edadMesesAlInicio <
            nivel.EdadMinMeses
        ||
        edadMesesAlInicio >
            nivel.EdadMaxMeses)
    {
        throw new InvalidOperationException(
            $"La edad del bebé no corresponde al nivel {nivel.Nombre}. " +
            $"El nivel admite entre {nivel.EdadMinMeses} y " +
            $"{nivel.EdadMaxMeses} meses.");
    }


    if (edadMesesAlInicio >= 84)
    {
        throw new InvalidOperationException(
            "El bebé ya alcanzó la edad máxima de 7 años permitida.");
    }


    // =========================================================
    // DNI
    // =========================================================

    await ValidarDniBebeAsync(
        db,
        dto.Dni,
        null,
        ct);


    // =========================================================
    // PLAN
    // =========================================================

    if (!dto.PlanId.HasValue)
    {
        throw new InvalidOperationException(
            "Debe seleccionar un plan.");
    }


    var plan =
        await db.Planes
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        dto.PlanId.Value
                    &&
                    x.Activo,
                ct)
        ?? throw new InvalidOperationException(
            "El plan seleccionado no existe o se encuentra inactivo.");

// =========================================================
// DESCUENTO MANUAL
// =========================================================

if (dto.TipoDescuento.HasValue)
{
    if (
        !dto.ValorDescuento.HasValue
        ||
        dto.ValorDescuento.Value <= 0)
    {
        throw new InvalidOperationException(
            "Debe indicar el valor del descuento.");
    }


    if (
        dto.TipoDescuento ==
            TipoDescuento.Porcentaje
        &&
        dto.ValorDescuento.Value > 100)
    {
        throw new InvalidOperationException(
            "El porcentaje de descuento no puede superar el 100%.");
    }


    /*
     * Si es monto fijo y tenemos una tarifa vigente,
     * tampoco permitimos que el descuento sea mayor
     * al precio mensual.
     */
    var tarifaActual =
        await db.TarifasPlan
            .AsNoTracking()
            .Where(x =>
                x.PlanId ==
                    plan.Id
                &&
                x.VigenciaDesde <=
                    dto.FechaInicioInscripcion
                &&
                (
                    !x.VigenciaHasta.HasValue
                    ||
                    x.VigenciaHasta.Value >=
                        dto.FechaInicioInscripcion
                ))
            .OrderByDescending(x =>
                x.VigenciaDesde)
            .FirstOrDefaultAsync(ct);


    if (
        dto.TipoDescuento ==
            TipoDescuento.MontoFijo
        &&
        tarifaActual is not null
        &&
        dto.ValorDescuento.Value >
            tarifaActual.PrecioMensual)
    {
        throw new InvalidOperationException(
            "El descuento no puede ser mayor que la tarifa mensual.");
    }
}
else
{
    /*
     * Si no hay descuento, limpiamos cualquier
     * dato residual que pudiera llegar del frontend.
     */
    dto.ValorDescuento =
        null;

    dto.MotivoDescuento =
        null;
}
    // =========================================================
    // TURNOS SELECCIONADOS
    // =========================================================

    if (dto.TurnosIds.Count !=
        plan.VecesPorSemana)
    {
        throw new InvalidOperationException(
            $"El plan {plan.Nombre} requiere seleccionar " +
            $"{plan.VecesPorSemana} " +
            $"{(plan.VecesPorSemana == 1 ? "turno semanal" : "turnos semanales")}.");
    }


    if (
        dto.TurnosIds
            .Distinct()
            .Count()
        !=
        dto.TurnosIds.Count)
    {
        throw new InvalidOperationException(
            "No puede seleccionar el mismo turno más de una vez.");
    }


    // =========================================================
    // BUSCAR TURNOS
    //
    // Además de activos:
    // - deben pertenecer al nivel seleccionado
    // - deben estar vigentes en la fecha de inicio
    // =========================================================

    var turnos =
        await db.Turnos
            .Include(x => x.Nivel)
            .Where(x =>
                dto.TurnosIds.Contains(x.Id)
                &&
                x.Estado ==
                    EstadoTurno.Activo
                &&
                x.NivelId ==
                    dto.NivelActualId.Value
                &&
                x.VigenteDesde <=
                    dto.FechaInicioInscripcion
                &&
                (
                    !x.VigenteHasta.HasValue
                    ||
                    x.VigenteHasta.Value >=
                        dto.FechaInicioInscripcion
                ))
            .ToListAsync(ct);


    if (turnos.Count !=
        dto.TurnosIds.Count)
    {
        throw new InvalidOperationException(
            "Uno o más horarios seleccionados no corresponden al nivel, " +
            "no están activos o no se encuentran vigentes para la fecha de inicio.");
    }


    // =========================================================
    // UN TURNO POR DÍA
    // =========================================================

    var diasDuplicados =
        turnos
            .GroupBy(x =>
                x.DiaSemana)
            .Where(x =>
                x.Count() > 1)
            .Select(x =>
                x.Key)
            .ToList();


    if (diasDuplicados.Count > 0)
    {
        throw new InvalidOperationException(
            $"No puede seleccionar más de un horario el mismo día. " +
            $"Revise: {string.Join(", ", diasDuplicados)}.");
    }


    // =========================================================
    // VALIDAR CUPOS
    //
    // Una inscripción nueva sin FechaFin se superpone con
    // cualquier inscripción activa que no haya terminado
    // antes de nuestra fecha de inicio.
    // =========================================================

    var turnosIds =
        turnos
            .Select(x => x.Id)
            .ToList();


    var ocupacion =
        await db.InscripcionesTurnos
            .AsNoTracking()
            .Where(x =>
                turnosIds.Contains(
                    x.TurnoId)
                &&
                x.Inscripcion.Estado ==
                    EstadoInscripcion.Activa
                &&
                (
                    !x.Inscripcion.FechaFin.HasValue
                    ||
                    x.Inscripcion.FechaFin.Value >=
                        dto.FechaInicioInscripcion
                ))
            .GroupBy(x =>
                x.TurnoId)
            .Select(x =>
                new
                {
                    TurnoId =
                        x.Key,

                    Ocupados =
                        x.Count()
                })
            .ToDictionaryAsync(
                x => x.TurnoId,
                x => x.Ocupados,
                ct);


    foreach (var turno in turnos)
    {
        var ocupados =
            ocupacion.TryGetValue(
                turno.Id,
                out var cantidad)
                ? cantidad
                : 0;


        var disponibles =
            turno.Cupo -
            ocupados;


        if (disponibles <= 0)
        {
            throw new InvalidOperationException(
                $"El turno {turno.DiaSemana} " +
                $"{turno.HoraInicio:HH\\:mm} - " +
                $"{turno.HoraFin:HH\\:mm} ya no tiene cupos disponibles.");
        }
    }


    // =========================================================
    // CREAR BEBÉ
    // =========================================================

    var bebe =
        new Bebe
        {
            Id =
                Guid.NewGuid(),

            Nombre =
                NormalizarRequerido(
                    dto.Nombre),

            Apellido =
                NormalizarRequerido(
                    dto.Apellido),

            FechaNacimiento =
                dto.FechaNacimiento,

            Sexo =
                dto.Sexo,

            Dni =
                Normalizar(
                    dto.Dni),

            FechaAlta =
                hoy,

            Estado =
                EstadoBebe.Activo,

            NivelActualId =
                dto.NivelActualId
        };


    db.Bebes.Add(
        bebe);


    // =========================================================
    // RESPONSABLES
    // =========================================================

    await VincularResponsablesAsync(
        db,
        bebe,
        dto.Responsables,
        ct);


    // =========================================================
    // CREAR INSCRIPCIÓN
    // =========================================================

   var inscripcion =
    new Inscripcion
    {
        Id =
            Guid.NewGuid(),

        Bebe =
            bebe,

        BebeId =
            bebe.Id,

        Plan =
            plan,

        PlanId =
            plan.Id,

        FechaInicio =
            dto.FechaInicioInscripcion,

        FechaFin =
            null,

        Estado =
            EstadoInscripcion.Activa,

        MedioPagoPreferido =
            dto.MedioPagoPreferido,


        // =====================================================
        // DESCUENTO MANUAL
        // =====================================================

        TipoDescuento =
            dto.TipoDescuento,

        ValorDescuento =
            dto.ValorDescuento,

        MotivoDescuento =
            string.IsNullOrWhiteSpace(
                dto.MotivoDescuento)
                ? null
                : dto.MotivoDescuento.Trim()
    };


    // =========================================================
    // VINCULAR TURNOS
    // =========================================================

    foreach (var turno in
        turnos
            .OrderBy(x =>
                x.DiaSemana)
            .ThenBy(x =>
                x.HoraInicio))
    {
        inscripcion.Turnos.Add(
            new InscripcionTurno
            {
                Inscripcion =
                    inscripcion,

                InscripcionId =
                    inscripcion.Id,

                Turno =
                    turno,

                TurnoId =
                    turno.Id,

                /*
                 * En una inscripción todos estos turnos
                 * forman parte de su asistencia habitual.
                 */
                EsPrincipal =
                    true
            });
    }


    db.Inscripciones.Add(
        inscripcion);


    // =========================================================
    // GUARDAR TODO JUNTO
    // =========================================================

    await db.SaveChangesAsync(
        ct);


    await transaction.CommitAsync(
        ct);


    return bebe.Id;
}


    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public async Task ActualizarAsync(
    Guid id,
    GuardarBebeDto dto,
    CancellationToken ct = default)
{
    ValidarDto(dto);


    await using var db =
        await factory.CreateDbContextAsync(ct);


    await using var transaction =
        await db.Database
            .BeginTransactionAsync(ct);


    var bebe =
        await db.Bebes
            .Include(x => x.Responsables)
                .ThenInclude(x => x.Responsable)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                ct)
        ?? throw new InvalidOperationException(
            "El bebé indicado no existe.");


    await ValidarNivelAsync(
        db,
        dto.NivelActualId,
        ct);


    await ValidarDniBebeAsync(
        db,
        dto.Dni,
        id,
        ct);


    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    if (dto.FechaNacimiento > hoy)
    {
        throw new InvalidOperationException(
            "La fecha de nacimiento no puede ser futura.");
    }


    bebe.Nombre =
        NormalizarRequerido(
            dto.Nombre);

    bebe.Apellido =
        NormalizarRequerido(
            dto.Apellido);

    bebe.FechaNacimiento =
        dto.FechaNacimiento;

    bebe.Sexo =
        dto.Sexo;

    bebe.Dni =
        Normalizar(dto.Dni);

    bebe.NivelActualId =
        dto.NivelActualId;


    // =========================================================
    // SINCRONIZAR RESPONSABLES
    //
    // Ya NO eliminamos todos para volver a insertarlos.
    // Actualizamos, agregamos o quitamos según corresponda.
    // =========================================================

    await SincronizarResponsablesAsync(
        db,
        bebe,
        dto.Responsables,
        ct);


    await db.SaveChangesAsync(ct);

    await transaction.CommitAsync(ct);
}

private static async Task SincronizarResponsablesAsync(
    AppDbContext db,
    Bebe bebe,
    IReadOnlyCollection<GuardarResponsableBebeDto> responsables,
    CancellationToken ct)
{
    if (responsables.Count == 0)
    {
        throw new InvalidOperationException(
            "Debe indicar al menos un responsable.");
    }


    var principales =
        responsables.Count(x =>
            x.EsContactoPrincipal);


    if (principales != 1)
    {
        throw new InvalidOperationException(
            "Debe existir exactamente un contacto principal.");
    }


    /*
     * Sacamos una fotografía de las relaciones que
     * actualmente tiene el bebé.
     *
     * Es importante hacer ToList() porque la colección
     * puede modificarse durante la sincronización.
     */
    var relacionesActuales =
        bebe.Responsables
            .ToList();


    /*
     * Sirve para:
     *
     * - detectar responsables duplicados en el DTO
     * - saber qué relaciones debemos conservar
     * - detectar cuáles fueron quitadas
     */
    var responsablesProcesados =
        new HashSet<Guid>();


    foreach (var dto in responsables)
    {
        ValidarResponsable(dto);


        Responsable responsable;

        BebeResponsable? relacionExistente =
            null;


        // =====================================================
        // RESPONSABLE YA EXISTENTE
        // =====================================================

        if (dto.ResponsableId.HasValue)
        {
            /*
             * Primero intentamos obtenerlo desde la relación
             * que ya está cargada en memoria.
             */
            relacionExistente =
                relacionesActuales
                    .FirstOrDefault(x =>
                        x.ResponsableId ==
                        dto.ResponsableId.Value);


            if (relacionExistente is not null)
            {
                responsable =
                    relacionExistente.Responsable;
            }
            else
            {
                /*
                 * También podría ser un responsable existente
                 * en el sistema pero que todavía no estaba
                 * vinculado a este bebé.
                 */
                responsable =
                    await db.Responsables
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id ==
                                dto.ResponsableId.Value,
                            ct)
                    ?? throw new InvalidOperationException(
                        "Uno de los responsables indicados no existe.");
            }


            ActualizarResponsable(
                responsable,
                dto);
        }

        // =====================================================
        // RESPONSABLE NUEVO
        // =====================================================

        else
        {
            /*
             * Intentamos reutilizar un responsable existente
             * por DNI/email antes de crear otro registro.
             */
            responsable =
                await BuscarResponsableExistenteAsync(
                    db,
                    dto,
                    ct)
                ??
                CrearResponsable(
                    dto);


            /*
             * Si BuscarResponsableExistenteAsync encontró
             * alguien que ya estaba relacionado con el bebé,
             * recuperamos esa relación.
             */
            relacionExistente =
                relacionesActuales
                    .FirstOrDefault(x =>
                        x.ResponsableId ==
                        responsable.Id);


            /*
             * Si acaba de crearse, todavía está Detached.
             */
            if (db.Entry(responsable).State ==
                EntityState.Detached)
            {
                db.Responsables.Add(
                    responsable);
            }
        }


        // =====================================================
        // DETECTAR DUPLICADOS REALES
        // =====================================================

        if (!responsablesProcesados.Add(
            responsable.Id))
        {
            throw new InvalidOperationException(
                $"El responsable {responsable.Nombre} {responsable.Apellido} está repetido.");
        }


        // =====================================================
        // ACTUALIZAR RELACIÓN EXISTENTE
        // =====================================================

        if (relacionExistente is not null)
        {
            relacionExistente.Relacion =
                dto.Relacion;

            relacionExistente.EsContactoPrincipal =
                dto.EsContactoPrincipal;

            relacionExistente.AutorizaComunicaciones =
                dto.AutorizaComunicaciones;
        }

        // =====================================================
        // AGREGAR NUEVA RELACIÓN
        // =====================================================

        else
        {
            bebe.Responsables.Add(
                new BebeResponsable
                {
                    Bebe =
                        bebe,

                    BebeId =
                        bebe.Id,

                    Responsable =
                        responsable,

                    ResponsableId =
                        responsable.Id,

                    Relacion =
                        dto.Relacion,

                    EsContactoPrincipal =
                        dto.EsContactoPrincipal,

                    AutorizaComunicaciones =
                        dto.AutorizaComunicaciones
                });
        }
    }


    // =========================================================
    // ELIMINAR RESPONSABLES QUE EL USUARIO QUITÓ DEL BEBÉ
    // =========================================================

    var relacionesAEliminar =
        relacionesActuales
            .Where(x =>
                !responsablesProcesados.Contains(
                    x.ResponsableId))
            .ToList();


    if (relacionesAEliminar.Count > 0)
    {
        db.BebesResponsables.RemoveRange(
            relacionesAEliminar);
    }
}

    // =========================================================
    // DAR DE BAJA
    // =========================================================

    public async Task DarDeBajaAsync(
        Guid id,
        DarDeBajaBebeDto dto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Motivo))
        {
            throw new InvalidOperationException(
                "Debe indicar el motivo de la baja.");
        }


        if (dto.Motivo.Trim().Length > 300)
        {
            throw new InvalidOperationException(
                "El motivo de baja no puede superar los 300 caracteres.");
        }


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var bebe =
            await db.Bebes
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El bebé indicado no existe.");


        if (bebe.Estado ==
            EstadoBebe.Baja)
        {
            return;
        }


        bebe.DarDeBaja(
            dto.Fecha,
            dto.Motivo.Trim());


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // REACTIVAR
    // =========================================================

    public async Task ReactivarAsync(
        Guid id,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var bebe =
            await db.Bebes
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El bebé indicado no existe.");


        if (bebe.Estado ==
            EstadoBebe.Activo)
        {
            return;
        }


        bebe.Reactivar();


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // CUMPLEAÑOS
    // =========================================================

    public async Task<IReadOnlyList<CumpleaniosBebeDto>>
        ObtenerCumpleaniosMesAsync(
            int anio,
            int mes,
            CancellationToken ct = default)
    {
        if (mes is < 1 or > 12)
        {
            throw new InvalidOperationException(
                "El mes indicado no es válido.");
        }


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        var bebes =
            await db.Bebes
                .AsNoTracking()
                .Where(x =>
                    x.Estado ==
                        EstadoBebe.Activo
                    &&
                    x.FechaNacimiento.Month ==
                        mes)
                .OrderBy(x =>
                    x.FechaNacimiento.Day)
                .ThenBy(x =>
                    x.Apellido)
                .ThenBy(x =>
                    x.Nombre)
                .Select(x =>
                    new
                    {
                        x.Id,
                        x.Nombre,
                        x.Apellido,
                        x.FechaNacimiento
                    })
                .ToListAsync(ct);


        return bebes
            .Select(x =>
            {
                var fechaCumple =
                    new DateOnly(
                        anio,
                        mes,
                        x.FechaNacimiento.Day);


                var edadQueCumple =
                    anio -
                    x.FechaNacimiento.Year;


                var diasFaltantes =
                    fechaCumple.DayNumber -
                    hoy.DayNumber;


                return new CumpleaniosBebeDto(
                    x.Id,
                    x.Nombre,
                    x.Apellido,

                    $"{x.Nombre} {x.Apellido}"
                        .Trim(),

                    x.FechaNacimiento,

                    edadQueCumple,

                    fechaCumple ==
                        hoy,

                    diasFaltantes);
            })
            .OrderBy(x =>
                x.FechaNacimiento.Day)
            .ThenBy(x =>
                x.Apellido)
            .ThenBy(x =>
                x.Nombre)
            .ToList();
    }


    // =========================================================
    // RESPONSABLES
    // =========================================================

    private static async Task VincularResponsablesAsync(
        AppDbContext db,
        Bebe bebe,
        IReadOnlyCollection<GuardarResponsableBebeDto> responsables,
        CancellationToken ct)
    {
        if (responsables.Count == 0)
        {
            throw new InvalidOperationException(
                "Debe indicar al menos un responsable.");
        }


        var principales =
            responsables.Count(x =>
                x.EsContactoPrincipal);


        if (principales != 1)
        {
            throw new InvalidOperationException(
                "Debe existir exactamente un contacto principal.");
        }


        foreach (var dto in responsables)
        {
            Responsable responsable;


            if (dto.ResponsableId.HasValue)
            {
                responsable =
                    await db.Responsables
                        .FirstOrDefaultAsync(
                            x =>
                                x.Id ==
                                dto.ResponsableId.Value,
                            ct)
                    ?? throw new InvalidOperationException(
                        "Uno de los responsables indicados no existe.");


                if (!responsable.Activo)
                {
                    responsable.Activo =
                        true;
                }


                ActualizarResponsable(
                    responsable,
                    dto);
            }
            else
            {
                responsable =
                    await BuscarResponsableExistenteAsync(
                        db,
                        dto,
                        ct)
                    ??
                    CrearResponsable(
                        dto);


                if (db.Entry(responsable).State ==
                    EntityState.Detached)
                {
                    db.Responsables.Add(
                        responsable);
                }
            }


            var repetido =
                bebe.Responsables.Any(x =>
                    x.ResponsableId ==
                    responsable.Id);


            if (repetido)
            {
                throw new InvalidOperationException(
                    $"El responsable {responsable.Nombre} {responsable.Apellido} está repetido.");
            }


            bebe.Responsables.Add(
                new BebeResponsable
                {
                    Bebe =
                        bebe,

                    BebeId =
                        bebe.Id,

                    Responsable =
                        responsable,

                    ResponsableId =
                        responsable.Id,

                    Relacion =
                        dto.Relacion,

                    EsContactoPrincipal =
                        dto.EsContactoPrincipal,

                    AutorizaComunicaciones =
                        dto.AutorizaComunicaciones
                });
        }
    }


   private static async Task<Responsable?>
    BuscarResponsableExistenteAsync(
        AppDbContext db,
        GuardarResponsableBebeDto dto,
        CancellationToken ct)
{
    var dni =
        Normalizar(dto.Dni);


    if (!string.IsNullOrWhiteSpace(
        dni))
    {
        var porDni =
            await db.Responsables
                .FirstOrDefaultAsync(
                    x =>
                        x.Dni ==
                        dni,
                    ct);


        if (porDni is not null)
        {
            ValidarQueSeaMismaPersona(
                porDni,
                dto,
                "DNI");


            ActualizarResponsable(
                porDni,
                dto);


            return porDni;
        }
    }


    var email =
        Normalizar(dto.Email);


    if (!string.IsNullOrWhiteSpace(
        email))
    {
        var emailLower =
            email.ToLower();


        var porEmail =
            await db.Responsables
                .FirstOrDefaultAsync(
                    x =>
                        x.Email.ToLower() ==
                        emailLower,
                    ct);


        if (porEmail is not null)
        {
            ValidarQueSeaMismaPersona(
                porEmail,
                dto,
                "email");


            ActualizarResponsable(
                porEmail,
                dto);


            return porEmail;
        }
    }


    return null;
}

private static void ValidarQueSeaMismaPersona(
    Responsable existente,
    GuardarResponsableBebeDto dto,
    string campoCoincidente)
{
    var mismoNombre =
        string.Equals(
            existente.Nombre.Trim(),
            dto.Nombre.Trim(),
            StringComparison.OrdinalIgnoreCase);


    var mismoApellido =
        string.Equals(
            existente.Apellido.Trim(),
            dto.Apellido.Trim(),
            StringComparison.OrdinalIgnoreCase);


    if (mismoNombre &&
        mismoApellido)
    {
        return;
    }


    throw new InvalidOperationException(
        $"El {campoCoincidente} ingresado ya pertenece a " +
        $"{existente.Nombre} {existente.Apellido}. " +
        $"Revise los datos antes de continuar.");
}


    private static Responsable CrearResponsable(
        GuardarResponsableBebeDto dto)
    {
        ValidarResponsable(dto);


        return new Responsable
        {
            Id =
                Guid.NewGuid(),

            Nombre =
                NormalizarRequerido(
                    dto.Nombre),

            Apellido =
                NormalizarRequerido(
                    dto.Apellido),

            Dni =
                Normalizar(dto.Dni),

            Telefono =
                Normalizar(dto.Telefono)
                ?? string.Empty,

            WhatsApp =
                Normalizar(dto.WhatsApp)
                ?? string.Empty,

            Email =
                Normalizar(dto.Email)
                ?? string.Empty,

            Domicilio =
                Normalizar(dto.Domicilio),

            Activo =
                true
        };
    }


    private static void ActualizarResponsable(
        Responsable responsable,
        GuardarResponsableBebeDto dto)
    {
        ValidarResponsable(dto);


        responsable.Nombre =
            NormalizarRequerido(
                dto.Nombre);

        responsable.Apellido =
            NormalizarRequerido(
                dto.Apellido);

        responsable.Dni =
            Normalizar(dto.Dni);

        responsable.Telefono =
            Normalizar(dto.Telefono)
            ?? string.Empty;

        responsable.WhatsApp =
            Normalizar(dto.WhatsApp)
            ?? string.Empty;

        responsable.Email =
            Normalizar(dto.Email)
            ?? string.Empty;

        responsable.Domicilio =
            Normalizar(dto.Domicilio);

        responsable.Activo =
            true;
    }


    // =========================================================
    // NIVEL
    // =========================================================

    private static async Task ValidarNivelAsync(
        AppDbContext db,
        Guid? nivelId,
        CancellationToken ct)
    {
        if (!nivelId.HasValue)
            return;


        var existe =
            await db.Niveles
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id ==
                        nivelId.Value
                    &&
                        x.Activo,
                    ct);


        if (!existe)
        {
            throw new InvalidOperationException(
                "El nivel seleccionado no existe o se encuentra inactivo.");
        }
    }


    // =========================================================
    // DNI BEBÉ
    // =========================================================

    private static async Task ValidarDniBebeAsync(
        AppDbContext db,
        string? dni,
        Guid? bebeId,
        CancellationToken ct)
    {
        var normalizado =
            Normalizar(dni);


        if (string.IsNullOrWhiteSpace(
            normalizado))
        {
            return;
        }


        var existe =
            await db.Bebes
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Dni ==
                            normalizado
                    &&
                        (
                            !bebeId.HasValue
                            ||
                            x.Id != bebeId.Value
                        ),
                    ct);


        if (existe)
        {
            throw new InvalidOperationException(
                "Ya existe un bebé registrado con ese DNI.");
        }
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void ValidarDto(
        GuardarBebeDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre del bebé.");
        }


        if (string.IsNullOrWhiteSpace(
            dto.Apellido))
        {
            throw new InvalidOperationException(
                "Debe indicar el apellido del bebé.");
        }


        if (dto.Nombre.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El nombre no puede superar los 100 caracteres.");
        }


        if (dto.Apellido.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El apellido no puede superar los 100 caracteres.");
        }


        if (
            dto.Dni is not null
            &&
            dto.Dni.Trim().Length > 20)
        {
            throw new InvalidOperationException(
                "El DNI no puede superar los 20 caracteres.");
        }


        if (
            dto.Responsables.Count > 0
            &&
            dto.Responsables.Count(x =>
                x.EsContactoPrincipal) != 1)
        {
            throw new InvalidOperationException(
                "Debe existir exactamente un contacto principal.");
        }
    }


    private static void ValidarResponsable(
        GuardarResponsableBebeDto dto)
    {
        if (string.IsNullOrWhiteSpace(
            dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre del responsable.");
        }


        if (string.IsNullOrWhiteSpace(
            dto.Apellido))
        {
            throw new InvalidOperationException(
                "Debe indicar el apellido del responsable.");
        }


        if (dto.Nombre.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El nombre del responsable no puede superar los 100 caracteres.");
        }


        if (dto.Apellido.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El apellido del responsable no puede superar los 100 caracteres.");
        }


        if (
            dto.Telefono?.Length > 50
            ||
            dto.WhatsApp?.Length > 50)
        {
            throw new InvalidOperationException(
                "El teléfono o WhatsApp supera la longitud permitida.");
        }


        if (dto.Email?.Length > 200)
        {
            throw new InvalidOperationException(
                "El email supera la longitud permitida.");
        }


        if (dto.Domicilio?.Length > 300)
        {
            throw new InvalidOperationException(
                "El domicilio supera la longitud permitida.");
        }
    }


    // =========================================================
    // MAPPING
    // =========================================================

    private static BebeDto MapearBebe(
        Bebe bebe,
        DateOnly hoy)
    {
        var edadMeses =
            bebe.EdadEnMeses(
                hoy);


        var edadAnios =
            edadMeses / 12;


        var responsables =
            bebe.Responsables
                .OrderByDescending(x =>
                    x.EsContactoPrincipal)
                .ThenBy(x =>
                    x.Responsable.Apellido)
                .ThenBy(x =>
                    x.Responsable.Nombre)
                .Select(x =>
                    new ResponsableBebeDto(
                        x.Responsable.Id,
                        x.Responsable.Nombre,
                        x.Responsable.Apellido,

                        $"{x.Responsable.Nombre} {x.Responsable.Apellido}"
                            .Trim(),

                        x.Responsable.Dni,
                        x.Responsable.Telefono,
                        x.Responsable.WhatsApp,
                        x.Responsable.Email,
                        x.Responsable.Domicilio,
                        x.Relacion,
                        x.EsContactoPrincipal,
                        x.AutorizaComunicaciones))
                .ToList();


        return new BebeDto(
            bebe.Id,
            bebe.Nombre,
            bebe.Apellido,

            $"{bebe.Nombre} {bebe.Apellido}"
                .Trim(),

            bebe.FechaNacimiento,
            bebe.Sexo,
            bebe.Dni,
            bebe.FechaAlta,
            bebe.Estado,
            bebe.FechaBaja,
            bebe.MotivoBaja,
            bebe.NivelActualId,
            bebe.NivelActual?.Nombre,
            edadMeses,
            edadAnios,
            bebe.AlcanzoEdadMaxima(hoy),
            responsables);
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private static string NormalizarRequerido(
        string texto)
    {
        return texto.Trim();
    }


    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(
            texto)
                ? null
                : texto.Trim();
    }

    private static int CalcularEdadMeses(
    DateOnly nacimiento,
    DateOnly fecha)
{
    var meses =
        (fecha.Year - nacimiento.Year) * 12
        +
        fecha.Month -
        nacimiento.Month;


    if (fecha.Day <
        nacimiento.Day)
    {
        meses--;
    }


    return Math.Max(
        0,
        meses);
}
}