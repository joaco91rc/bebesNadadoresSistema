using System.Data;
using BebesNadadores.Application.Inscripciones;
using BebesNadadores.Application.Inscripciones.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Inscripciones;

public sealed class InscripcionService(
    IDbContextFactory<AppDbContext> factory)
    : IInscripcionService
{
    public async Task CambiarAsync(
        Guid bebeId,
        CambiarInscripcionDto dto,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        // =====================================================
        // POR AHORA LOS CAMBIOS SE HACEN EFECTIVOS HOY
        //
        // Más adelante podemos agregar inscripciones programadas
        // para permitir cambios futuros.
        // =====================================================

        if (dto.AplicarDesde != hoy)
        {
            throw new InvalidOperationException(
                "Por el momento los cambios de inscripción deben aplicarse con fecha de hoy.");
        }


        // =====================================================
        // BEBÉ
        // =====================================================

        var bebe =
            await db.Bebes
                .FirstOrDefaultAsync(
                    x => x.Id == bebeId,
                    ct)
            ?? throw new InvalidOperationException(
                "El bebé indicado no existe.");


        if (bebe.Estado !=
            EstadoBebe.Activo)
        {
            throw new InvalidOperationException(
                "No puede modificar la inscripción de un bebé dado de baja.");
        }


        // =====================================================
        // INSCRIPCIÓN ACTUAL
        // =====================================================

        var actual =
            await db.Inscripciones
                .Include(x => x.Turnos)
                .Where(x =>
                    x.BebeId == bebeId
                    &&
                    x.Estado ==
                        EstadoInscripcion.Activa)
                .OrderByDescending(x =>
                    x.FechaInicio)
                .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "El bebé no posee una inscripción activa.");


        // =====================================================
        // NIVEL
        // =====================================================

        var nivel =
            await db.Niveles
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            dto.NivelId
                        &&
                        x.Activo,
                    ct)
            ?? throw new InvalidOperationException(
                "El nivel seleccionado no existe o está inactivo.");


        // =====================================================
        // PLAN
        // =====================================================

        var plan =
            await db.Planes
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            dto.PlanId
                        &&
                        x.Activo,
                    ct)
            ?? throw new InvalidOperationException(
                "El plan seleccionado no existe o está inactivo.");


        // =====================================================
        // CANTIDAD TURNOS SEGÚN PLAN
        // =====================================================

        if (dto.TurnosIds.Count !=
            plan.VecesPorSemana)
        {
            throw new InvalidOperationException(
                $"El plan {plan.Nombre} requiere " +
                $"{plan.VecesPorSemana} turno(s) semanal(es).");
        }


        if (
            dto.TurnosIds
                .Distinct()
                .Count()
            != dto.TurnosIds.Count)
        {
            throw new InvalidOperationException(
                "No puede seleccionar el mismo turno más de una vez.");
        }


        // =====================================================
        // TURNOS
        // =====================================================

        var turnos =
            await db.Turnos
                .Where(x =>
                    dto.TurnosIds.Contains(x.Id)
                    &&
                    x.NivelId ==
                        nivel.Id
                    &&
                    x.Estado ==
                        EstadoTurno.Activo
                    &&
                    x.VigenteDesde <=
                        dto.AplicarDesde
                    &&
                    (
                        !x.VigenteHasta.HasValue
                        ||
                        x.VigenteHasta.Value >=
                            dto.AplicarDesde
                    ))
                .ToListAsync(ct);


        if (turnos.Count !=
            dto.TurnosIds.Count)
        {
            throw new InvalidOperationException(
                "Uno o más horarios no corresponden al nivel seleccionado " +
                "o no están vigentes.");
        }


        // =====================================================
        // UN TURNO POR DÍA
        // =====================================================

        if (
            turnos
                .GroupBy(x =>
                    x.DiaSemana)
                .Any(x =>
                    x.Count() > 1))
        {
            throw new InvalidOperationException(
                "No puede seleccionar más de un horario el mismo día.");
        }


        // =====================================================
        // CUPO
        //
        // Excluimos la inscripción que estamos modificando,
        // porque actualmente ya ocupa un lugar.
        // =====================================================

        var turnoIds =
            turnos
                .Select(x => x.Id)
                .ToList();


        var ocupacion =
            await db.InscripcionesTurnos
                .AsNoTracking()
                .Where(x =>
                    turnoIds.Contains(
                        x.TurnoId)
                    &&
                    x.InscripcionId !=
                        actual.Id
                    &&
                    x.Inscripcion.Estado ==
                        EstadoInscripcion.Activa
                    &&
                    (
                        !x.Inscripcion.FechaFin.HasValue
                        ||
                        x.Inscripcion.FechaFin.Value >=
                            dto.AplicarDesde
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


            if (ocupados >=
                turno.Cupo)
            {
                throw new InvalidOperationException(
                    $"El turno {turno.DiaSemana} " +
                    $"{turno.HoraInicio:HH\\:mm} ya no tiene cupo.");
            }
        }


        // =====================================================
        // DESCUENTO
        // =====================================================

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


            var tarifa =
                await db.TarifasPlan
                    .AsNoTracking()
                    .Where(x =>
                        x.PlanId ==
                            plan.Id
                        &&
                        x.VigenciaDesde <=
                            dto.AplicarDesde
                        &&
                        (
                            !x.VigenciaHasta.HasValue
                            ||
                            x.VigenciaHasta.Value >=
                                dto.AplicarDesde
                        ))
                    .OrderByDescending(x =>
                        x.VigenciaDesde)
                    .FirstOrDefaultAsync(ct);


            if (
                dto.TipoDescuento ==
                    TipoDescuento.MontoFijo
                &&
                tarifa is not null
                &&
                dto.ValorDescuento.Value >
                    tarifa.PrecioMensual)
            {
                throw new InvalidOperationException(
                    "El descuento no puede superar la tarifa mensual.");
            }
        }
        else
        {
            dto.ValorDescuento =
                null;

            dto.MotivoDescuento =
                null;
        }


        // =====================================================
        // SI LA INSCRIPCIÓN COMENZÓ HOY
        //
        // Todavía no existe un período anterior para preservar.
        // Modificamos la inscripción directamente.
        // =====================================================

       if (actual.FechaInicio ==
    dto.AplicarDesde)
{
    actual.PlanId =
        plan.Id;

    actual.MedioPagoPreferido =
        dto.MedioPagoPreferido;

    actual.TipoDescuento =
        dto.TipoDescuento;

    actual.ValorDescuento =
        dto.ValorDescuento;

    actual.MotivoDescuento =
        Normalizar(
            dto.MotivoDescuento);


    // =====================================================
    // SINCRONIZAR TURNOS
    // =====================================================

    var nuevosIds =
        turnos
            .Select(x =>
                x.Id)
            .ToHashSet();


    var relacionesActuales =
        actual.Turnos
            .ToList();


    // -----------------------------------------------------
    // Quitar solamente los que dejaron de estar seleccionados
    // -----------------------------------------------------

    var eliminar =
        relacionesActuales
            .Where(x =>
                !nuevosIds.Contains(
                    x.TurnoId))
            .ToList();


    if (eliminar.Count > 0)
    {
        db.InscripcionesTurnos
            .RemoveRange(
                eliminar);
    }


    // -----------------------------------------------------
    // Agregar solamente los nuevos
    // -----------------------------------------------------

    var actualesIds =
        relacionesActuales
            .Select(x =>
                x.TurnoId)
            .ToHashSet();


    foreach (var turno in turnos)
    {
        if (actualesIds.Contains(
            turno.Id))
        {
            continue;
        }


        actual.Turnos.Add(
            new InscripcionTurno
            {
                InscripcionId =
                    actual.Id,

                TurnoId =
                    turno.Id,

                EsPrincipal =
                    true
            });
    }


    bebe.NivelActualId =
        nivel.Id;


    await db.SaveChangesAsync(ct);

    await transaction.CommitAsync(ct);

    return;
}


        // =====================================================
        // CERRAR INSCRIPCIÓN ANTERIOR
        // =====================================================

        actual.FechaFin =
            dto.AplicarDesde.AddDays(-1);

        actual.Estado =
            EstadoInscripcion.Finalizada;


        // =====================================================
        // NUEVA INSCRIPCIÓN
        // =====================================================

        var nueva =
            new Inscripcion
            {
                Id =
                    Guid.NewGuid(),

                BebeId =
                    bebe.Id,

                PlanId =
                    plan.Id,

                FechaInicio =
                    dto.AplicarDesde,

                FechaFin =
                    null,

                Estado =
                    EstadoInscripcion.Activa,

                MedioPagoPreferido =
                    dto.MedioPagoPreferido,

                TipoDescuento =
                    dto.TipoDescuento,

                ValorDescuento =
                    dto.ValorDescuento,

                MotivoDescuento =
                    Normalizar(
                        dto.MotivoDescuento)
            };


        foreach (var turno in turnos)
        {
            nueva.Turnos.Add(
                new InscripcionTurno
                {
                    InscripcionId =
                        nueva.Id,

                    TurnoId =
                        turno.Id,

                    EsPrincipal =
                        true
                });
        }


        db.Inscripciones.Add(
            nueva);


        bebe.NivelActualId =
            nivel.Id;


        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }


    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(
            texto)
            ? null
            : texto.Trim();
    }

    public async Task CrearPrimeraAsync(
    Guid bebeId,
    CambiarInscripcionDto dto,
    CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    await using var transaction =
        await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct);


    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    // =========================================================
    // POR AHORA LOS CAMBIOS SE APLICAN DESDE HOY
    // =========================================================

    if (dto.AplicarDesde != hoy)
    {
        throw new InvalidOperationException(
            "Por el momento la inscripción debe comenzar en la fecha actual.");
    }


    // =========================================================
    // BEBÉ
    // =========================================================

    var bebe =
        await db.Bebes
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        bebeId,
                ct)
        ?? throw new InvalidOperationException(
            "El bebé indicado no existe.");


    if (bebe.Estado !=
        EstadoBebe.Activo)
    {
        throw new InvalidOperationException(
            "El bebé no se encuentra activo.");
    }


    // =========================================================
    // VERIFICAR QUE REALMENTE NO TENGA INSCRIPCIÓN ACTUAL
    // =========================================================

    var existeInscripcionActual =
        await db.Inscripciones
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.BebeId ==
                        bebeId

                    &&
                    x.Estado ==
                        EstadoInscripcion.Activa

                    &&
                    x.FechaInicio <=
                        dto.AplicarDesde

                    &&
                    (
                        !x.FechaFin.HasValue
                        ||
                        x.FechaFin.Value >=
                            dto.AplicarDesde
                    ),
                ct);


    if (existeInscripcionActual)
    {
        throw new InvalidOperationException(
            "El bebé ya posee una inscripción activa. Utilice la opción de modificar inscripción.");
    }


    // =========================================================
    // NIVEL
    // =========================================================

    if (dto.NivelId ==
        Guid.Empty)
    {
        throw new InvalidOperationException(
            "Debe seleccionar un nivel.");
    }


    var nivel =
        await db.Niveles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        dto.NivelId
                    &&
                    x.Activo,
                ct)
        ?? throw new InvalidOperationException(
            "El nivel seleccionado no existe o se encuentra inactivo.");


    // =========================================================
    // VALIDAR EDAD DEL BEBÉ
    // =========================================================

    var edadMeses =
        CalcularEdadMesesInscripcion(
            bebe.FechaNacimiento,
            dto.AplicarDesde);


    if (
        edadMeses <
            nivel.EdadMinMeses
        ||
        edadMeses >
            nivel.EdadMaxMeses)
    {
        throw new InvalidOperationException(
            $"La edad del bebé no corresponde al nivel {nivel.Nombre}. " +
            $"El nivel admite entre {nivel.EdadMinMeses} y " +
            $"{nivel.EdadMaxMeses} meses.");
    }


    if (edadMeses >= 84)
    {
        throw new InvalidOperationException(
            "El bebé alcanzó la edad máxima permitida.");
    }


    // =========================================================
    // PLAN
    // =========================================================

    if (dto.PlanId ==
        Guid.Empty)
    {
        throw new InvalidOperationException(
            "Debe seleccionar un plan.");
    }


    var plan =
        await db.Planes
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        dto.PlanId
                    &&
                    x.Activo,
                ct)
        ?? throw new InvalidOperationException(
            "El plan seleccionado no existe o se encuentra inactivo.");


    // =========================================================
    // CANTIDAD DE TURNOS
    // =========================================================

    if (dto.TurnosIds.Count !=
        plan.VecesPorSemana)
    {
        throw new InvalidOperationException(
            $"El plan {plan.Nombre} requiere " +
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
    // TURNOS
    // =========================================================

    var turnos =
        await db.Turnos
            .Where(x =>
                dto.TurnosIds.Contains(
                    x.Id)

                &&
                x.Estado ==
                    EstadoTurno.Activo

                &&
                x.NivelId ==
                    dto.NivelId

                &&
                x.VigenteDesde <=
                    dto.AplicarDesde

                &&
                (
                    !x.VigenteHasta.HasValue
                    ||
                    x.VigenteHasta.Value >=
                        dto.AplicarDesde
                ))
            .OrderBy(x =>
                x.DiaSemana)
            .ThenBy(x =>
                x.HoraInicio)
            .ToListAsync(ct);


    if (turnos.Count !=
        dto.TurnosIds.Count)
    {
        throw new InvalidOperationException(
            "Uno o más horarios seleccionados no corresponden al nivel, " +
            "no están activos o no están vigentes.");
    }


    // =========================================================
    // NO PERMITIR DOS TURNOS EL MISMO DÍA
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
            "No puede seleccionar más de un horario el mismo día.");
    }


    // =========================================================
    // VALIDAR CUPOS
    // =========================================================

    var turnosIds =
        turnos
            .Select(x =>
                x.Id)
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
                x.Inscripcion.FechaInicio <=
                    dto.AplicarDesde

                &&
                (
                    !x.Inscripcion.FechaFin.HasValue
                    ||
                    x.Inscripcion.FechaFin.Value >=
                        dto.AplicarDesde
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
                x =>
                    x.TurnoId,
                x =>
                    x.Ocupados,
                ct);


    foreach (var turno in turnos)
    {
        var ocupados =
            ocupacion.TryGetValue(
                turno.Id,
                out var cantidad)
                ? cantidad
                : 0;


        if (ocupados >=
            turno.Cupo)
        {
            throw new InvalidOperationException(
                $"El turno {turno.DiaSemana} " +
                $"{turno.HoraInicio:HH\\:mm} ya no posee cupo disponible.");
        }
    }


    // =========================================================
    // DESCUENTO
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
            dto.ValorDescuento.Value >
                100)
        {
            throw new InvalidOperationException(
                "El porcentaje de descuento no puede superar el 100%.");
        }


        if (
            dto.TipoDescuento ==
                TipoDescuento.MontoFijo)
        {
            var tarifa =
                await db.TarifasPlan
                    .AsNoTracking()
                    .Where(x =>
                        x.PlanId ==
                            plan.Id

                        &&
                        x.VigenciaDesde <=
                            dto.AplicarDesde

                        &&
                        (
                            !x.VigenciaHasta.HasValue
                            ||
                            x.VigenciaHasta.Value >=
                                dto.AplicarDesde
                        ))
                    .OrderByDescending(x =>
                        x.VigenciaDesde)
                    .FirstOrDefaultAsync(ct);


            if (
                tarifa is not null
                &&
                dto.ValorDescuento.Value >
                    tarifa.PrecioMensual)
            {
                throw new InvalidOperationException(
                    "El descuento no puede ser mayor que la tarifa mensual.");
            }
        }
    }


    // =========================================================
    // CREAR INSCRIPCIÓN
    // =========================================================

    var inscripcion =
        new Inscripcion
        {
            Id =
                Guid.NewGuid(),

            BebeId =
                bebe.Id,

            Bebe =
                bebe,

            PlanId =
                plan.Id,

            FechaInicio =
                dto.AplicarDesde,

            FechaFin =
                null,

            Estado =
                EstadoInscripcion.Activa,

            MedioPagoPreferido =
                dto.MedioPagoPreferido,

            TipoDescuento =
                dto.TipoDescuento,

            ValorDescuento =
                dto.TipoDescuento.HasValue
                    ? dto.ValorDescuento
                    : null,

            MotivoDescuento =
                dto.TipoDescuento.HasValue
                    &&
                    !string.IsNullOrWhiteSpace(
                        dto.MotivoDescuento)

                    ? dto.MotivoDescuento.Trim()

                    : null
        };


    // =========================================================
    // TURNOS DE LA INSCRIPCIÓN
    // =========================================================

    foreach (var turno in turnos)
    {
        inscripcion.Turnos.Add(
            new InscripcionTurno
            {
                InscripcionId =
                    inscripcion.Id,

                TurnoId =
                    turno.Id,

                EsPrincipal =
                    true
            });
    }


    // =========================================================
    // NIVEL ACTUAL DEL BEBÉ
    // =========================================================

    bebe.NivelActualId =
        nivel.Id;


    db.Inscripciones.Add(
        inscripcion);


    await db.SaveChangesAsync(ct);


    await transaction.CommitAsync(ct);
}

private static int CalcularEdadMesesInscripcion(
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