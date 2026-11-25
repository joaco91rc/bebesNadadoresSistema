using BebesNadadores.Application.Pagos;

using BebesNadadores.Application.Pagos.Dtos;

using BebesNadadores.Domain.Entities;

using BebesNadadores.Domain.Enums;
using BebesNadadores.Domain.Common;

using BebesNadadores.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using CajaEntidad = BebesNadadores.Domain.Entities.Caja;

using static BebesNadadores.Infrastructure.Services.Pagos.RegistradorCobros;

namespace BebesNadadores.Infrastructure.Services.Pagos;


public sealed class PagoService(

    IDbContextFactory<AppDbContext> factory,

    RegistradorCobros registradorCobros)

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

                        x.BebeId)
                        &&
                        !x.AnuladoUtc.HasValue)


                .ToListAsync(ct);


        var pagosReservaIds = (await db.ReservasVacantesCiclo.AsNoTracking()
            .Where(x => bebeIds.Contains(x.BebeId) && x.Estado != EstadoReservaVacanteCiclo.Cancelada)
            .Select(x => x.PagoId).ToListAsync(ct)).ToHashSet();

        var resultado =

            new List<BebePagoResumenDto>();


            var hoy = ObtenerFechaArgentina();

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
                    .Where(x => x.FechaVencimiento < hoy)
                    .Sum(x => Math.Max(
                        0m,
                        x.Importe - x.Imputaciones.Sum(i => i.Importe)));


            // El anticipo de un ciclo FUTURO no es saldo libre para pagar 2026.
            var saldoFavor =

                pagosBebe.Where(x => !pagosReservaIds.Contains(x.Id))

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


                .Include(x => x.Bonificaciones)
                .Include(x =>
                    x.Reactivaciones)


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


                    /*
                     * Una cuota anulada no se cobra: no tiene saldo
                     * pendiente aunque no se haya pagado nada.
                     */
                    var saldo =
                        x.Estado == EstadoCuota.Anulada
                            ? 0
                            : Math.Max(
                                0,
                                x.Importe -
                                pagado);


                    var ultimaReactivacion =
                        x.Reactivaciones
                            .OrderByDescending(r => r.FechaUtc)
                            .FirstOrDefault();


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

                        x.Descripcion,
                        x.MotivoAnulacion,
                        ultimaReactivacion?.FechaUtc,
                        ultimaReactivacion?.Motivo,
                        x.Bonificaciones.Where(b => b.AnuladaUtc == null).Sum(b => b.Importe),
                        x.Importe + x.Bonificaciones.Where(b => b.AnuladaUtc == null).Sum(b => b.Importe));

                })

                .ToList();


        /*
         * Quién registró / anuló cada pago (auditoría).
         */
        var idsUsuarios =
            pagos
                .SelectMany(x => new[] { x.CreadoPorUsuarioId, x.AnuladoPorUsuarioId })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

        var nombresUsuarios =
            idsUsuarios.Count == 0
                ? new Dictionary<Guid, string>()
                : await db.Users
                    .AsNoTracking()
                    .Where(x => idsUsuarios.Contains(x.Id))
                    .ToDictionaryAsync(
                        x => x.Id,
                        x => x.NombreCompleto,
                        ct);


        var pagosReservaIdsBebe = (await db.ReservasVacantesCiclo.AsNoTracking()
            .Where(x => x.BebeId == bebeId && x.Estado != EstadoReservaVacanteCiclo.Cancelada)
            .Select(x => x.PagoId).ToListAsync(ct)).ToHashSet();

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

                        x.Anulado
                            ? 0
                            : Math.Max(
                                0,
                                x.Importe -
                                imputado),

                        x.MedioPago,

                        x.CuentaBancariaId,

                        nombreCuenta,

                        x.CajaId,

                        x.ReferenciaExterna,

                        x.Observaciones,
                        x.Anulado,
                        x.MotivoAnulacion,
                        x.AnuladoUtc,
                        x.CreadoPorUsuarioId.HasValue
                            ? nombresUsuarios.GetValueOrDefault(x.CreadoPorUsuarioId.Value)
                            : null,
                        x.AnuladoPorUsuarioId.HasValue
                            ? nombresUsuarios.GetValueOrDefault(x.AnuladoPorUsuarioId.Value)
                            : null);

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
            x.Estado != EstadoCuota.Anulada &&
            x.FechaVencimiento < hoy)
        .Sum(x => x.Saldo);

        var totalPagado =
            pagosDto
                .Where(x =>
                    !x.Anulado)
                .Sum(x =>
                    x.Importe);


         // Crédito reservado para el próximo ciclo: visible en los pagos,
         // pero NO disponible para otras cuotas.
         var saldoFavor =
            pagosDto.Where(x => !pagosReservaIdsBebe.Contains(x.Id))
                .Sum(x => x.SaldoNoImputado);


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


        FiltrarCuotaObjetivo(cuotas, dto.CuotaIdObjetivo);

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


    public Task<RegistrarPagoResultadoDto> RegistrarAsync(
        RegistrarPagoDto dto,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => RegistrarInternoAsync(
                dto,
                usuarioId,
                ct),
            ct);
    }


    private async Task<RegistrarPagoResultadoDto> RegistrarInternoAsync(

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


        // Sin try/catch con Rollback manual: si la transacción ya está
        // abortada, el Rollback explota y tapa el error real. El
        // "await using" hace el rollback solo.
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
        // COBRO (caja / banco / imputación)
        // =================================================

        var cuotas =
            await ObtenerCuotasImputablesAsync(
                db,
                bebe.Id,
                ct);

        FiltrarCuotaObjetivo(cuotas, dto.CuotaIdObjetivo);

        var cobro =
            await registradorCobros.RegistrarAsync(
                db,
                new NuevoCobro(
                    Bebe: bebe,
                    ResponsableId: responsableId,
                    Importe: dto.Importe,
                    MedioPago: dto.MedioPago,
                    CuentaBancariaId: dto.CuentaBancariaId,
                    ReferenciaExterna: dto.ReferenciaExterna,
                    Observaciones: dto.Observaciones,
                    CuotasAImputar: cuotas,
                    DetalleMovimiento: $"Pago de {bebe.Nombre} {bebe.Apellido}",
                    UsuarioId: usuarioId),
                ct);

        var pago =
            cobro.Pago;

        var restante =
            cobro.Restante;
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


    // =========================================================

    // APLICAR SALDO A FAVOR A CUOTAS NUEVAS

    // =========================================================


    public Task AplicarSaldosAFavorAsync(
        Guid bebeId,
        CancellationToken ct = default)
    {
        return ReintentoTransaccional.EjecutarAsync(
            () => AplicarSaldosAFavorInternoAsync(
                bebeId,
                ct),
            ct);
    }


    private async Task AplicarSaldosAFavorInternoAsync(

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


        // Sin try/catch con Rollback manual: si la transacción ya está
        // abortada, el Rollback explota y tapa el error real. El
        // "await using" hace el rollback solo.
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

                        bebeId
                        &&
                        !x.AnuladoUtc.HasValue)


                .OrderBy(x =>

                    x.FechaUtc)


                .ToListAsync(ct);


        var cuotas =

            await ObtenerCuotasImputablesAsync(

                db,

                bebeId,

                ct);


        // Los anticipos de reserva nunca participan de la imputación genérica.
        // Sólo pueden cubrir la PRIMERA cuota del ciclo correspondiente.
        var reservasCiclo = await db.ReservasVacantesCiclo
            .Include(x => x.CicloLectivo)
            .Where(x => x.BebeId == bebeId && x.Estado != EstadoReservaVacanteCiclo.Cancelada)
            .ToListAsync(ct);
        var pagosReservaIds = reservasCiclo.Select(x => x.PagoId).ToHashSet();
        var hoyArgentina = BebesNadadores.Domain.Common.FechaArgentina.Hoy;

        foreach (var reserva in reservasCiclo)
        {
            // El anticipo no debe aplicarse a una inscripción anterior.
            if (!reserva.InscripcionId.HasValue || hoyArgentina < reserva.CicloLectivo.FechaInicio)
                continue;
            var primerPeriodo = new DateOnly(reserva.CicloLectivo.FechaInicio.Year,
                reserva.CicloLectivo.FechaInicio.Month, 1);
            var primeraCuota = cuotas.FirstOrDefault(x =>
                x.InscripcionId == reserva.InscripcionId.Value && x.Periodo == primerPeriodo);
            var pagoReserva = pagos.FirstOrDefault(x => x.Id == reserva.PagoId);
            if (primeraCuota is null || pagoReserva is null) continue;

            var pendiente = pagoReserva.Importe - pagoReserva.Imputaciones.Sum(x => x.Importe);
            var saldo = ObtenerSaldoCuota(primeraCuota);
            if (pendiente <= 0 || saldo <= 0) continue;
            var aplicar = Math.Min(pendiente, saldo);
            var anterior = pagoReserva.Imputaciones.FirstOrDefault(x => x.CuotaId == primeraCuota.Id);
            if (anterior is not null)
            {
                anterior.Importe += aplicar;
            }
            else
            {
                var imputacion = new ImputacionPago {
                    PagoId = pagoReserva.Id, Pago = pagoReserva,
                    CuotaId = primeraCuota.Id, Cuota = primeraCuota,
                    Importe = aplicar
                };
                db.ImputacionesPago.Add(imputacion);
                AgregarSiFalta(pagoReserva.Imputaciones, imputacion);
                AgregarSiFalta(primeraCuota.Imputaciones, imputacion);
            }
            ActualizarEstadoCuota(primeraCuota);
            if (pendiente - aplicar <= 0)
                reserva.Estado = EstadoReservaVacanteCiclo.Aplicada;
        }

        foreach (var pago in pagos.Where(x => !pagosReservaIds.Contains(x.Id)))

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


                    /*
                     * EF ya hace el fix-up de navegaciones al
                     * agregar; evitamos duplicarla en memoria
                     * (contaba doble y marcaba "Pagada").
                     */
                    AgregarSiFalta(

                        pago.Imputaciones,

                        nuevaImputacion);


                    AgregarSiFalta(

                        cuota.Imputaciones,

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


    // =========================================================

    // CUOTAS IMPUTABLES

    // =========================================================


    /*
     * Orden de imputación: de la más vieja a la más nueva.
     * Primero lo vencido y el mes actual; si sobra, las cuotas de
     * meses siguientes que YA estén generadas (pago adelantado).
     * Recién después de eso queda saldo a favor.
     *
     * La deuda que se muestra sigue siendo solo hasta el mes actual.
     */
    private static async Task<List<Cuota>>
    ObtenerCuotasImputablesAsync(
        AppDbContext db,
        Guid bebeId,
        CancellationToken ct)
{
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
                EstadoCuota.Anulada)

        .OrderBy(x =>
            x.Periodo)

        .ThenBy(x =>
            x.FechaVencimiento)

        .ToListAsync(ct);
}




    private static void FiltrarCuotaObjetivo(List<Cuota> cuotas, Guid? cuotaId)
    {
        if (!cuotaId.HasValue) return;
        var seleccionada = cuotas.FirstOrDefault(x => x.Id == cuotaId.Value
            && x.Importe > x.Imputaciones.Sum(i => i.Importe));
        if (seleccionada is null)
            throw new InvalidOperationException("La cuota elegida no existe, está anulada o ya no tiene saldo pendiente.");
        cuotas.Clear();
        cuotas.Add(seleccionada);
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

            dto.MedioPago

                .RequiereCuentaBancaria()

            &&

            !dto.CuentaBancariaId.HasValue)

        {

            throw new InvalidOperationException(

                "Debe seleccionar la cuenta que recibió el pago.");

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
