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

    public Task<Guid> AbrirAsync(
        AbrirCajaDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => AbrirInternoAsync(
                dto,
                usuarioId,
                ct),
            ct);
    }


    private async Task<Guid> AbrirInternoAsync(
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


        // Sin try/catch con Rollback manual: si la transacción ya está
        // abortada, el Rollback explota y tapa el error real. El
        // "await using" hace el rollback solo.
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


    // =========================================================
    // CERRAR CAJA
    // =========================================================

    public Task<CerrarCajaResultadoDto> CerrarAsync(
        Guid cajaId,
        CerrarCajaDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => CerrarInternoAsync(
                cajaId,
                dto,
                usuarioId,
                ct),
            ct);
    }


    private async Task<CerrarCajaResultadoDto> CerrarInternoAsync(
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


        // Sin try/catch con Rollback manual: si la transacción ya está
        // abortada, el Rollback explota y tapa el error real. El
        // "await using" hace el rollback solo.
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


    // =========================================================
    // REABRIR CAJA (solo la de hoy)
    // =========================================================

    /*
     * Si la caja de HOY se cerró por error se puede reabrir:
     * vuelve a quedar Abierta y se borra el cierre (saldo
     * declarado, fecha y usuario de cierre). Queda registrado en
     * las observaciones quién la reabrió, cuándo y por qué.
     *
     * Una caja de un día anterior NO se reabre: ese día ya se
     * contó el efectivo. Las correcciones se hacen con anulaciones,
     * que registran la reversa en la caja de hoy.
     */
    public Task ReabrirAsync(
        Guid cajaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => ReabrirInternoAsync(
                cajaId,
                motivo,
                usuarioId,
                ct),
            ct);
    }


    private async Task ReabrirInternoAsync(
        Guid cajaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "No se pudo determinar el usuario que reabre la caja.");
        }

        var motivoNormalizado =
            Normalizar(motivo)
            ?? throw new InvalidOperationException(
                "Debe indicar el motivo de la reapertura.");

        if (motivoNormalizado.Length > 300)
        {
            throw new InvalidOperationException(
                "El motivo no puede superar los 300 caracteres.");
        }

        var hoy =
            ObtenerFechaArgentina();

        await using var db =
            await factory.CreateDbContextAsync(ct);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                ct);

        var caja =
            await db.Cajas
                .FirstOrDefaultAsync(
                    x => x.Id == cajaId,
                    ct)
            ?? throw new InvalidOperationException(
                "La caja indicada no existe.");

        if (caja.Estado != EstadoCaja.Cerrada)
        {
            throw new InvalidOperationException(
                "La caja ya se encuentra abierta.");
        }

        if (caja.FechaOperativa != hoy)
        {
            throw new InvalidOperationException(
                $"Solo se puede reabrir la caja del día. La caja del " +
                $"{caja.FechaOperativa:dd/MM/yyyy} ya quedó cerrada; si hay que " +
                $"corregir algo, anulá el movimiento y la reversa se registra hoy.");
        }

        /*
         * No puede haber dos cajas abiertas a la vez.
         * (Por cómo funciona la apertura no debería pasar, pero se
         * valida igual: la reapertura es una acción sensible.)
         */
        var otraAbierta =
            await db.Cajas
                .AsNoTracking()
                .Where(x =>
                    x.Id != caja.Id
                    && x.Estado == EstadoCaja.Abierta)
                .Select(x => (DateOnly?)x.FechaOperativa)
                .FirstOrDefaultAsync(ct);

        if (otraAbierta.HasValue)
        {
            throw new InvalidOperationException(
                $"Hay otra caja abierta (del {otraAbierta.Value:dd/MM/yyyy}). " +
                $"Cerrala antes de reabrir la de hoy.");
        }

        var usuarioExiste =
            await db.Users
                .AnyAsync(
                    x => x.Id == usuarioId,
                    ct);

        if (!usuarioExiste)
        {
            throw new InvalidOperationException(
                "El usuario indicado no existe.");
        }

        if (!caja.FechaCierreUtc.HasValue
            || !caja.UsuarioCierreId.HasValue
            || !caja.SaldoFinalDeclarado.HasValue)
        {
            throw new InvalidOperationException(
                "La caja no tiene registrados los datos de su cierre; " +
                "no se puede reabrir sin perder esa información.");
        }

        /*
         * Antes de limpiar el cierre, se guarda una "foto" del cierre
         * anterior: quién la cerró, cuándo y con qué saldo.
         * Los movimientos no se tocan: siguen en la misma caja.
         */
        db.CajasReaperturas.Add(
            new CajaReapertura
            {
                Id = Guid.NewGuid(),
                CajaId = caja.Id,
                FechaUtc = DateTimeOffset.UtcNow,
                UsuarioId = usuarioId,
                Motivo = motivoNormalizado,
                FechaCierreAnteriorUtc = caja.FechaCierreUtc.Value,
                UsuarioCierreAnteriorId = caja.UsuarioCierreId.Value,
                SaldoFinalDeclaradoAnterior = caja.SaldoFinalDeclarado.Value
            });

        caja.Estado = EstadoCaja.Abierta;
        caja.SaldoFinalDeclarado = null;
        caja.FechaCierreUtc = null;
        caja.UsuarioCierreId = null;
        caja.ModificadoUtc = DateTimeOffset.UtcNow;
        caja.ModificadoPorUsuarioId = usuarioId;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }


    // =========================================================
    // MOVIMIENTO MANUAL
    // =========================================================

    public Task<Guid> RegistrarMovimientoManualAsync(
        Guid cajaId,
        RegistrarMovimientoCajaDto dto,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => RegistrarMovimientoManualInternoAsync(
                cajaId,
                dto,
                ct),
            ct);
    }


    private async Task<Guid> RegistrarMovimientoManualInternoAsync(
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


    // Sin try/catch con Rollback manual: si la transacción ya está
    // abortada, el Rollback explota y tapa el error real. El
    // "await using" hace el rollback solo.
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

        // Quién cargó cada movimiento (auditoría).
        usuarioIds.AddRange(
            movimientos
                .Where(x => x.CreadoPorUsuarioId.HasValue)
                .Select(x => x.CreadoPorUsuarioId!.Value)
                .Distinct());


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


        // -----------------------------------------------------
        // REAPERTURAS (auditoría)
        // -----------------------------------------------------

        var reaperturas =
            await db.CajasReaperturas
                .AsNoTracking()
                .Where(x => x.CajaId == caja.Id)
                .OrderBy(x => x.FechaUtc)
                .ToListAsync(ct);

        var idsUsuariosReapertura =
            reaperturas
                .SelectMany(x => new[] { x.UsuarioId, x.UsuarioCierreAnteriorId })
                .Where(x => !usuarios.ContainsKey(x))
                .Distinct()
                .ToList();

        if (idsUsuariosReapertura.Count > 0)
        {
            var extra =
                await db.Users
                    .AsNoTracking()
                    .Where(x => idsUsuariosReapertura.Contains(x.Id))
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => x.NombreCompleto,
                        ct);

            foreach (var par in extra)
            {
                usuarios[par.Key] = par.Value;
            }
        }

        var reaperturasDto =
            reaperturas
                .Select(x =>
                    new CajaReaperturaDto(
                        x.FechaUtc,
                        usuarios.GetValueOrDefault(x.UsuarioId) ?? "—",
                        x.Motivo,
                        x.FechaCierreAnteriorUtc,
                        usuarios.GetValueOrDefault(x.UsuarioCierreAnteriorId) ?? "—",
                        x.SaldoFinalDeclaradoAnterior))
                .ToList();


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
                x.PagoId,
                x.PagoProveedorId,
                x.AnuladoUtc.HasValue,
                x.AnulaMovimientoId.HasValue,
                x.MotivoAnulacion,
                x.CreadoPorUsuarioId.HasValue
                    ? usuarios.GetValueOrDefault(x.CreadoPorUsuarioId.Value)
                    : null))
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

            movimientosDto,

            reaperturasDto);
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