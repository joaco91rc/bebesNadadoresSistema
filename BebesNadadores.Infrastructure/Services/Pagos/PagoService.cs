using BebesNadadores.Application.Pagos;

using BebesNadadores.Application.Pagos.Dtos;

using BebesNadadores.Domain.Entities;

using BebesNadadores.Domain.Enums;
using BebesNadadores.Domain.Common;

using BebesNadadores.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using CajaEntidad = BebesNadadores.Domain.Entities.Caja;

namespace BebesNadadores.Infrastructure.Services.Pagos;


public sealed class PagoService(

    IDbContextFactory<AppDbContext> factory)

    : IPagoService

{

    // =========================================================

    // LISTADO GENERAL

    // =========================================================


    public async Task<IReadOnlyList<BebePagoResumenDto>>

        ListarBebesAsync(

            string? buscar = null,

            CancellationToken ct = default)

    {

        await using var db =

            await factory.CreateDbContextAsync(ct);


        var bebes =

            await db.Bebes

                .AsNoTracking()


                .Include(x =>

                    x.Responsables)


                    .ThenInclude(x =>

                        x.Responsable)


                .OrderBy(x =>

                    x.Apellido)


                .ThenBy(x =>

                    x.Nombre)


                .ToListAsync(ct);


        if (!string.IsNullOrWhiteSpace(

            buscar))

        {

            var texto =

                buscar.Trim();


            bebes =

                bebes

                    .Where(x =>

                        Contiene(

                            x.Nombre,

                            texto)

                        ||

                        Contiene(

                            x.Apellido,

                            texto)

                        ||

                        Contiene(

                            x.Dni,

                            texto)

                        ||

                        x.Responsables.Any(r =>

                            Contiene(

                                r.Responsable.Nombre,

                                texto)

                            ||

                            Contiene(

                                r.Responsable.Apellido,

                                texto)

                            ||

                            Contiene(

                                r.Responsable.Dni,

                                texto)))

                    .ToList();

        }


        var bebeIds =

            bebes

                .Select(x =>

                    x.Id)

                .ToList();


        var cuotas =

            await db.Cuotas

                .AsNoTracking()


                .Include(x =>

                    x.Imputaciones)


                .Include(x =>

                    x.Inscripcion)


                .Where(x =>

                    bebeIds.Contains(

                        x.Inscripcion.BebeId))


                .ToListAsync(ct);


        var pagos =

            await db.Pagos

                .AsNoTracking()


                .Include(x =>

                    x.Imputaciones)


                .Where(x =>

                    bebeIds.Contains(

                        x.BebeId))


                .ToListAsync(ct);


        var resultado =

            new List<BebePagoResumenDto>();


            var hoy =
    DateOnly.FromDateTime(
        DateTime.Today);

var periodoActual =
    new DateOnly(
        hoy.Year,
        hoy.Month,
        1);

        foreach (var bebe in bebes)

        {

           var cuotasBebe =
    cuotas
        .Where(x =>
            x.Inscripcion.BebeId ==
                bebe.Id
            &&
            x.Estado !=
                EstadoCuota.Anulada
            &&
            x.Periodo <=
                periodoActual)
        .ToList();


            var pagosBebe =

                pagos

                    .Where(x =>

                        x.BebeId ==

                            bebe.Id)

                    .ToList();


            var deuda =

                cuotasBebe

                    .Sum(x =>

                        Math.Max(

                            0,

                            x.Importe -

                            x.Imputaciones.Sum(i =>

                                i.Importe)));


            var saldoFavor =

                pagosBebe

                    .Sum(x =>

                        Math.Max(

                            0,

                            x.Importe -

                            x.Imputaciones.Sum(i =>

                                i.Importe)));


            var responsablePrincipal =

                bebe.Responsables

                    .FirstOrDefault(x =>

                        x.EsContactoPrincipal)

                ??

                bebe.Responsables

                    .FirstOrDefault();


            resultado.Add(

                new BebePagoResumenDto(

                    bebe.Id,


                    $"{bebe.Nombre} {bebe.Apellido}"

                        .Trim(),


                    bebe.Estado,


                    responsablePrincipal is null

                        ? null

                        : $"{responsablePrincipal.Responsable.Nombre} " +

                          $"{responsablePrincipal.Responsable.Apellido}",


                    deuda,


                    saldoFavor));

        }


        return resultado;

    }


    // =========================================================

    // CUENTA CORRIENTE

    // =========================================================


    public async Task<CuentaCorrienteBebeDto?>

        ObtenerCuentaCorrienteAsync(

            Guid bebeId,

            CancellationToken ct = default)

    {

        await using var db =

            await factory.CreateDbContextAsync(ct);


        var bebe =

            await db.Bebes

                .AsNoTracking()


                .Include(x =>

                    x.Responsables)


                    .ThenInclude(x =>

                        x.Responsable)


                .FirstOrDefaultAsync(

                    x =>

                        x.Id == bebeId,

                    ct);


        if (bebe is null)

            return null;


        var cuotas =

            await db.Cuotas

                .AsNoTracking()


                .Include(x =>

                    x.Imputaciones)


                .Include(x =>

                    x.Inscripcion)


                .Where(x =>

                    x.Inscripcion.BebeId ==

                        bebeId)


                .OrderByDescending(x =>

                    x.Periodo)


                .ToListAsync(ct);


        var pagos =

            await db.Pagos

                .AsNoTracking()


                .Include(x =>

                    x.Imputaciones)


                .Include(x =>

                    x.CuentaBancaria)


                    .ThenInclude(x =>

                        x!.Banco)


                .Where(x =>

                    x.BebeId ==

                        bebeId)


                .OrderByDescending(x =>

                    x.FechaUtc)


                .ToListAsync(ct);


        var hoy =

            ObtenerFechaArgentina();

            var periodoActual =
    new DateOnly(
        hoy.Year,
        hoy.Month,
        1);


        var cuotasDto =

            cuotas

                .Select(x =>

                {

                    var pagado =

                        x.Imputaciones

                            .Sum(i =>

                                i.Importe);


                    var saldo =

                        Math.Max(

                            0,

                            x.Importe -

                            pagado);


                    return new CuotaCuentaCorrienteDto(

                        x.Id,

                        x.Periodo,

                        x.FechaVencimiento,

                        x.Importe,

                        pagado,

                        saldo,

                        CalcularEstadoCuota(

                            x,

                            pagado,

                            hoy),

                        x.Descripcion);

                })

                .ToList();


        var pagosDto =

            pagos

                .Select(x =>

                {

                    var imputado =

                        x.Imputaciones

                            .Sum(i =>

                                i.Importe);


                    var nombreCuenta =

                        x.CuentaBancaria is null

                            ? null

                            : $"{x.CuentaBancaria.Banco.Nombre} · " +

                              $"{x.CuentaBancaria.Nombre}";


                    return new PagoCuentaCorrienteDto(

                        x.Id,

                        x.FechaUtc,

                        x.Importe,

                        imputado,

                        Math.Max(

                            0,

                            x.Importe -

                            imputado),

                        x.MedioPago,

                        x.CuentaBancariaId,

                        nombreCuenta,

                        x.CajaId,

                        x.ReferenciaExterna,

                        x.Observaciones);

                })

                .ToList();

var totalFacturado =
    cuotasDto
        .Where(x =>
            x.Estado !=
                EstadoCuota.Anulada
            &&
            x.Periodo <=
                periodoActual)
        .Sum(x =>
            x.Importe);


var deuda =
    cuotasDto
        .Where(x =>
            x.Estado !=
                EstadoCuota.Anulada
            &&
            x.Periodo <=
                periodoActual)
        .Sum(x =>
            x.Saldo);

        var totalPagado =

            pagosDto.Sum(x =>

                x.Importe);


        var saldoFavor =

            pagosDto.Sum(x =>

                x.SaldoNoImputado);


        var responsables =

            bebe.Responsables

                .Select(x =>

                    new ResponsablePagoDto(

                        x.ResponsableId,


                        $"{x.Responsable.Nombre} " +

                        $"{x.Responsable.Apellido}",


                        x.EsContactoPrincipal))

                .OrderByDescending(x =>

                    x.EsPrincipal)

                .ThenBy(x =>

                    x.NombreCompleto)

                .ToList();


        var responsablePrincipalId =

            responsables

                .FirstOrDefault(x =>

                    x.EsPrincipal)

                ?.Id

            ??

            responsables

                .FirstOrDefault()

                ?.Id;


        return new CuentaCorrienteBebeDto(

            bebe.Id,


            $"{bebe.Nombre} {bebe.Apellido}"

                .Trim(),


            totalFacturado,

            totalPagado,

            deuda,

            saldoFavor,

            responsablePrincipalId,

            responsables,

            cuotasDto,

            pagosDto);

    }


    // =========================================================

    // PREVISUALIZACIÓN

    // =========================================================


    public async Task<PreviewPagoDto>

        PrevisualizarAsync(

            RegistrarPagoDto dto,

            CancellationToken ct = default)

    {

        if (dto.Importe <= 0)

        {

            return new PreviewPagoDto(

                dto.Importe,

                0,

                0,

                []);

        }


        await using var db =

            await factory.CreateDbContextAsync(ct);


        var cuotas =

            await ObtenerCuotasImputablesAsync(

                db,

                dto.BebeId,

                ct);


        var restante =

            dto.Importe;


        var imputaciones =

            new List<ImputacionPreviewDto>();


        foreach (var cuota in cuotas)

        {

            if (restante <= 0)

                break;


            var saldoActual =

                ObtenerSaldoCuota(

                    cuota);


            if (saldoActual <= 0)

                continue;


            var aplicar =

                Math.Min(

                    restante,

                    saldoActual);


            imputaciones.Add(

                new ImputacionPreviewDto(

                    cuota.Id,

                    cuota.Periodo,

                    saldoActual,

                    aplicar,

                    saldoActual - aplicar));


            restante -=

                aplicar;

        }


        return new PreviewPagoDto(

            dto.Importe,

            dto.Importe - restante,

            restante,

            imputaciones);

    }


    // =========================================================

    // REGISTRAR PAGO

    // =========================================================


    public async Task<RegistrarPagoResultadoDto>

        RegistrarAsync(

            RegistrarPagoDto dto,

            Guid usuarioId,

            CancellationToken ct = default)

    {

        ValidarPago(

            dto);


        if (usuarioId ==

            Guid.Empty)

        {

            throw new InvalidOperationException(

                "No se pudo determinar el usuario que registra el pago.");

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

            // =================================================

            // BEBÉ

            // =================================================


            var bebe =

                await db.Bebes

                    .FirstOrDefaultAsync(

                        x =>

                            x.Id ==

                                dto.BebeId,

                        ct)

                ??

                throw new InvalidOperationException(

                    "El bebé indicado no existe.");


            // =================================================

            // RESPONSABLE

            // =================================================


            Guid? responsableId =

                dto.ResponsableId;


            if (responsableId.HasValue)

            {

                var pertenece =

                    await db.BebesResponsables

                        .AnyAsync(

                            x =>

                                x.BebeId ==

                                    bebe.Id

                                &&

                                x.ResponsableId ==

                                    responsableId.Value,

                            ct);


                if (!pertenece)

                {

                    throw new InvalidOperationException(

                        "El responsable seleccionado no pertenece al bebé.");

                }

            }

            else

            {

                responsableId =

                    await db.BebesResponsables


                        .Where(x =>

                            x.BebeId ==

                                bebe.Id)


                        .OrderByDescending(x =>

                            x.EsContactoPrincipal)


                        .Select(x =>

                            (Guid?)x.ResponsableId)


                        .FirstOrDefaultAsync(

                            ct);

            }


            // =================================================

            // ORIGEN / DESTINO DEL DINERO

            // =================================================


            CajaEntidad? caja =

                null;


            CuentaBancaria? cuentaBancaria =

                null;


            if (dto.MedioPago ==

                MedioPago.Efectivo)

            {

                var hoy =

                    ObtenerFechaArgentina();


                caja =

                    await db.Cajas

                        .FirstOrDefaultAsync(

                            x =>

                                x.FechaOperativa ==

                                    hoy,

                            ct);


                if (caja is null)

                {

                    throw new InvalidOperationException(

                        "No hay una caja abierta para el día de hoy. Debe abrirla antes de registrar un pago en efectivo.");

                }


                if (caja.Estado !=

                    EstadoCaja.Abierta)

                {

                    throw new InvalidOperationException(

                        "La caja del día ya se encuentra cerrada. No pueden registrarse pagos en efectivo.");

                }

            }

            else if (

                RequiereCuentaBancaria(

                    dto.MedioPago))

            {

                if (!dto.CuentaBancariaId.HasValue)

                {

                    throw new InvalidOperationException(

                        "Debe seleccionar la cuenta que recibió el pago.");

                }


                cuentaBancaria =

                    await db.CuentasBancarias


                        .Include(x =>

                            x.Banco)


                        .FirstOrDefaultAsync(

                            x =>

                                x.Id ==

                                    dto.CuentaBancariaId.Value

                                &&

                                x.Activa

                                &&

                                x.Banco.Activo,

                            ct)


                    ??

                    throw new InvalidOperationException(

                        "La cuenta seleccionada no existe o se encuentra inactiva.");

            }


            // =================================================

            // PAGO

            // =================================================


            var pago =

                new Pago

                {

                    Id =

                        Guid.NewGuid(),


                    BebeId =

                        bebe.Id,


                    ResponsableId =

                        responsableId,


                    FechaUtc =

                        DateTimeOffset.UtcNow,


                    Importe =

                        dto.Importe,


                    MedioPago =

                        dto.MedioPago,


                    ReferenciaExterna =

                        Normalizar(

                            dto.ReferenciaExterna),


                    Observaciones =

                        Normalizar(

                            dto.Observaciones),


                    CajaId =

                        caja?.Id,


                    CuentaBancariaId =

                        cuentaBancaria?.Id

                };


            db.Pagos.Add(

                pago);


            // =================================================

            // IMPUTAR A CUOTAS MÁS ANTIGUAS

            // =================================================


            var cuotas =

                await ObtenerCuotasImputablesAsync(

                    db,

                    bebe.Id,

                    ct);


            var restante =

                pago.Importe;


            foreach (var cuota in cuotas)

            {

                if (restante <= 0)

                    break;


                var saldoCuota =

                    ObtenerSaldoCuota(

                        cuota);


                if (saldoCuota <= 0)

                    continue;


                var importeAplicar =

                    Math.Min(

                        restante,

                        saldoCuota);


                var imputacion =

                    new ImputacionPago

                    {

                        PagoId =

                            pago.Id,


                        Pago =

                            pago,


                        CuotaId =

                            cuota.Id,


                        Cuota =

                            cuota,


                        Importe =

                            importeAplicar

                    };


                db.ImputacionesPago.Add(

                    imputacion);


                

                cuota.Imputaciones.Add(

                    imputacion);


                restante -=

                    importeAplicar;


                ActualizarEstadoCuota(

                    cuota);

            }


         // =================================================
// EFECTIVO -> MOVIMIENTO DE CAJA
// =================================================

if (caja is not null)
{
    var conceptoCuota =
        await db.ConceptosCaja
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                    ConceptosCajaSistema
                        .CuotaNatatorioMensualId
                    &&
                    x.Activo,
                ct)

        ??
        throw new InvalidOperationException(
            "No se encuentra configurado el concepto de caja " +
            "'Cuota natatorio mensual'.");


    var movimientoCaja =
        new MovimientoCaja
        {
            Id =
                Guid.NewGuid(),

            CajaId =
                caja.Id,

            FechaUtc =
                pago.FechaUtc,

            Tipo =
                conceptoCuota.Tipo,

            ConceptoCajaId =
                conceptoCuota.Id,

            Concepto =
                conceptoCuota.Nombre,

            Detalle =
                $"Pago de {bebe.Nombre} {bebe.Apellido}",

            Importe =
                pago.Importe,

            Referencia =
                pago.ReferenciaExterna,

            PagoId =
                pago.Id,

            Pago =
                pago
        };


    db.MovimientosCaja.Add(
        movimientoCaja);
}

            // =================================================

            // BANCO / BILLETERA -> MOVIMIENTO BANCARIO

            // =================================================


            if (cuentaBancaria is not null)

            {

                var movimientoBancario =

                    new MovimientoBancario

                    {

                        Id =

                            Guid.NewGuid(),


                        CuentaBancariaId =

                            cuentaBancaria.Id,


                        FechaUtc =

                            pago.FechaUtc,


                        Tipo =

                            TipoMovimientoBancario.Ingreso,


                        Origen =

                            OrigenMovimientoBancario.PagoBebe,


                        Importe =

                            pago.Importe,


                        Concepto =

                            $"Pago de cuotas - " +

                            $"{bebe.Nombre} {bebe.Apellido}",


                        PagoId =

                            pago.Id,


                        Pago =

                            pago,


                        ReferenciaExterna =

                            pago.ReferenciaExterna,


                        Observaciones =

                            pago.Observaciones

                    };


                db.MovimientosBancarios.Add(

                    movimientoBancario);

            }


            await db.SaveChangesAsync(

                ct);


            await transaction

                .CommitAsync(ct);


            var importeImputado =

                pago.Importe -

                restante;


            return new RegistrarPagoResultadoDto(

                pago.Id,

                pago.Importe,

                importeImputado,

                restante);

        }

        catch

        {

            await transaction

                .RollbackAsync(ct);


            throw;

        }

    }


    // =========================================================

    // APLICAR SALDO A FAVOR A CUOTAS NUEVAS

    // =========================================================


    public async Task AplicarSaldosAFavorAsync(

        Guid bebeId,

        CancellationToken ct = default)

    {

        await using var db =

            await factory.CreateDbContextAsync(ct);


        await using var transaction =

            await db.Database

                .BeginTransactionAsync(

                    System.Data.IsolationLevel.Serializable,

                    ct);


        try

        {

            var existeBebe =

                await db.Bebes

                    .AnyAsync(

                        x =>

                            x.Id ==

                                bebeId,

                        ct);


            if (!existeBebe)

            {

                throw new InvalidOperationException(

                    "El bebé indicado no existe.");

            }


            var pagos =

                await db.Pagos


                    .Include(x =>

                        x.Imputaciones)


                    .Where(x =>

                        x.BebeId ==

                            bebeId)


                    .OrderBy(x =>

                        x.FechaUtc)


                    .ToListAsync(ct);


            var cuotas =

                await ObtenerCuotasImputablesAsync(

                    db,

                    bebeId,

                    ct);


            foreach (var pago in pagos)

            {

                var disponible =

                    pago.Importe -

                    pago.Imputaciones.Sum(x =>

                        x.Importe);


                if (disponible <= 0)

                    continue;


                foreach (var cuota in cuotas)

                {

                    if (disponible <= 0)

                        break;


                    var saldoCuota =

                        ObtenerSaldoCuota(

                            cuota);


                    if (saldoCuota <= 0)

                        continue;


                    var aplicar =

                        Math.Min(

                            disponible,

                            saldoCuota);


                    var imputacionExistente =

                        pago.Imputaciones

                            .FirstOrDefault(x =>

                                x.CuotaId ==

                                    cuota.Id);


                    if (imputacionExistente is not null)

                    {

                        imputacionExistente.Importe +=

                            aplicar;

                    }

                    else

                    {

                        var nuevaImputacion =

                            new ImputacionPago

                            {

                                PagoId =

                                    pago.Id,


                                Pago =

                                    pago,


                                CuotaId =

                                    cuota.Id,


                                Cuota =

                                    cuota,


                                Importe =

                                    aplicar

                            };


                        db.ImputacionesPago.Add(

                            nuevaImputacion);


                        pago.Imputaciones.Add(

                            nuevaImputacion);


                        cuota.Imputaciones.Add(

                            nuevaImputacion);

                    }


                    disponible -=

                        aplicar;


                    ActualizarEstadoCuota(

                        cuota);

                }

            }


            await db.SaveChangesAsync(

                ct);


            await transaction

                .CommitAsync(ct);

        }

        catch

        {

            await transaction

                .RollbackAsync(ct);


            throw;

        }

    }


    // =========================================================

    // CUOTAS IMPUTABLES

    // =========================================================


    private static async Task<List<Cuota>>
    ObtenerCuotasImputablesAsync(
        AppDbContext db,
        Guid bebeId,
        CancellationToken ct)
{
    var hoy =
        DateOnly.FromDateTime(
            DateTime.Today);


    var periodoActual =
        new DateOnly(
            hoy.Year,
            hoy.Month,
            1);


    return await db.Cuotas

        .Include(x =>
            x.Imputaciones)

        .Include(x =>
            x.Inscripcion)

        .Where(x =>
            x.Inscripcion.BebeId ==
                bebeId
            &&
            x.Estado !=
                EstadoCuota.Pagada
            &&
            x.Estado !=
                EstadoCuota.Anulada
            &&
            x.Periodo <=
                periodoActual)

        .OrderBy(x =>
            x.Periodo)

        .ThenBy(x =>
            x.FechaVencimiento)

        .ToListAsync(ct);
}


    // =========================================================

    // ESTADO CUOTA

    // =========================================================


    private static void ActualizarEstadoCuota(

        Cuota cuota)

    {

        if (cuota.Estado ==

            EstadoCuota.Anulada)

        {

            return;

        }


        var pagado =

            cuota.Imputaciones

                .Sum(x =>

                    x.Importe);


        var hoy =

            ObtenerFechaArgentina();


        cuota.Estado =

            CalcularEstadoCuota(

                cuota,

                pagado,

                hoy);

    }


    private static EstadoCuota CalcularEstadoCuota(

        Cuota cuota,

        decimal importePagado,

        DateOnly hoy)

    {

        if (cuota.Estado ==

            EstadoCuota.Anulada)

        {

            return EstadoCuota.Anulada;

        }


        if (importePagado >=

            cuota.Importe)

        {

            return EstadoCuota.Pagada;

        }



        if (cuota.FechaVencimiento <

            hoy)

        {

            return EstadoCuota.Vencida;

        }


        if (importePagado > 0)

        {

            return EstadoCuota.Parcial;

        }


        return EstadoCuota.Pendiente;

    }


    private static decimal ObtenerSaldoCuota(

        Cuota cuota)

    {

        var pagado =

            cuota.Imputaciones

                .Sum(x =>

                    x.Importe);


        return Math.Max(

            0,

            cuota.Importe -

            pagado);

    }


    // =========================================================

    // VALIDACIONES

    // =========================================================


    private static void ValidarPago(

        RegistrarPagoDto dto)

    {

        if (dto.BebeId ==

            Guid.Empty)

        {

            throw new InvalidOperationException(

                "Debe seleccionar un bebé.");

        }


        if (dto.Importe <= 0)

        {

            throw new InvalidOperationException(

                "El importe del pago debe ser mayor a cero.");

        }


        if (

            RequiereCuentaBancaria(

                dto.MedioPago)

            &&

            !dto.CuentaBancariaId.HasValue)

        {

            throw new InvalidOperationException(

                "Debe seleccionar la cuenta que recibió el pago.");

        }

    }


    private static bool RequiereCuentaBancaria(

        MedioPago medio)

    {

        return medio is

            MedioPago.Transferencia

            or MedioPago.Tarjeta

            or MedioPago.MercadoPago;

    }


    // =========================================================

    // FECHA OPERATIVA ARGENTINA

    // =========================================================


    private static DateOnly ObtenerFechaArgentina()

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


    private static TimeZoneInfo ObtenerZonaHorariaArgentina()

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


    // =========================================================

    // HELPERS

    // =========================================================


    private static string? Normalizar(

        string? valor)

    {

        return string.IsNullOrWhiteSpace(

            valor)

            ? null

            : valor.Trim();

    }


    private static bool Contiene(

        string? texto,

        string buscar)

    {

        return

            !string.IsNullOrWhiteSpace(

                texto)

            &&

            texto.Contains(

                buscar,

                StringComparison.OrdinalIgnoreCase);

    }

}
