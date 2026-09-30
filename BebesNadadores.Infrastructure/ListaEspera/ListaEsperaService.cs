using BebesNadadores.Application.ListaEspera;
using BebesNadadores.Application.ListaEspera.Dtos;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using ListaEsperaEntity =
    BebesNadadores.Domain.Entities.ListaEspera;

namespace BebesNadadores.Infrastructure.Services.ListaEspera;

public sealed class ListaEsperaService(
    IDbContextFactory<AppDbContext> factory)
    : IListaEsperaService
{
    // =========================================================
    // BUSCAR BEBÉS PARA EL AUTOCOMPLETE
    // =========================================================

    public async Task<IReadOnlyList<BebeListaEsperaBusquedaDto>>
        BuscarBebesAsync(
            string? texto,
            int maxResultados = 10,
            CancellationToken ct = default)
    {
        if (maxResultados <= 0)
        {
            return [];
        }


        maxResultados =
            Math.Min(
                maxResultados,
                30);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        var busqueda =
            Normalizar(texto);


        // =====================================================
        // BEBÉS ACTIVOS CON RECUPEROS DISPONIBLES
        // =====================================================

        var query =
            db.Bebes
                .AsNoTracking()

                .Include(x =>
                    x.NivelActual)

                .Include(x =>
                    x.Responsables)
                    .ThenInclude(x =>
                        x.Responsable)

                .Include(x =>
                    x.Recuperaciones)

                .Where(x =>
                    x.Estado ==
                        EstadoBebe.Activo

                    &&
                    x.NivelActualId.HasValue

                    &&
                    x.NivelActual != null

                    &&
                    x.Recuperaciones.Any(r =>
                        r.Estado ==
                            EstadoRecuperacion.Disponible

                        &&
                        !r.SesionUsoId.HasValue

                        &&
                        (
                            !r.FechaVencimiento.HasValue
                            ||
                            r.FechaVencimiento.Value >=
                                hoy
                        )));


        // =====================================================
        // FILTRO TEXTO
        // =====================================================

        if (!string.IsNullOrWhiteSpace(
            busqueda))
        {
            var textoLower =
                busqueda.ToLower();


            query =
                query.Where(x =>
                    x.Nombre.ToLower()
                        .Contains(textoLower)

                    ||

                    x.Apellido.ToLower()
                        .Contains(textoLower)

                    ||

                    (
                        x.Nombre + " " + x.Apellido
                    )
                    .ToLower()
                    .Contains(textoLower)

                    ||

                    (
                        x.Apellido + " " + x.Nombre
                    )
                    .ToLower()
                    .Contains(textoLower));
        }


        var bebes =
            await query
                .OrderBy(x =>
                    x.Apellido)

                .ThenBy(x =>
                    x.Nombre)

                .Take(maxResultados)

                .ToListAsync(ct);


        if (bebes.Count == 0)
        {
            return [];
        }


        var bebeIds =
            bebes
                .Select(x =>
                    x.Id)
                .ToList();


        // =====================================================
        // RECUPEROS QUE YA ESTÁN EN LISTA DE ESPERA
        //
        // Un bebé puede tener más de un recupero.
        //
        // Si uno ya está en lista, todavía puede aparecer
        // si tiene otro recupero disponible.
        // =====================================================

        var recuperacionesEnEspera =
            await db.ListaEspera
                .AsNoTracking()

                .Where(x =>
                    bebeIds.Contains(
                        x.BebeId)

                    &&
                    x.RecuperacionId.HasValue

                    &&
                    (
                        x.Estado ==
                            EstadoListaEspera.Activa

                        ||

                        x.Estado ==
                            EstadoListaEspera.Notificada
                    ))

                .Select(x =>
                    x.RecuperacionId!.Value)

                .ToListAsync(ct);


        var recuperacionesEnEsperaSet =
            recuperacionesEnEspera
                .ToHashSet();


        var resultado =
            new List<BebeListaEsperaBusquedaDto>();


        foreach (var bebe in bebes)
        {
            var recuperaciones =
                bebe
                    .Recuperaciones

                    .Where(x =>
                        x.Estado ==
                            EstadoRecuperacion.Disponible

                        &&
                        !x.SesionUsoId.HasValue

                        &&
                        (
                            !x.FechaVencimiento.HasValue
                            ||
                            x.FechaVencimiento.Value >=
                                hoy
                        )

                        &&
                        !recuperacionesEnEsperaSet
                            .Contains(x.Id))

                    /*
                     * Primero mostramos los que vencen antes.
                     *
                     * Los que no vencen quedan últimos.
                     */
                    .OrderBy(x =>
                        x.FechaVencimiento
                        ??
                        DateOnly.MaxValue)

                    .ThenBy(x =>
                        x.FechaGeneracion)

                    .Select(x =>
                        new RecuperacionDisponibleListaEsperaDto(
                            x.Id,
                            x.Tipo,
                            x.FechaGeneracion,
                            x.FechaVencimiento,
                            x.SinVencimiento))

                    .ToList();


            /*
             * Todos los recuperos disponibles de este bebé
             * ya podrían estar actualmente en lista.
             */
            if (recuperaciones.Count == 0)
            {
                continue;
            }


            var principal =
                bebe
                    .Responsables

                    .OrderByDescending(x =>
                        x.EsContactoPrincipal)

                    .ThenBy(x =>
                        x.Responsable.Apellido)

                    .ThenBy(x =>
                        x.Responsable.Nombre)

                    .FirstOrDefault();


            resultado.Add(
                new BebeListaEsperaBusquedaDto(
                    bebe.Id,

                    $"{bebe.Nombre} {bebe.Apellido}"
                        .Trim(),

                    bebe.NivelActualId!.Value,

                    bebe.NivelActual!.Nombre,

                    principal is null
                        ? null
                        : $"{principal.Responsable.Nombre} " +
                          $"{principal.Responsable.Apellido}",

                    principal?.Responsable.WhatsApp,

                    principal?.Responsable.Email,

                    recuperaciones));
        }


        return resultado;
    }


    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<ListaEsperaItemDto>>
        ListarAsync(
            bool incluirFinalizadas = false,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        /*
         * Primero dejamos consistentes las solicitudes
         * cuyos recuperos ya vencieron.
         */
        await ActualizarVencidasAsync(
            db,
            hoy,
            ct);


        var query =
            db.ListaEspera
                .AsNoTracking()

                .Include(x =>
                    x.Bebe)
                    .ThenInclude(x =>
                        x.Responsables)
                            .ThenInclude(x =>
                                x.Responsable)

                .Include(x =>
                    x.Nivel)

                .Include(x =>
                    x.Recuperacion)

                .AsQueryable();


        if (!incluirFinalizadas)
        {
            query =
                query.Where(x =>
                    x.Estado ==
                        EstadoListaEspera.Activa

                    ||

                    x.Estado ==
                        EstadoListaEspera.Notificada);
        }


        var items =
            await query
                .OrderBy(x =>
                    x.Estado)

                .ThenBy(x =>
                    x.FechaAltaUtc)

                .ToListAsync(ct);


        return items
            .Select(Mapear)
            .ToList();
    }


    // =========================================================
    // AGREGAR
    // =========================================================

    public async Task<Guid> AgregarAsync(
        CrearListaEsperaDto dto,
        CancellationToken ct = default)
    {
        ValidarDto(
            dto.DiaPreferido,
            dto.FranjaHoraria,
            dto.Observaciones);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database
                .BeginTransactionAsync(ct);


        var hoy =
            DateOnly.FromDateTime(
                DateTime.Today);


        // =====================================================
        // BEBÉ
        // =====================================================

        var bebe =
            await db.Bebes

                .Include(x =>
                    x.NivelActual)

                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            dto.BebeId,
                    ct)

            ?? throw new InvalidOperationException(
                "El bebé seleccionado no existe.");


        if (bebe.Estado !=
            EstadoBebe.Activo)
        {
            throw new InvalidOperationException(
                "El bebé seleccionado no se encuentra activo.");
        }


        if (
            !bebe.NivelActualId.HasValue
            ||
            bebe.NivelActual is null)
        {
            throw new InvalidOperationException(
                "El bebé no tiene un nivel actual asignado.");
        }


        // =====================================================
        // RECUPERO
        // =====================================================

        var recuperacion =
            await db.Recuperaciones
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            dto.RecuperacionId,
                    ct)

            ?? throw new InvalidOperationException(
                "El recupero seleccionado no existe.");


        if (recuperacion.BebeId !=
            bebe.Id)
        {
            throw new InvalidOperationException(
                "El recupero seleccionado no pertenece al bebé.");
        }


        if (recuperacion.Estado !=
            EstadoRecuperacion.Disponible)
        {
            throw new InvalidOperationException(
                "El recupero seleccionado ya no está disponible.");
        }


        if (recuperacion.SesionUsoId.HasValue)
        {
            throw new InvalidOperationException(
                "El recupero seleccionado ya tiene una clase asignada.");
        }


        // =====================================================
        // VENCIMIENTO
        // =====================================================

        if (
            recuperacion.FechaVencimiento.HasValue
            &&
            recuperacion.FechaVencimiento.Value <
                hoy)
        {
            recuperacion.Estado =
                EstadoRecuperacion.Vencida;


            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);


            throw new InvalidOperationException(
                "El recupero seleccionado se encuentra vencido.");
        }


        // =====================================================
        // NO DUPLICAR EL MISMO RECUPERO EN LA LISTA
        // =====================================================

        var yaExiste =
            await db.ListaEspera
                .AnyAsync(
                    x =>
                        x.RecuperacionId ==
                            recuperacion.Id

                        &&
                        (
                            x.Estado ==
                                EstadoListaEspera.Activa

                            ||

                            x.Estado ==
                                EstadoListaEspera.Notificada
                        ),
                    ct);


        if (yaExiste)
        {
            throw new InvalidOperationException(
                "Ese recupero ya se encuentra en la lista de espera.");
        }


        // =====================================================
        // CREAR SOLICITUD
        // =====================================================

        var item =
            new ListaEsperaEntity
            {
                Id =
                    Guid.NewGuid(),

                BebeId =
                    bebe.Id,

                NivelId =
                    bebe.NivelActualId.Value,

                TurnoId =
                    null,

                RecuperacionId =
                    recuperacion.Id,

                DiaPreferido =
                    dto.DiaPreferido,

                FranjaHoraria =
                    dto.FranjaHoraria,

                Estado =
                    EstadoListaEspera.Activa,

                FechaAltaUtc =
                    DateTimeOffset.UtcNow,

                Observaciones =
                    Normalizar(
                        dto.Observaciones)
            };


        db.ListaEspera.Add(
            item);


        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);


        return item.Id;
    }


    // =========================================================
    // ACTUALIZAR PREFERENCIAS
    // =========================================================

    public async Task ActualizarAsync(
        Guid listaEsperaId,
        ActualizarListaEsperaDto dto,
        CancellationToken ct = default)
    {
        ValidarDto(
            dto.DiaPreferido,
            dto.FranjaHoraria,
            dto.Observaciones);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var item =
            await db.ListaEspera
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            listaEsperaId,
                    ct)

            ?? throw new InvalidOperationException(
                "La solicitud de lista de espera no existe.");


        if (item.Estado is
            EstadoListaEspera.Asignada
            or
            EstadoListaEspera.Cancelada
            or
            EstadoListaEspera.Vencida)
        {
            throw new InvalidOperationException(
                "La solicitud ya no puede modificarse.");
        }


        item.DiaPreferido =
            dto.DiaPreferido;


        item.FranjaHoraria =
            dto.FranjaHoraria;


        item.Observaciones =
            Normalizar(
                dto.Observaciones);


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // CANCELAR
    // =========================================================

    public async Task CancelarAsync(
        Guid listaEsperaId,
        CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var item =
            await db.ListaEspera
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            listaEsperaId,
                    ct)

            ?? throw new InvalidOperationException(
                "La solicitud de lista de espera no existe.");


        if (item.Estado ==
            EstadoListaEspera.Cancelada)
        {
            return;
        }


        if (item.Estado ==
            EstadoListaEspera.Asignada)
        {
            throw new InvalidOperationException(
                "No puede cancelar una solicitud que ya fue asignada.");
        }


        item.Estado =
            EstadoListaEspera.Cancelada;


        await db.SaveChangesAsync(ct);
    }


    // =========================================================
    // ACTUALIZAR SOLICITUDES VENCIDAS
    // =========================================================

    private static async Task ActualizarVencidasAsync(
        AppDbContext db,
        DateOnly hoy,
        CancellationToken ct)
    {
        var solicitudes =
            await db.ListaEspera

                .Include(x =>
                    x.Recuperacion)

                .Where(x =>
                    x.RecuperacionId.HasValue

                    &&
                    (
                        x.Estado ==
                            EstadoListaEspera.Activa

                        ||

                        x.Estado ==
                            EstadoListaEspera.Notificada
                    ))

                .ToListAsync(ct);


        var huboCambios =
            false;


        foreach (var solicitud in solicitudes)
        {
            var recuperacion =
                solicitud.Recuperacion;


            if (recuperacion is null)
            {
                continue;
            }


            // -------------------------------------------------
            // RECUPERO VENCIDO
            // -------------------------------------------------

            if (
                recuperacion.Estado ==
                    EstadoRecuperacion.Vencida

                ||

                (
                    recuperacion.FechaVencimiento.HasValue
                    &&
                    recuperacion.FechaVencimiento.Value <
                        hoy
                ))
            {
                solicitud.Estado =
                    EstadoListaEspera.Vencida;

                huboCambios =
                    true;

                continue;
            }


            // -------------------------------------------------
            // RECUPERO ANULADO
            // -------------------------------------------------

            if (recuperacion.Estado ==
                EstadoRecuperacion.Anulada)
            {
                solicitud.Estado =
                    EstadoListaEspera.Cancelada;

                huboCambios =
                    true;
            }
        }


        if (huboCambios)
        {
            await db.SaveChangesAsync(ct);
        }
    }


    // =========================================================
    // MAPEO
    // =========================================================

    private static ListaEsperaItemDto Mapear(
        ListaEsperaEntity item)
    {
        var principal =
            item
                .Bebe
                .Responsables

                .OrderByDescending(x =>
                    x.EsContactoPrincipal)

                .ThenBy(x =>
                    x.Responsable.Apellido)

                .ThenBy(x =>
                    x.Responsable.Nombre)

                .FirstOrDefault();


        return new ListaEsperaItemDto(
            item.Id,

            item.BebeId,

            $"{item.Bebe.Nombre} {item.Bebe.Apellido}"
                .Trim(),

            item.NivelId,

            item.Nivel.Nombre,

            item.RecuperacionId,

            item.Recuperacion?.Tipo,

            item.Recuperacion?.FechaVencimiento,

            item.DiaPreferido,

            item.FranjaHoraria,

            item.Estado,

            item.FechaAltaUtc,

            item.Observaciones,

            principal is null
                ? null
                : $"{principal.Responsable.Nombre} " +
                  $"{principal.Responsable.Apellido}",

            principal?.Responsable.WhatsApp,

            principal?.Responsable.Email);
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void ValidarDto(
        DiaSemana? diaPreferido,
        FranjaHoraria franja,
        string? observaciones)
    {
        if (
            diaPreferido.HasValue
            &&
            !Enum.IsDefined(
                typeof(DiaSemana),
                diaPreferido.Value))
        {
            throw new InvalidOperationException(
                "El día preferido no es válido.");
        }


        if (!Enum.IsDefined(
            typeof(FranjaHoraria),
            franja))
        {
            throw new InvalidOperationException(
                "La franja horaria no es válida.");
        }


        if (
            observaciones is not null
            &&
            observaciones.Trim().Length >
                1000)
        {
            throw new InvalidOperationException(
                "Las observaciones no pueden superar los 1000 caracteres.");
        }
    }


    // =========================================================
    // NORMALIZAR
    // =========================================================

    private static string? Normalizar(
        string? texto)
    {
        return string.IsNullOrWhiteSpace(
            texto)
            ? null
            : texto.Trim();
    }
}