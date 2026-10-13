using BebesNadadores.Application.Proveedores;
using BebesNadadores.Application.Proveedores.Dtos;
using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CajaEntidad =    BebesNadadores.Domain.Entities.Caja;
namespace BebesNadadores.Infrastructure.Services.Proveedores;

public sealed class PagoProveedorService(
    IDbContextFactory<AppDbContext> factory)
    : IPagoProveedorService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<PagoProveedorDto>>
        ListarAsync(
            Guid proveedorId,
            CancellationToken ct = default)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);


        return await db.PagosProveedores
            .AsNoTracking()

            .Where(x =>
                x.ProveedorId ==
                    proveedorId)

            .OrderByDescending(x =>
                x.FechaUtc)

            .Select(x =>
                new PagoProveedorDto(
                    x.Id,
                    x.FechaUtc,
                    x.Importe,
                    x.MedioPago,
                    x.Concepto,
                    x.NumeroComprobante,
                    x.ReferenciaExterna,
                    x.Observaciones,
                    x.CajaId,
                    x.CuentaBancariaId,

                    x.CuentaBancaria == null
                        ? null
                        : x.CuentaBancaria.Nombre,

                    x.CuentaBancaria == null
                        ? null
                        : x.CuentaBancaria.Banco.Nombre))

            .ToListAsync(ct);
    }


    // =========================================================
    // REGISTRAR
    // =========================================================

    public async Task<Guid>
        RegistrarAsync(
            RegistrarPagoProveedorDto dto,
            Guid usuarioId,
            CancellationToken ct = default)
    {
        Validar(dto);


        await using var db =
            await factory.CreateDbContextAsync(ct);


        await using var transaction =
            await db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable,
                ct);


        // =====================================================
        // PROVEEDOR
        // =====================================================

        var proveedor =
            await db.Proveedores
                .FirstOrDefaultAsync(
                    x =>
                        x.Id ==
                            dto.ProveedorId,
                    ct)

            ?? throw new InvalidOperationException(
                "El proveedor indicado no existe.");


        if (!proveedor.Activo)
        {
            throw new InvalidOperationException(
                "No se pueden registrar pagos a un proveedor inactivo.");
        }


        // =====================================================
        // FECHA
        // =====================================================

        var ahoraUtc =
            DateTimeOffset.UtcNow;


        var hoyArgentina =
            DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(
                    ahoraUtc,
                    ObtenerZonaHorariaArgentina())
                .DateTime);


        CajaEntidad? caja =
            null;


        CuentaBancaria? cuenta =
            null;


        // =====================================================
        // EFECTIVO -> CAJA
        // =====================================================

        if (dto.MedioPago ==
            MedioPago.Efectivo)
        {
            caja =
                await db.Cajas
                    .FirstOrDefaultAsync(
                        x =>
                            x.Estado ==
                                EstadoCaja.Abierta
                            &&
                            x.FechaOperativa ==
                                hoyArgentina,
                        ct);


            if (caja is null)
            {
                throw new InvalidOperationException(
                    "Para registrar un pago en efectivo debe existir una caja abierta para hoy.");
            }
        }

        // =====================================================
        // NO EFECTIVO -> CUENTA BANCARIA
        // =====================================================

        else
        {
            if (!dto.CuentaBancariaId.HasValue)
            {
                throw new InvalidOperationException(
                    "Debe seleccionar la cuenta desde la que se realizó el pago.");
            }


            cuenta =
                await db.CuentasBancarias
                    .Include(x =>
                        x.Banco)

                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                dto.CuentaBancariaId.Value,
                        ct)

                ?? throw new InvalidOperationException(
                    "La cuenta bancaria indicada no existe.");


            if (!cuenta.Activa)
            {
                throw new InvalidOperationException(
                    "La cuenta bancaria seleccionada se encuentra inactiva.");
            }
        }


        // =====================================================
        // PAGO
        // =====================================================

        var pago =
            new PagoProveedor
            {
                Id =
                    Guid.NewGuid(),

                ProveedorId =
                    proveedor.Id,

                FechaUtc =
                    ahoraUtc,

                Importe =
                    dto.Importe,

                MedioPago =
                    dto.MedioPago,

                Concepto =
                    dto.Concepto.Trim(),

                NumeroComprobante =
                    Normalizar(
                        dto.NumeroComprobante),

                ReferenciaExterna =
                    Normalizar(
                        dto.ReferenciaExterna),

                Observaciones =
                    Normalizar(
                        dto.Observaciones),

                UsuarioRegistroId =
                    usuarioId,

                CajaId =
                    caja?.Id,

                CuentaBancariaId =
                    cuenta?.Id
            };


        db.PagosProveedores.Add(
            pago);


        // =====================================================
        // MOVIMIENTO DE CAJA
        // =====================================================

        if (caja is not null)
        {
           var conceptoCaja =
    await db.ConceptosCaja
        .FirstOrDefaultAsync(
            x =>
                x.Id ==
                    ConceptosCajaSistema.PagoProveedorId,
            ct);


if (conceptoCaja is null)
{
    conceptoCaja =
        new ConceptoCaja
        {
            Id =
                ConceptosCajaSistema.PagoProveedorId,

            Nombre =
                ConceptosCajaSistema.PagoProveedor,

            Tipo =
                TipoMovimientoCaja.Egreso,

            Activo =
                true,

            Orden =
                0
        };


    db.ConceptosCaja.Add(
        conceptoCaja);
}
else
{
    /*
     * Es un concepto interno del sistema.
     * Si por algún motivo quedó inactivo,
     * lo reactivamos.
     */
    conceptoCaja.Nombre =
        ConceptosCajaSistema.PagoProveedor;

    conceptoCaja.Tipo =
        TipoMovimientoCaja.Egreso;

    conceptoCaja.Activo =
        true;
}


            db.MovimientosCaja.Add(
                new MovimientoCaja
                {
                    Id =
                        Guid.NewGuid(),

                    CajaId =
                        caja.Id,

                    FechaUtc =
                        ahoraUtc,

                    Tipo =
                        TipoMovimientoCaja.Egreso,

                    ConceptoCajaId =
                        conceptoCaja.Id,

                    Concepto =
                        conceptoCaja.Nombre,

                    Detalle =
                        $"{proveedor.RazonSocial} · {dto.Concepto.Trim()}",

                    Importe =
                        dto.Importe,

                    Referencia =
                        Normalizar(
                            dto.ReferenciaExterna)
                        ??
                        Normalizar(
                            dto.NumeroComprobante),

                    PagoProveedorId =
                        pago.Id,

                    PagoProveedor =
                        pago
                });
        }


        // =====================================================
        // MOVIMIENTO BANCARIO
        // =====================================================

        if (cuenta is not null)
        {
            db.MovimientosBancarios.Add(
                new MovimientoBancario
                {
                    Id =
                        Guid.NewGuid(),

                    CuentaBancariaId =
                        cuenta.Id,

                    FechaUtc =
                        ahoraUtc,

                    Tipo =
                        TipoMovimientoBancario.Egreso,

                    Origen =
                        OrigenMovimientoBancario.PagoProveedor,

                    Importe =
                        dto.Importe,

                    Concepto =
                        $"Pago a proveedor - {proveedor.RazonSocial}: {dto.Concepto.Trim()}",

                    PagoProveedorId =
                        pago.Id,

                    PagoProveedor =
                        pago,

                    ReferenciaExterna =
                        Normalizar(
                            dto.ReferenciaExterna)
                        ??
                        Normalizar(
                            dto.NumeroComprobante),

                    Observaciones =
                        Normalizar(
                            dto.Observaciones)
                });
        }


        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);


        return pago.Id;
    }


    // =========================================================
    // VALIDACIÓN
    // =========================================================

    private static void Validar(
        RegistrarPagoProveedorDto dto)
    {
        if (dto.ProveedorId ==
            Guid.Empty)
        {
            throw new InvalidOperationException(
                "Debe indicar el proveedor.");
        }


        if (dto.Importe <= 0)
        {
            throw new InvalidOperationException(
                "El importe debe ser mayor a cero.");
        }


        if (string.IsNullOrWhiteSpace(
            dto.Concepto))
        {
            throw new InvalidOperationException(
                "Debe indicar el concepto del pago.");
        }
    }


    private static string? Normalizar(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
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