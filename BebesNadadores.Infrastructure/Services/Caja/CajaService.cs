using BebesNadadores.Application.Caja;
using BebesNadadores.Application.Caja.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Caja;

public sealed class CajaService(
    IDbContextFactory<AppDbContext> factory)
    : ICajaService
{
    // =========================================================
    // CAJA DE HOY
    // =========================================================

    public async Task<CajaDetalleDto?>
        ObtenerHoyAsync(
            CancellationToken ct = default)
    {
        var hoy =
            ObtenerFechaArgentina();


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var caja =
            await db.Cajas
                .AsNoTracking()

                .Include(x =>
                    x.Movimientos)

                .FirstOrDefaultAsync(
                    x =>
                        x.FechaOperativa ==
                            hoy,
                    ct);


        if (caja is null)
        {
            return null;
        }


        return await MapearDetalleAsync(
            db,
            caja,
            ct);
    }


    // =========================================================
    // OBTENER POR ID
    // =========================================================

    public async Task<CajaDetalleDto?>
        ObtenerAsync(
            Guid cajaId,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var caja =
            await db.Cajas
                .AsNoTracking()

                .Include(x =>
                    x.Movimientos)

                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            cajaId,
                    ct);


        if (caja is null)
        {
            return null;
        }


        return await MapearDetalleAsync(
            db,
            caja,
            ct);
    }


    // =========================================================
    // HISTÓRICO
    // =========================================================

    public async Task<IReadOnlyList<CajaResumenDto>>
        ListarHistoricoAsync(
            DateOnly? desde = null,
            DateOnly? hasta = null,
            int cantidad = 90,
            CancellationToken ct = default)
    {
        if (
            desde.HasValue
            &&
            hasta.HasValue
            &&
            hasta.Value <
                desde.Value)
        {
            throw new InvalidOperationException(
                "La fecha hasta no puede ser anterior a la fecha desde.");
        }


        if (cantidad <= 0)
        {
            cantidad =
                90;
        }


        cantidad =
            Math.Min(
                cantidad,
                365);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var query =
            db.Cajas
                .AsNoTracking()
                .AsQueryable();


        if (desde.HasValue)
        {
            query =
                query.Where(x =>
                    x.FechaOperativa >=
                        desde.Value);
        }


        if (hasta.HasValue)
        {
            query =
                query.Where(x =>
                    x.FechaOperativa <=
                        hasta.Value);
        }


        var cajas =
            await query
                .OrderByDescending(x =>
                    x.FechaOperativa)

                .Take(cantidad)

                .ToListAsync(ct);


        if (cajas.Count == 0)
        {
            return [];
        }


        var cajaIds =
            cajas
                .Select(x =>
                    x.Id)
                .ToList();


        var movimientos =
            await db.MovimientosCaja
                .AsNoTracking()

                .Where(x =>
                    cajaIds.Contains(
                        x.CajaId))

                .ToListAsync(ct);


        var usuarioIds =
            cajas
                .Select(x =>
                    x.UsuarioAperturaId)

                .Concat(
                    cajas
                        .Where(x =>
                            x.UsuarioCierreId
                                .HasValue)

                        .Select(x =>
                            x.UsuarioCierreId!.Value))

                .Distinct()

                .ToList();


        var usuarios =
            await db.Users
                .AsNoTracking()

                .Where(x =>
                    usuarioIds.Contains(
                        x.Id))

                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.NombreCompleto,
                    ct);


        var resultado =
            new List<CajaResumenDto>();


        foreach (var caja in cajas)
        {
            var movimientosCaja =
                movimientos
                    .Where(x =>
                        x.CajaId ==
                            caja.Id)
                    .ToList();


            var totales =
                CalcularTotales(
                    caja,
                    movimientosCaja);


            usuarios.TryGetValue(
                caja.UsuarioAperturaId,
                out var usuarioApertura);


            string? usuarioCierre =
                null;


            if (caja.UsuarioCierreId.HasValue)
            {
                usuarios.TryGetValue(
                    caja.UsuarioCierreId.Value,
                    out usuarioCierre);
            }


            resultado.Add(
                new CajaResumenDto(
                    caja.Id,
                    caja.FechaOperativa,
                    caja.Estado,
                    caja.FechaAperturaUtc,
                    caja.FechaCierreUtc,
                    caja.SaldoInicial,
                    totales.Ingresos,
                    totales.Egresos,
                    totales.SaldoEsperado,
                    caja.SaldoFinalDeclarado,

                    caja.SaldoFinalDeclarado.HasValue
                        ? caja.SaldoFinalDeclarado.Value
                          -
                          totales.SaldoEsperado
                        : null,

                    caja.UsuarioAperturaId,

                    usuarioApertura
                        ?? "Usuario",

                    caja.UsuarioCierreId,

                    usuarioCierre,

                    caja.Observaciones));
        }


        return resultado;
    }


    // =========================================================
    // ABRIR CAJA
    // =========================================================

    public async Task<Guid>
        AbrirAsync(
            AbrirCajaDto dto,
            Guid usuarioId,
            CancellationToken ct = default)
    {
        if (usuarioId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "No se pudo determinar el usuario que abre la caja.");
        }


        if (dto.SaldoInicial < 0)
        {
            throw new InvalidOperationException(
                "El saldo inicial no puede ser negativo.");
        }


        var observaciones =
            Normalizar(
                dto.Observaciones);


        if (
            observaciones is not null
            &&
            observaciones.Length > 1000)
        {
            throw new InvalidOperationException(
                "Las observaciones no pueden superar los 1000 caracteres.");
        }


        var hoy =
            ObtenerFechaArgentina();


        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database
                .BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    ct);


        try
        {
            // =================================================
            // USUARIO
            // =================================================

            var usuarioExiste =
                await db.Users
                    .AnyAsync(
                        x =>
                            x.Id ==
                                usuarioId,
                        ct);


            if (!usuarioExiste)
            {
                throw new InvalidOperationException(
                    "El usuario indicado no existe.");
            }


            // =================================================
            // CAJA ANTERIOR ABIERTA
            // =================================================

            var cajaAnteriorAbierta =
                await db.Cajas
                    .AsNoTracking()

                    .Where(x =>
                        x.Estado ==
                            EstadoCaja.Abierta)

                    .OrderBy(x =>
                        x.FechaOperativa)

                    .FirstOrDefaultAsync(ct);


            if (
                cajaAnteriorAbierta is not null
                &&
                cajaAnteriorAbierta.FechaOperativa !=
                    hoy)
            {
                throw new InvalidOperationException(
                    $"Existe una caja del " +
                    $"{cajaAnteriorAbierta.FechaOperativa:dd/MM/yyyy} " +
                    $"que todavía se encuentra abierta. " +
                    $"Debe cerrarla antes de abrir la caja de hoy.");
            }


            // =================================================
            // YA EXISTE CAJA HOY
            // =================================================

            var cajaHoy =
                await db.Cajas
                    .AsNoTracking()

                    .FirstOrDefaultAsync(
                        x =>
                            x.FechaOperativa ==
                                hoy,
                        ct);


            if (cajaHoy is not null)
            {
                if (cajaHoy.Estado ==
                    EstadoCaja.Abierta)
                {
                    throw new InvalidOperationException(
                        "La caja del día ya se encuentra abierta.");
                }


                throw new InvalidOperationException(
                    "La caja del día ya fue cerrada. " +
                    "No puede abrirse una segunda caja para la misma fecha.");
            }


            // =================================================
            // CREAR
            // =================================================

            var caja =
                new Domain.Entities.Caja
                {
                    Id =
                        Guid.NewGuid(),

                    FechaOperativa =
                        hoy,

                    FechaAperturaUtc =
                        DateTimeOffset.UtcNow,

                    SaldoInicial =
                        dto.SaldoInicial,

                    SaldoFinalDeclarado =
                        null,

                    UsuarioAperturaId =
                        usuarioId,

                    UsuarioCierreId =
                        null,

                    Estado =
                        EstadoCaja.Abierta,

                    Observaciones =
                        observaciones
                };


            db.Cajas.Add(
                caja);


            await db.SaveChangesAsync(
                ct);


            await transaction
                .CommitAsync(ct);


            return caja.Id;
        }
        catch
        {
            await transaction
                .RollbackAsync(ct);

            throw;
        }
    }


    // =========================================================
    // CERRAR CAJA
    // =========================================================

    public async Task<CerrarCajaResultadoDto>
        CerrarAsync(
            Guid cajaId,
            CerrarCajaDto dto,
            Guid usuarioId,
            CancellationToken ct = default)
    {
        if (cajaId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "Debe indicar la caja.");
        }


        if (usuarioId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "No se pudo determinar el usuario que cierra la caja.");
        }


        if (dto.SaldoFinalDeclarado < 0)
        {
            throw new InvalidOperationException(
                "El saldo final declarado no puede ser negativo.");
        }


        var observaciones =
            Normalizar(
                dto.Observaciones);


        if (
            observaciones is not null
            &&
            observaciones.Length > 1000)
        {
            throw new InvalidOperationException(
                "Las observaciones no pueden superar los 1000 caracteres.");
        }


        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database
                .BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    ct);


        try
        {
            var usuarioExiste =
                await db.Users
                    .AnyAsync(
                        x =>
                            x.Id ==
                                usuarioId,
                        ct);


            if (!usuarioExiste)
            {
                throw new InvalidOperationException(
                    "El usuario indicado no existe.");
            }


            var caja =
                await db.Cajas

                    .Include(x =>
                        x.Movimientos)

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                cajaId,
                        ct)

                ??
                throw new InvalidOperationException(
                    "La caja indicada no existe.");


            if (caja.Estado ==
                EstadoCaja.Cerrada)
            {
                throw new InvalidOperationException(
                    "La caja ya se encuentra cerrada.");
            }


            var totales =
                CalcularTotales(
                    caja,
                    caja.Movimientos);


            caja.SaldoFinalDeclarado =
                dto.SaldoFinalDeclarado;


            caja.FechaCierreUtc =
                DateTimeOffset.UtcNow;


            caja.UsuarioCierreId =
                usuarioId;


            caja.Estado =
                EstadoCaja.Cerrada;


            /*
             * Si el usuario escribió observaciones al cerrar,
             * las agregamos sin perder las de apertura.
             */
            if (observaciones is not null)
            {
                caja.Observaciones =
                    CombinarObservaciones(
                        caja.Observaciones,
                        observaciones);
            }


            await db.SaveChangesAsync(
                ct);


            await transaction
                .CommitAsync(ct);


            var diferencia =
                dto.SaldoFinalDeclarado
                -
                totales.SaldoEsperado;


            return new CerrarCajaResultadoDto(
                caja.Id,
                totales.SaldoEsperado,
                dto.SaldoFinalDeclarado,
                diferencia);
        }
        catch
        {
            await transaction
                .RollbackAsync(ct);

            throw;
        }
    }


    // =========================================================
    // MOVIMIENTO MANUAL
    // =========================================================

    public async Task<Guid>
    RegistrarMovimientoManualAsync(
        Guid cajaId,
        RegistrarMovimientoCajaDto dto,
        CancellationToken ct = default)
{
    if (cajaId ==
        Guid.Empty)
    {
        throw new InvalidOperationException(
            "Debe indicar la caja.");
    }


    if (dto.ConceptoCajaId ==
        Guid.Empty)
    {
        throw new InvalidOperationException(
            "Debe seleccionar un concepto.");
    }


    if (dto.Importe <= 0)
    {
        throw new InvalidOperationException(
            "El importe debe ser mayor a cero.");
    }


    var detalle =
        Normalizar(
            dto.Detalle);


    if (
        detalle is not null
        &&
        detalle.Length > 500)
    {
        throw new InvalidOperationException(
            "El detalle no puede superar los 500 caracteres.");
    }


    var referencia =
        Normalizar(
            dto.Referencia);


    if (
        referencia is not null
        &&
        referencia.Length > 200)
    {
        throw new InvalidOperationException(
            "La referencia no puede superar los 200 caracteres.");
    }


    var hoy =
        ObtenerFechaArgentina();


    await using var db =
        await factory.CreateDbContextAsync(ct);


    await using var transaction =
        await db.Database
            .BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                ct);


    try
    {
        var caja =
            await db.Cajas
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            cajaId,
                    ct)

            ??
            throw new InvalidOperationException(
                "La caja indicada no existe.");


        if (caja.Estado !=
            EstadoCaja.Abierta)
        {
            throw new InvalidOperationException(
                "No pueden registrarse movimientos en una caja cerrada.");
        }


        if (caja.FechaOperativa !=
            hoy)
        {
            throw new InvalidOperationException(
                "Sólo pueden registrarse movimientos en la caja del día actual.");
        }


        var concepto =
            await db.ConceptosCaja
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            dto.ConceptoCajaId
                        &&
                        x.Activo,
                    ct)

            ??
            throw new InvalidOperationException(
                "El concepto seleccionado no existe o está inactivo.");


        var movimiento =
            new MovimientoCaja
            {
                Id =
                    Guid.NewGuid(),

                CajaId =
                    caja.Id,

                FechaUtc =
                    DateTimeOffset.UtcNow,

                Tipo =
                    concepto.Tipo,

                ConceptoCajaId =
                    concepto.Id,

                Concepto =
                    concepto.Nombre,

                Detalle =
                    detalle,

                Importe =
                    dto.Importe,

                Referencia =
                    referencia,

                PagoId =
                    null
            };


        db.MovimientosCaja.Add(
            movimiento);


        await db.SaveChangesAsync(
            ct);


        await transaction
            .CommitAsync(ct);


        return movimiento.Id;
    }
    catch
    {
        await transaction
            .RollbackAsync(ct);

        throw;
    }
}


    // =========================================================
    // MAPEO DETALLE
    // =========================================================

    private static async Task<CajaDetalleDto>
        MapearDetalleAsync(
            AppDbContext db,
            Domain.Entities.Caja caja,
            CancellationToken ct)
    {
        /*
         * Cuando viene desde ObtenerAsync/HoyAsync,
         * Movimientos ya está cargado.
         */
        var movimientos =
            caja.Movimientos
                .OrderByDescending(x =>
                    x.FechaUtc)
                .ToList();


        var totales =
            CalcularTotales(
                caja,
                movimientos);


        var usuarioIds =
            new List<Guid>
            {
                caja.UsuarioAperturaId
            };


        if (caja.UsuarioCierreId.HasValue)
        {
            usuarioIds.Add(
                caja.UsuarioCierreId.Value);
        }


        var usuarios =
            await db.Users
                .AsNoTracking()

                .Where(x =>
                    usuarioIds.Contains(
                        x.Id))

                .ToDictionaryAsync(
                    x => x.Id,
                    x => x.NombreCompleto,
                    ct);


        usuarios.TryGetValue(
            caja.UsuarioAperturaId,
            out var usuarioApertura);


        string? usuarioCierre =
            null;


        if (caja.UsuarioCierreId.HasValue)
        {
            usuarios.TryGetValue(
                caja.UsuarioCierreId.Value,
                out usuarioCierre);
        }


       var movimientosDto =
    movimientos
        .Select(x =>
            new MovimientoCajaDto(
                x.Id,
                x.FechaUtc,
                x.Tipo,
                x.ConceptoCajaId,
                x.Concepto,
                x.Detalle,
                x.Importe,
                x.Referencia,
                x.PagoId))
        .ToList();


        return new CajaDetalleDto(
            caja.Id,
            caja.FechaOperativa,
            caja.Estado,
            caja.FechaAperturaUtc,
            caja.FechaCierreUtc,
            caja.SaldoInicial,
            totales.Ingresos,
            totales.Egresos,
            totales.SaldoEsperado,
            caja.SaldoFinalDeclarado,

            caja.SaldoFinalDeclarado.HasValue
                ? caja.SaldoFinalDeclarado.Value
                  -
                  totales.SaldoEsperado
                : null,

            caja.UsuarioAperturaId,

            usuarioApertura
                ?? "Usuario",

            caja.UsuarioCierreId,

            usuarioCierre,

            caja.Observaciones,

            movimientosDto);
    }


    // =========================================================
    // TOTALES
    // =========================================================

   private static TotalesCaja
    CalcularTotales(
        Domain.Entities.Caja caja,
        IEnumerable<MovimientoCaja> movimientos)
{
    var ingresos =
        movimientos
            .Where(x =>
                x.Tipo ==
                    TipoMovimientoCaja.Ingreso)

            .Sum(x =>
                x.Importe);


    var egresos =
        movimientos
            .Where(x =>
                x.Tipo ==
                    TipoMovimientoCaja.Egreso)

            .Sum(x =>
                x.Importe);


    var saldoEsperado =
        caja.SaldoInicial
        +
        ingresos
        -
        egresos;


    return new TotalesCaja(
        ingresos,
        egresos,
        saldoEsperado);
}


    // =========================================================
    // FECHA ARGENTINA
    // =========================================================

    private static DateOnly
        ObtenerFechaArgentina()
    {
        var zona =
            ObtenerZonaHorariaArgentina();


        var ahoraArgentina =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                zona);


        return DateOnly.FromDateTime(
            ahoraArgentina.DateTime);
    }


    private static TimeZoneInfo
        ObtenerZonaHorariaArgentina()
    {
        /*
         * Linux / VPS
         */
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch
        {
            /*
             * Windows
             */
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
    }


    // =========================================================
    // HELPERS
    // =========================================================

    private static string?
        Normalizar(
            string? valor)
    {
        return string.IsNullOrWhiteSpace(
            valor)
            ? null
            : valor.Trim();
    }


    private static string
        CombinarObservaciones(
            string? actual,
            string nueva)
    {
        if (string.IsNullOrWhiteSpace(
            actual))
        {
            return nueva;
        }


        var resultado =
            $"{actual.Trim()}\n{nueva.Trim()}";


        /*
         * La propiedad tiene máximo 1000.
         */
        return resultado.Length <= 1000
            ? resultado
            : resultado[..1000];
    }


    private sealed record TotalesCaja(
        decimal Ingresos,
        decimal Egresos,
        decimal SaldoEsperado);
}