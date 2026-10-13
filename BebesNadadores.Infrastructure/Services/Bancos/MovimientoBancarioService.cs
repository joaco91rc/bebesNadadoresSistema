using BebesNadadores.Application.Bancos;
using BebesNadadores.Application.Bancos.Dtos;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bancos;

public sealed class MovimientoBancarioService(
    IDbContextFactory<AppDbContext> factory)
    : IMovimientoBancarioService
{
    // =========================================================
    // LISTADO SIMPLE
    // =========================================================

    public async Task<IReadOnlyList<MovimientoBancarioDto>>
        ListarAsync(
            Guid? cuentaBancariaId,
            DateOnly desde,
            DateOnly hasta,
            CancellationToken ct = default)
    {
        if (hasta < desde)
        {
            throw new InvalidOperationException(
                "La fecha hasta no puede ser anterior a la fecha desde.");
        }


        var desdeUtc =
            ObtenerInicioDiaUtc(
                desde);


        var hastaUtc =
            ObtenerInicioDiaUtc(
                hasta.AddDays(1));


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var query =
            db.MovimientosBancarios
                .AsNoTracking()
                .Where(x =>
                    x.FechaUtc >= desdeUtc
                    &&
                    x.FechaUtc < hastaUtc);


        if (cuentaBancariaId.HasValue)
        {
            query =
                query.Where(x =>
                    x.CuentaBancariaId ==
                        cuentaBancariaId.Value);
        }


        return await query
            .OrderByDescending(x =>
                x.FechaUtc)

            .Select(x =>
                new MovimientoBancarioDto(
                    x.Id,
                    x.CuentaBancariaId,
                    x.CuentaBancaria.Nombre,
                    x.CuentaBancaria.Banco.Nombre,
                    x.FechaUtc,
                    x.Tipo,
                    x.Origen,
                    x.Importe,
                    x.Concepto,
                    x.ReferenciaExterna,
                    x.Observaciones,
                    x.PagoId,

                    x.Pago == null
                        ? null
                        : (
                            x.Pago.Bebe.Nombre
                            + " "
                            + x.Pago.Bebe.Apellido
                        ).Trim()))

            .ToListAsync(ct);
    }


    // =========================================================
    // LISTADO FILTRADO
    // =========================================================

    public async Task<ResumenMovimientosBancariosDto>
        ListarFiltradoAsync(
            MovimientosBancariosFiltroDto filtro,
            CancellationToken ct = default)
    {
        if (filtro.CuentaBancariaId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "Debe indicar una cuenta bancaria.");
        }


        if (
            filtro.Desde.HasValue
            &&
            filtro.Hasta.HasValue
            &&
            filtro.Hasta.Value <
                filtro.Desde.Value)
        {
            throw new InvalidOperationException(
                "La fecha hasta no puede ser anterior a la fecha desde.");
        }


        DateTimeOffset? desdeUtc =
            filtro.Desde.HasValue
                ? ObtenerInicioDiaUtc(
                    filtro.Desde.Value)
                : null;


        DateTimeOffset? hastaUtc =
            filtro.Hasta.HasValue
                ? ObtenerInicioDiaUtc(
                    filtro.Hasta.Value.AddDays(1))
                : null;


        await using var db =
            await factory.CreateDbContextAsync(ct);


        var existeCuenta =
            await db.CuentasBancarias
                .AsNoTracking()
                .AnyAsync(
                    x =>
                        x.Id ==
                            filtro.CuentaBancariaId,
                    ct);


        if (!existeCuenta)
        {
            throw new InvalidOperationException(
                "La cuenta bancaria indicada no existe.");
        }


        var query =
            db.MovimientosBancarios
                .AsNoTracking()
                .Where(x =>
                    x.CuentaBancariaId ==
                        filtro.CuentaBancariaId);


        // =====================================================
        // FECHA DESDE
        // =====================================================

        if (desdeUtc.HasValue)
        {
            query =
                query.Where(x =>
                    x.FechaUtc >=
                        desdeUtc.Value);
        }


        // =====================================================
        // FECHA HASTA
        // =====================================================

        if (hastaUtc.HasValue)
        {
            query =
                query.Where(x =>
                    x.FechaUtc <
                        hastaUtc.Value);
        }


        // =====================================================
        // TIPO
        // =====================================================

        if (filtro.Tipo.HasValue)
        {
            query =
                query.Where(x =>
                    x.Tipo ==
                        filtro.Tipo.Value);
        }


        // =====================================================
        // ORIGEN
        // =====================================================

        if (filtro.Origen.HasValue)
        {
            query =
                query.Where(x =>
                    x.Origen ==
                        filtro.Origen.Value);
        }


        // =====================================================
        // BÚSQUEDA
        // =====================================================

        if (!string.IsNullOrWhiteSpace(
            filtro.Buscar))
        {
            var buscar =
                filtro.Buscar.Trim();


            query =
                query.Where(x =>

                    // Concepto
                    EF.Functions.ILike(
                        x.Concepto,
                        $"%{buscar}%")

                    ||

                    // Referencia
                    (
                        x.ReferenciaExterna != null
                        &&
                        EF.Functions.ILike(
                            x.ReferenciaExterna,
                            $"%{buscar}%")
                    )

                    ||

                    // Observaciones
                    (
                        x.Observaciones != null
                        &&
                        EF.Functions.ILike(
                            x.Observaciones,
                            $"%{buscar}%")
                    )

                    ||

                    // Bebé
                    (
                        x.Pago != null
                        &&
                        (
                            EF.Functions.ILike(
                                x.Pago.Bebe.Nombre,
                                $"%{buscar}%")

                            ||

                            EF.Functions.ILike(
                                x.Pago.Bebe.Apellido,
                                $"%{buscar}%")
                        )
                    )

                    ||

                    // Proveedor
                    (
                        x.PagoProveedor != null
                        &&
                        (
                            EF.Functions.ILike(
                                x.PagoProveedor.Proveedor.RazonSocial,
                                $"%{buscar}%")

                            ||

                            (
                                x.PagoProveedor.Proveedor.NombreFantasia != null
                                &&
                                EF.Functions.ILike(
                                    x.PagoProveedor.Proveedor.NombreFantasia,
                                    $"%{buscar}%")
                            )

                            ||

                            (
                                x.PagoProveedor.Proveedor.Cuit != null
                                &&
                                EF.Functions.ILike(
                                    x.PagoProveedor.Proveedor.Cuit,
                                    $"%{buscar}%")
                            )
                        )
                    ));
        }


        // =====================================================
        // PROYECCIÓN
        // =====================================================

        var movimientos =
            await query

                .OrderByDescending(x =>
                    x.FechaUtc)

                .ThenByDescending(x =>
                    x.Id)

                .Select(x =>
                    new MovimientoBancarioDto(
                        x.Id,
                        x.CuentaBancariaId,
                        x.CuentaBancaria.Nombre,
                        x.CuentaBancaria.Banco.Nombre,
                        x.FechaUtc,
                        x.Tipo,
                        x.Origen,
                        x.Importe,
                        x.Concepto,
                        x.ReferenciaExterna,
                        x.Observaciones,
                        x.PagoId,

                        x.Pago == null
                            ? null
                            : (
                                x.Pago.Bebe.Nombre
                                + " "
                                + x.Pago.Bebe.Apellido
                            ).Trim()))

                .ToListAsync(ct);


        // =====================================================
        // TOTALES
        // =====================================================

        var totalIngresos =
            movimientos
                .Where(x =>
                    x.Tipo ==
                        TipoMovimientoBancario.Ingreso)
                .Sum(x =>
                    x.Importe);


        var totalEgresos =
            movimientos
                .Where(x =>
                    x.Tipo ==
                        TipoMovimientoBancario.Egreso)
                .Sum(x =>
                    x.Importe);


        return new ResumenMovimientosBancariosDto(
            totalIngresos,
            totalEgresos,
            totalIngresos - totalEgresos,
            movimientos.Count,
            movimientos);
    }


    // =========================================================
    // DETALLE
    // =========================================================

    public async Task<MovimientoBancarioDetalleDto?>
        ObtenerDetalleAsync(
            Guid movimientoId,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var movimiento =
            await db.MovimientosBancarios
                .AsNoTracking()

                // Cobro de bebé
                .Include(x =>
                    x.Pago)
                    .ThenInclude(x =>
                        x!.Bebe)

                .Include(x =>
                    x.Pago)
                    .ThenInclude(x =>
                        x!.Responsable)

                // Pago a proveedor
                .Include(x =>
                    x.PagoProveedor)
                    .ThenInclude(x =>
                        x!.Proveedor)

                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            movimientoId,
                    ct);


        if (movimiento is null)
        {
            return null;
        }


        // =====================================================
        // PAGO DE BEBÉ
        // =====================================================

        PagoBancarioDetalleDto? pagoDto =
            null;


        if (
            movimiento.PagoId.HasValue
            &&
            movimiento.Pago is not null)
        {
            var pago =
                movimiento.Pago;


            var imputaciones =
                await db.ImputacionesPago
                    .AsNoTracking()

                    .Include(x =>
                        x.Cuota)

                    .Where(x =>
                        x.PagoId ==
                            pago.Id)

                    .OrderBy(x =>
                        x.Cuota.Periodo)

                    .Select(x =>
                        new ImputacionPagoBancarioDto(
                            x.CuotaId,
                            x.Cuota.Periodo,
                            x.Importe))

                    .ToListAsync(ct);


            string? responsable =
                null;


            if (pago.Responsable is not null)
            {
                responsable =
                    $"{pago.Responsable.Nombre} " +
                    $"{pago.Responsable.Apellido}";

                responsable =
                    responsable.Trim();
            }


            var bebe =
                $"{pago.Bebe.Nombre} " +
                $"{pago.Bebe.Apellido}";


            pagoDto =
                new PagoBancarioDetalleDto(
                    pago.Id,
                    pago.BebeId,
                    bebe.Trim(),
                    responsable,
                    pago.FechaUtc,
                    pago.Importe,
                    pago.MedioPago,
                    pago.ReferenciaExterna,
                    pago.Observaciones,
                    imputaciones);
        }


        // =====================================================
        // PAGO A PROVEEDOR
        // =====================================================

        PagoProveedorBancarioDetalleDto?
            pagoProveedorDto =
                null;


        if (
            movimiento.PagoProveedorId.HasValue
            &&
            movimiento.PagoProveedor is not null)
        {
            var pagoProveedor =
                movimiento.PagoProveedor;


            pagoProveedorDto =
                new PagoProveedorBancarioDetalleDto(
                    pagoProveedor.Id,
                    pagoProveedor.ProveedorId,
                    pagoProveedor.Proveedor.RazonSocial,
                    pagoProveedor.FechaUtc,
                    pagoProveedor.Importe,
                    pagoProveedor.MedioPago,
                    pagoProveedor.Concepto,
                    pagoProveedor.NumeroComprobante,
                    pagoProveedor.ReferenciaExterna,
                    pagoProveedor.Observaciones);
        }


        // =====================================================
        // RESULTADO
        // =====================================================

        return new MovimientoBancarioDetalleDto(
            movimiento.Id,
            movimiento.FechaUtc,
            movimiento.Tipo,
            movimiento.Origen,
            movimiento.Importe,
            movimiento.Concepto,
            movimiento.ReferenciaExterna,
            movimiento.Observaciones,
            pagoDto,
            pagoProveedorDto);
    }


    // =========================================================
    // SALDO TÉCNICO
    // =========================================================

    public async Task<decimal>
        ObtenerSaldoAsync(
            Guid cuentaBancariaId,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        var cuenta =
            await db.CuentasBancarias
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            cuentaBancariaId,
                    ct)

            ?? throw new InvalidOperationException(
                "La cuenta bancaria indicada no existe.");


        var ingresos =
            await db.MovimientosBancarios
                .AsNoTracking()
                .Where(x =>
                    x.CuentaBancariaId ==
                        cuentaBancariaId
                    &&
                    x.Tipo ==
                        TipoMovimientoBancario.Ingreso)

                .SumAsync(
                    x => (decimal?)x.Importe,
                    ct)

            ?? 0m;


        var egresos =
            await db.MovimientosBancarios
                .AsNoTracking()
                .Where(x =>
                    x.CuentaBancariaId ==
                        cuentaBancariaId
                    &&
                    x.Tipo ==
                        TipoMovimientoBancario.Egreso)

                .SumAsync(
                    x => (decimal?)x.Importe,
                    ct)

            ?? 0m;


        return cuenta.SaldoInicial
               +
               ingresos
               -
               egresos;
    }


    // =========================================================
    // FECHAS
    // =========================================================

    private static DateTimeOffset
        ObtenerInicioDiaUtc(
            DateOnly fecha)
    {
        var local =
            DateTime.SpecifyKind(
                fecha.ToDateTime(
                    TimeOnly.MinValue),
                DateTimeKind.Unspecified);


        var zona =
            ObtenerZonaHorariaArgentina();


        var offset =
            zona.GetUtcOffset(
                local);


        return new DateTimeOffset(
                local,
                offset)
            .ToUniversalTime();
    }


    private static TimeZoneInfo
        ObtenerZonaHorariaArgentina()
    {
        try
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "America/Argentina/Buenos_Aires");
        }
        catch
        {
            return TimeZoneInfo
                .FindSystemTimeZoneById(
                    "Argentina Standard Time");
        }
    }
}