using BebesNadadores.Application.CiclosLectivos;
using BebesNadadores.Application.CiclosLectivos.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.CiclosLectivos;

public sealed class CicloLectivoService(
    IDbContextFactory<AppDbContext> factory)
    : ICicloLectivoService
{
    public async Task<IReadOnlyList<CicloLectivoDto>>
        ListarAsync(
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        var ciclos =
            await db.CiclosLectivos
                .AsNoTracking()
                .OrderByDescending(x => x.Anio)
                .ToListAsync(ct);


        var resultado =
            new List<CicloLectivoDto>();


        foreach (var ciclo in ciclos)
        {
            resultado.Add(
                await MapearAsync(
                    db,
                    ciclo,
                    ct));
        }


        return resultado;
    }


    public async Task<CicloLectivoDto?>
        ObtenerAsync(
            Guid id,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var ciclo =
            await db.CiclosLectivos
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct);


        if (ciclo is null)
            return null;


        return await MapearAsync(
            db,
            ciclo,
            ct);
    }


    public async Task<CicloLectivoDto?>
        ObtenerPorAnioAsync(
            int anio,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var ciclo =
            await db.CiclosLectivos
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Anio == anio,
                    ct);


        if (ciclo is null)
            return null;


        return await MapearAsync(
            db,
            ciclo,
            ct);
    }


    public async Task<Guid> CrearAsync(
        GuardarCicloLectivoDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var existe =
            await db.CiclosLectivos
                .AnyAsync(
                    x => x.Anio == dto.Anio,
                    ct);


        if (existe)
        {
            throw new InvalidOperationException(
                $"Ya existe un ciclo lectivo para el año {dto.Anio}.");
        }


        var ciclo =
            new CicloLectivo
            {
                Id =
                    Guid.NewGuid(),

                Anio =
                    dto.Anio,

                FechaInicio =
                    dto.FechaInicio,

                FechaFin =
                    dto.FechaFin,

                Estado =
                    EstadoCicloLectivo.Borrador,

                Observaciones =
                    Normalizar(
                        dto.Observaciones)
            };


        db.CiclosLectivos.Add(ciclo);

        await db.SaveChangesAsync(ct);


        return ciclo.Id;
    }


    public async Task ActualizarAsync(
        Guid id,
        GuardarCicloLectivoDto dto,
        CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var ciclo =
            await db.CiclosLectivos
                .FirstOrDefaultAsync(
                    x => x.Id == id,
                    ct)
            ?? throw new InvalidOperationException(
                "El ciclo lectivo indicado no existe.");


        if (ciclo.Estado ==
            EstadoCicloLectivo.Cerrado)
        {
            throw new InvalidOperationException(
                "No se puede modificar un ciclo lectivo cerrado.");
        }


        var otroMismoAnio =
            await db.CiclosLectivos
                .AnyAsync(
                    x =>
                        x.Id != id &&
                        x.Anio == dto.Anio,
                    ct);


        if (otroMismoAnio)
        {
            throw new InvalidOperationException(
                $"Ya existe otro ciclo lectivo para {dto.Anio}.");
        }


        /*
         * Mientras está publicado evitamos cambiar el rango.
         * Después podemos agregar un flujo específico para
         * extender un ciclo.
         */
        if (ciclo.Estado ==
            EstadoCicloLectivo.Publicado)
        {
            if (
                ciclo.FechaInicio != dto.FechaInicio
                ||
                ciclo.FechaFin != dto.FechaFin)
            {
                throw new InvalidOperationException(
                    "No se pueden cambiar las fechas de un ciclo ya publicado.");
            }
        }


        ciclo.Anio =
            dto.Anio;

        ciclo.FechaInicio =
            dto.FechaInicio;

        ciclo.FechaFin =
            dto.FechaFin;

        ciclo.Observaciones =
            Normalizar(
                dto.Observaciones);


        await db.SaveChangesAsync(ct);
    }


    private static async Task<CicloLectivoDto>
        MapearAsync(
            AppDbContext db,
            CicloLectivo ciclo,
            CancellationToken ct)
    {
        var cantidadTurnos =
            await db.Turnos
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.CicloLectivoId ==
                        ciclo.Id
                        &&
                        x.Estado !=
                        EstadoTurno.Baja,
                    ct);


        var cantidadSesiones =
            await db.SesionesClase
                .AsNoTracking()
                .CountAsync(
                    x =>
                        x.Fecha >=
                        ciclo.FechaInicio
                        &&
                        x.Fecha <=
                        ciclo.FechaFin
                        &&
                        x.Turno.CicloLectivoId ==
                        ciclo.Id,
                    ct);


        return new CicloLectivoDto(
            ciclo.Id,
            ciclo.Anio,
            ciclo.FechaInicio,
            ciclo.FechaFin,
            ciclo.Estado,
            ciclo.FechaPublicacionUtc,
            ciclo.Observaciones,
            cantidadTurnos,
            cantidadSesiones);
    }

    public async Task CerrarAsync(
    Guid id,
    CancellationToken ct = default)
{
    await using var db =
        await factory.CreateDbContextAsync(ct);


    var ciclo =
        await db.CiclosLectivos
            .FirstOrDefaultAsync(
                x => x.Id == id,
                ct)
        ?? throw new InvalidOperationException(
            "El ciclo lectivo indicado no existe.");


    if (ciclo.Estado ==
        EstadoCicloLectivo.Cerrado)
    {
        return;
    }


    /*
     * Permitimos cerrar tanto un ciclo publicado
     * como uno que haya quedado en borrador.
     *
     * El cierre es lógico: conservamos toda
     * la información histórica.
     */
    ciclo.Estado =
        EstadoCicloLectivo.Cerrado;


    await db.SaveChangesAsync(ct);
}


    private static void Validar(
        GuardarCicloLectivoDto dto)
    {
        if (
            dto.Anio < 2020
            ||
            dto.Anio > 2100)
        {
            throw new InvalidOperationException(
                "El año del ciclo lectivo no es válido.");
        }


        if (dto.FechaFin <
            dto.FechaInicio)
        {
            throw new InvalidOperationException(
                "La fecha de fin no puede ser anterior al inicio.");
        }


        if (
            dto.FechaInicio.Year !=
            dto.Anio
            ||
            dto.FechaFin.Year !=
            dto.Anio)
        {
            throw new InvalidOperationException(
                "Las fechas deben pertenecer al año del ciclo lectivo.");
        }
    }


    private static string? Normalizar(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }
}