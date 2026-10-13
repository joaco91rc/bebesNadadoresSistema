using BebesNadadores.Application.Configuracion;
using BebesNadadores.Application.Configuracion.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Configuracion;

public sealed class PlanTarifaService(
    IDbContextFactory<AppDbContext> factory)
    : IPlanTarifaService
{
    public async Task<IReadOnlyList<PlanTarifaDto>> ListarAsync(
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var hoy =
            ObtenerHoyArgentina();

        var planes =
            await db.Planes
                .AsNoTracking()
                .Include(x => x.Tarifas)
                .Include(x => x.Inscripciones)
                .OrderBy(x => x.VecesPorSemana)
                .ThenBy(x => x.Nombre)
                .ToListAsync(ct);

        return planes
            .Select(plan =>
            {
                var tarifaActual =
                    plan.Tarifas
                        .Where(x =>
                            x.VigenciaDesde <= hoy
                            &&
                            (
                                !x.VigenciaHasta.HasValue
                                ||
                                x.VigenciaHasta.Value >= hoy
                            ))
                        .OrderByDescending(x =>
                            x.VigenciaDesde)
                        .FirstOrDefault();

                var tarifas =
                    plan.Tarifas
                        .OrderByDescending(x =>
                            x.VigenciaDesde)
                        .Select(x =>
                            new TarifaPlanDto(
                                x.Id,
                                x.PrecioMensual,
                                x.VigenciaDesde,
                                x.VigenciaHasta,
                                tarifaActual?.Id == x.Id))
                        .ToList();

                return new PlanTarifaDto(
                    plan.Id,
                    plan.Nombre,
                    plan.Descripcion,
                    plan.VecesPorSemana,
                    plan.Activo,
                    tarifaActual?.PrecioMensual,
                    tarifaActual?.VigenciaDesde,
                    tarifaActual?.VigenciaHasta,
                    plan.Inscripciones.Count(x =>
                        x.Estado ==
                            EstadoInscripcion.Activa),
                    tarifas);
            })
            .ToList();
    }


    public async Task<Guid> GuardarPlanAsync(
        GuardarPlanDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        ValidarPlan(dto);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var nombre =
            dto.Nombre.Trim();

        var nombreNormalizado =
            nombre.ToLower();

        var duplicado =
            await db.Planes
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        (!dto.Id.HasValue || x.Id != dto.Id.Value)
                        &&
                        x.Nombre.ToLower() ==
                            nombreNormalizado,
                    ct);

        if (duplicado)
        {
            throw new InvalidOperationException(
                "Ya existe un plan con ese nombre.");
        }

        if (!dto.Id.HasValue)
        {
            var nuevo =
                new Plan
                {
                    Id = Guid.NewGuid(),
                    Nombre = nombre,
                    Descripcion =
                        Normalizar(dto.Descripcion),
                    VecesPorSemana =
                        dto.VecesPorSemana,
                    Activo = dto.Activo,
                    CreadoPorUsuarioId =
                        usuarioId == Guid.Empty
                            ? null
                            : usuarioId
                };

            db.Planes.Add(nuevo);

            await db.SaveChangesAsync(ct);

            return nuevo.Id;
        }

        var plan =
            await db.Planes
                .FirstOrDefaultAsync(
                    x => x.Id == dto.Id.Value,
                    ct)
            ?? throw new InvalidOperationException(
                "El plan indicado no existe.");

        if (plan.VecesPorSemana !=
            dto.VecesPorSemana)
        {
            var tieneInscripcionesActivas =
                await db.Inscripciones
                    .AsNoTracking()
                    .AnyAsync(
                        x =>
                            x.PlanId == plan.Id
                            &&
                            x.Estado ==
                                EstadoInscripcion.Activa,
                        ct);

            if (tieneInscripcionesActivas)
            {
                throw new InvalidOperationException(
                    "No se puede cambiar la cantidad de clases semanales " +
                    "de un plan que tiene inscripciones activas. " +
                    "Creá un plan nuevo y dejá este para conservar el historial.");
            }
        }

        plan.Nombre = nombre;
        plan.Descripcion =
            Normalizar(dto.Descripcion);
        plan.VecesPorSemana =
            dto.VecesPorSemana;
        plan.Activo = dto.Activo;
        plan.ModificadoUtc =
            DateTimeOffset.UtcNow;
        plan.ModificadoPorUsuarioId =
            usuarioId == Guid.Empty
                ? null
                : usuarioId;

        await db.SaveChangesAsync(ct);

        return plan.Id;
    }


    public async Task CambiarEstadoPlanAsync(
        Guid planId,
        bool activo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var plan =
            await db.Planes
                .FirstOrDefaultAsync(
                    x => x.Id == planId,
                    ct)
            ?? throw new InvalidOperationException(
                "El plan indicado no existe.");

        plan.Activo = activo;
        plan.ModificadoUtc =
            DateTimeOffset.UtcNow;
        plan.ModificadoPorUsuarioId =
            usuarioId == Guid.Empty
                ? null
                : usuarioId;

        await db.SaveChangesAsync(ct);
    }


    public async Task<Guid> GuardarTarifaAsync(
        GuardarTarifaPlanDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        if (dto.PlanId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Debe indicar el plan.");
        }

        if (dto.PrecioMensual <= 0)
        {
            throw new InvalidOperationException(
                "La tarifa mensual debe ser mayor a cero.");
        }

        /*
         * Las tarifas son mensuales.
         * Si la médica/administrativa eligió, por ejemplo,
         * 18/11/2026, la vigencia se normaliza a 01/11/2026.
         */
        var vigenteDesde =
            new DateOnly(
                dto.VigenciaDesde.Year,
                dto.VigenciaDesde.Month,
                1);

        await using var db =
            await factory.CreateDbContextAsync(ct);

        var planExiste =
            await db.Planes
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == dto.PlanId,
                    ct);

        if (!planExiste)
        {
            throw new InvalidOperationException(
                "El plan indicado no existe.");
        }

        await using var transaction =
            await db.Database
                .BeginTransactionAsync(ct);

        var mismaFecha =
            await db.TarifasPlan
                .FirstOrDefaultAsync(
                    x =>
                        x.PlanId == dto.PlanId
                        &&
                        x.VigenciaDesde ==
                            vigenteDesde,
                    ct);

        if (mismaFecha is not null)
        {
            /*
             * Mismo plan + mismo mes = corrección de la tarifa.
             * No generamos un duplicado histórico.
             */
            mismaFecha.PrecioMensual =
                dto.PrecioMensual;
            mismaFecha.ModificadoUtc =
                DateTimeOffset.UtcNow;
            mismaFecha.ModificadoPorUsuarioId =
                usuarioId == Guid.Empty
                    ? null
                    : usuarioId;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return mismaFecha.Id;
        }

        var anterior =
            await db.TarifasPlan
                .Where(x =>
                    x.PlanId == dto.PlanId
                    &&
                    x.VigenciaDesde < vigenteDesde)
                .OrderByDescending(x =>
                    x.VigenciaDesde)
                .FirstOrDefaultAsync(ct);

        var siguiente =
            await db.TarifasPlan
                .AsNoTracking()
                .Where(x =>
                    x.PlanId == dto.PlanId
                    &&
                    x.VigenciaDesde > vigenteDesde)
                .OrderBy(x =>
                    x.VigenciaDesde)
                .FirstOrDefaultAsync(ct);

        if (
            anterior is not null
            &&
            (
                !anterior.VigenciaHasta.HasValue
                ||
                anterior.VigenciaHasta.Value >=
                    vigenteDesde
            ))
        {
            anterior.VigenciaHasta =
                vigenteDesde.AddDays(-1);
            anterior.ModificadoUtc =
                DateTimeOffset.UtcNow;
            anterior.ModificadoPorUsuarioId =
                usuarioId == Guid.Empty
                    ? null
                    : usuarioId;
        }

        var nueva =
            new TarifaPlan
            {
                Id = Guid.NewGuid(),
                PlanId = dto.PlanId,
                PrecioMensual =
                    dto.PrecioMensual,
                VigenciaDesde =
                    vigenteDesde,
                VigenciaHasta =
                    siguiente is null
                        ? null
                        : siguiente
                            .VigenciaDesde
                            .AddDays(-1),
                CreadoPorUsuarioId =
                    usuarioId == Guid.Empty
                        ? null
                        : usuarioId
            };

        db.TarifasPlan.Add(nueva);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return nueva.Id;
    }


    private static void ValidarPlan(
        GuardarPlanDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            throw new InvalidOperationException(
                "Debe indicar el nombre del plan.");
        }

        if (dto.VecesPorSemana <= 0)
        {
            throw new InvalidOperationException(
                "La cantidad de clases por semana debe ser mayor a cero.");
        }
    }


    private static DateOnly ObtenerHoyArgentina()
    {
        var zona =
            ObtenerZonaHorariaArgentina();

        var ahora =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                zona);

        return DateOnly.FromDateTime(
            ahora.DateTime);
    }


    private static TimeZoneInfo ObtenerZonaHorariaArgentina()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "America/Argentina/Buenos_Aires");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Argentina Standard Time");
        }
    }


    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(texto)
            ? null
            : texto.Trim();
    }
}
