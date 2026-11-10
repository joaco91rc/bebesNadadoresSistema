using System.Data;
using System.Globalization;
using BebesNadadores.Application.Anulaciones;
using BebesNadadores.Application.Pagos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Pagos;
using Microsoft.EntityFrameworkCore;
using CajaEntidad = BebesNadadores.Domain.Entities.Caja;

namespace BebesNadadores.Infrastructure.Services.Anulaciones;

/*
 * =============================================================
 * ANULACIONES
 * =============================================================
 *
 * Reglas comunes:
 *
 * 1. Nada se borra. Lo anulado queda con AnuladoUtc, usuario y
 *    motivo (obligatorio).
 *
 * 2. El dinero se revierte con un CONTRA-ASIENTO:
 *    - Efectivo: movimiento inverso en la caja ABIERTA DE HOY
 *      (el día en que se anula). Si el original era de una caja
 *      ya cerrada, esa caja no cambia sus números. Si quedó
 *      abierta la caja de un día anterior, hay que cerrarla
 *      primero: la reversa nunca se registra en otro día.
 *    - Banco: movimiento inverso en la misma cuenta, con fecha
 *      de hoy.
 *    El contra-asiento apunta al original (AnulaMovimientoId).
 *
 * 3. Una cuota con pagos imputados NO se puede anular: primero
 *    se anula el pago (la cuota vuelve a Pendiente/Vencida) y
 *    recién ahí se anula la cuota. Así nunca aparece un saldo a
 *    favor que no existe.
 *
 * 4. Transacción Serializable con reintento (ReintentoTransaccional).
 */
public sealed class AnulacionService(
    IDbContextFactory<AppDbContext> factory,
    IPagoService pagoService)
    : IAnulacionService
{
    private const int LargoMaximoMotivo = 300;

    private static readonly CultureInfo CulturaArgentina =
        CultureInfo.GetCultureInfo("es-AR");


    // =========================================================
    // PAGO DE UNA FAMILIA
    // =========================================================

    public async Task AnularPagoAsync(
        Guid pagoId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var motivoNormalizado =
            ValidarMotivo(motivo);

        ValidarUsuario(usuarioId);

        var bebeId =
            await ReintentoTransaccional.EjecutarAsync(
                () => AnularPagoInternoAsync(
                    pagoId,
                    motivoNormalizado,
                    usuarioId,
                    ct),
                ct);

        /*
         * Si el bebé tenía otros pagos con saldo a favor, ahora
         * pueden cubrir las cuotas que quedaron liberadas.
         */
        await pagoService.AplicarSaldosAFavorAsync(
            bebeId,
            ct);
    }


    private async Task<Guid> AnularPagoInternoAsync(
        Guid pagoId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        var pago =
            await db.Pagos
                .FirstOrDefaultAsync(
                    x => x.Id == pagoId,
                    ct)
            ?? throw new InvalidOperationException(
                "El pago indicado no existe.");

        if (pago.Anulado)
        {
            throw new InvalidOperationException(
                "El pago ya se encuentra anulado.");
        }

        // -----------------------------------------------------
        // 1. Liberar las cuotas que cancelaba
        //    (vuelven a Pendiente / Parcial / Vencida)
        // -----------------------------------------------------

        var cuotas =
            await db.Cuotas
                .Include(x => x.Imputaciones)
                .Where(x =>
                    x.Imputaciones.Any(i =>
                        i.PagoId == pago.Id))
                .ToListAsync(ct);

        foreach (var cuota in cuotas)
        {
            var imputacionesDelPago =
                cuota.Imputaciones
                    .Where(x => x.PagoId == pago.Id)
                    .ToList();

            foreach (var imputacion in imputacionesDelPago)
            {
                db.ImputacionesPago.Remove(imputacion);

                cuota.Imputaciones.Remove(imputacion);
            }

            RegistradorCobros.ActualizarEstadoCuota(cuota);

            cuota.ModificadoUtc = DateTimeOffset.UtcNow;
            cuota.ModificadoPorUsuarioId = usuarioId;
        }

        // -----------------------------------------------------
        // 2. Revertir el dinero
        // -----------------------------------------------------

        var movimientosCaja =
            await db.MovimientosCaja
                .Where(x =>
                    x.PagoId == pago.Id
                    && !x.AnuladoUtc.HasValue
                    && !x.AnulaMovimientoId.HasValue)
                .ToListAsync(ct);

        foreach (var movimiento in movimientosCaja)
        {
            await RevertirMovimientoCajaAsync(
                db,
                movimiento,
                motivo,
                usuarioId,
                ct);
        }

        var movimientosBanco =
            await db.MovimientosBancarios
                .Where(x =>
                    x.PagoId == pago.Id
                    && !x.AnuladoUtc.HasValue
                    && !x.AnulaMovimientoId.HasValue)
                .ToListAsync(ct);

        foreach (var movimiento in movimientosBanco)
        {
            RevertirMovimientoBancario(
                db,
                movimiento,
                motivo,
                usuarioId);
        }

        // -----------------------------------------------------
        // 3. Marcar el pago
        // -----------------------------------------------------

        pago.AnuladoUtc = DateTimeOffset.UtcNow;
        pago.AnuladoPorUsuarioId = usuarioId;
        pago.MotivoAnulacion = motivo;
        pago.ModificadoUtc = DateTimeOffset.UtcNow;
        pago.ModificadoPorUsuarioId = usuarioId;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return pago.BebeId;
    }


    // =========================================================
    // CUOTA
    // =========================================================

    public async Task AnularCuotaAsync(
        Guid cuotaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var motivoNormalizado =
            ValidarMotivo(motivo);

        ValidarUsuario(usuarioId);

        /*
         * Ya no hace falta AplicarSaldosAFavorAsync: una cuota con
         * pagos no se puede anular, así que no se libera dinero.
         */
        await ReintentoTransaccional.EjecutarAsync(
            () => AnularCuotaInternoAsync(
                cuotaId,
                motivoNormalizado,
                usuarioId,
                ct),
            ct);
    }


    private async Task<Guid> AnularCuotaInternoAsync(
        Guid cuotaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        var cuota =
            await db.Cuotas
                .Include(x => x.Imputaciones)
                .Include(x => x.Inscripcion)
                .FirstOrDefaultAsync(
                    x => x.Id == cuotaId,
                    ct)
            ?? throw new InvalidOperationException(
                "La cuota indicada no existe.");

        if (cuota.Estado == EstadoCuota.Anulada)
        {
            throw new InvalidOperationException(
                "La cuota ya se encuentra anulada.");
        }

        /*
         * Al anular un pago se borran sus imputaciones, así que
         * cualquier imputación que quede acá es de un pago vigente.
         */
        var importePagado =
            cuota.Imputaciones.Sum(x => x.Importe);

        if (importePagado > 0)
        {
            throw new InvalidOperationException(
                $"La cuota tiene {importePagado.ToString("C2", CulturaArgentina)} cobrados. " +
                "Para anularla, primero anulá el pago desde el Historial de pagos.");
        }

        cuota.Estado = EstadoCuota.Anulada;
        cuota.AnuladaUtc = DateTimeOffset.UtcNow;
        cuota.AnuladaPorUsuarioId = usuarioId;
        cuota.MotivoAnulacion = motivo;
        cuota.ModificadoUtc = DateTimeOffset.UtcNow;
        cuota.ModificadoPorUsuarioId = usuarioId;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return cuota.Inscripcion.BebeId;
    }


    // =========================================================
    // REACTIVAR CUOTA (anulada por error)
    // =========================================================

    public async Task ReactivarCuotaAsync(
        Guid cuotaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var motivoNormalizado =
            ValidarMotivo(
                motivo,
                "la reactivación");

        ValidarUsuario(usuarioId);

        var bebeId =
            await ReintentoTransaccional.EjecutarAsync(
                () => ReactivarCuotaInternoAsync(
                    cuotaId,
                    motivoNormalizado,
                    usuarioId,
                    ct),
                ct);

        /*
         * Si el bebé tiene saldo a favor (por ejemplo, cuotas que se
         * anularon con pagos antes de existir el bloqueo), se aplica
         * a la cuota reactivada.
         */
        await pagoService.AplicarSaldosAFavorAsync(
            bebeId,
            ct);
    }


    private async Task<Guid> ReactivarCuotaInternoAsync(
        Guid cuotaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        var cuota =
            await db.Cuotas
                .Include(x => x.Imputaciones)
                .Include(x => x.Inscripcion)
                .FirstOrDefaultAsync(
                    x => x.Id == cuotaId,
                    ct)
            ?? throw new InvalidOperationException(
                "La cuota indicada no existe.");

        if (cuota.Estado != EstadoCuota.Anulada)
        {
            throw new InvalidOperationException(
                "La cuota no está anulada.");
        }

        // -----------------------------------------------------
        // 1. La inscripción tenía que estar vigente ese mes
        // -----------------------------------------------------

        var inscripcion =
            cuota.Inscripcion;

        if (inscripcion.Estado == EstadoInscripcion.Cancelada)
        {
            throw new InvalidOperationException(
                "No se puede reactivar: la inscripción de esta cuota fue cancelada.");
        }

        if (inscripcion.FechaFin.HasValue)
        {
            var periodoFin =
                new DateOnly(
                    inscripcion.FechaFin.Value.Year,
                    inscripcion.FechaFin.Value.Month,
                    1);

            if (cuota.Periodo > periodoFin)
            {
                throw new InvalidOperationException(
                    "No se puede reactivar: la cuota es de un mes posterior " +
                    "a la baja o al fin de la inscripción.");
            }
        }

        // -----------------------------------------------------
        // 2. Una sola cuota vigente por bebé y por mes
        //    (misma regla que el generador)
        // -----------------------------------------------------

        var hayOtraVigente =
            await db.Cuotas
                .AnyAsync(
                    x =>
                        x.Id != cuota.Id
                        && x.Inscripcion.BebeId == inscripcion.BebeId
                        && x.Periodo == cuota.Periodo
                        && x.Estado != EstadoCuota.Anulada,
                    ct);

        if (hayOtraVigente)
        {
            throw new InvalidOperationException(
                "No se puede reactivar: el bebé ya tiene otra cuota vigente para ese mes.");
        }

        // -----------------------------------------------------
        // 3. Auditoría: foto de la anulación que se deshace
        // -----------------------------------------------------

        db.CuotasReactivaciones.Add(
            new CuotaReactivacion
            {
                Id = Guid.NewGuid(),
                CuotaId = cuota.Id,
                FechaUtc = DateTimeOffset.UtcNow,
                UsuarioId = usuarioId,
                Motivo = motivo,
                AnuladaUtcAnterior = cuota.AnuladaUtc,
                AnuladaPorUsuarioIdAnterior = cuota.AnuladaPorUsuarioId,
                MotivoAnulacionAnterior = cuota.MotivoAnulacion
            });

        // -----------------------------------------------------
        // 4. Reactivar
        // -----------------------------------------------------

        cuota.Estado = EstadoCuota.Pendiente;
        cuota.AnuladaUtc = null;
        cuota.AnuladaPorUsuarioId = null;
        cuota.MotivoAnulacion = null;

        RegistradorCobros.ActualizarEstadoCuota(cuota);

        cuota.ModificadoUtc = DateTimeOffset.UtcNow;
        cuota.ModificadoPorUsuarioId = usuarioId;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return inscripcion.BebeId;
    }


    // =========================================================
    // MOVIMIENTO MANUAL DE CAJA
    // =========================================================

    public Task AnularMovimientoCajaAsync(
        Guid movimientoCajaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var motivoNormalizado =
            ValidarMotivo(motivo);

        ValidarUsuario(usuarioId);

        return ReintentoTransaccional.EjecutarAsync(
            () => AnularMovimientoCajaInternoAsync(
                movimientoCajaId,
                motivoNormalizado,
                usuarioId,
                ct),
            ct);
    }


    private async Task AnularMovimientoCajaInternoAsync(
        Guid movimientoCajaId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        var movimiento =
            await db.MovimientosCaja
                .FirstOrDefaultAsync(
                    x => x.Id == movimientoCajaId,
                    ct)
            ?? throw new InvalidOperationException(
                "El movimiento indicado no existe.");

        if (movimiento.PagoId.HasValue)
        {
            throw new InvalidOperationException(
                "Este movimiento corresponde a un cobro. " +
                "Anulalo desde la pantalla de Pagos.");
        }

        if (movimiento.PagoProveedorId.HasValue)
        {
            throw new InvalidOperationException(
                "Este movimiento corresponde a un pago a proveedor. " +
                "Anulalo desde la pantalla del proveedor.");
        }

        await RevertirMovimientoCajaAsync(
            db,
            movimiento,
            motivo,
            usuarioId,
            ct);

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }


    // =========================================================
    // PAGO A PROVEEDOR
    // =========================================================

    public Task AnularPagoProveedorAsync(
        Guid pagoProveedorId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var motivoNormalizado =
            ValidarMotivo(motivo);

        ValidarUsuario(usuarioId);

        return ReintentoTransaccional.EjecutarAsync(
            () => AnularPagoProveedorInternoAsync(
                pagoProveedorId,
                motivoNormalizado,
                usuarioId,
                ct),
            ct);
    }


    private async Task AnularPagoProveedorInternoAsync(
        Guid pagoProveedorId,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        await using var db =
            await factory.CreateDbContextAsync(ct);

        await using var transaction =
            await db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct);

        var pago =
            await db.PagosProveedores
                .FirstOrDefaultAsync(
                    x => x.Id == pagoProveedorId,
                    ct)
            ?? throw new InvalidOperationException(
                "El pago a proveedor indicado no existe.");

        if (pago.Anulado)
        {
            throw new InvalidOperationException(
                "El pago a proveedor ya se encuentra anulado.");
        }

        var movimientosCaja =
            await db.MovimientosCaja
                .Where(x =>
                    x.PagoProveedorId == pago.Id
                    && !x.AnuladoUtc.HasValue
                    && !x.AnulaMovimientoId.HasValue)
                .ToListAsync(ct);

        foreach (var movimiento in movimientosCaja)
        {
            await RevertirMovimientoCajaAsync(
                db,
                movimiento,
                motivo,
                usuarioId,
                ct);
        }

        var movimientosBanco =
            await db.MovimientosBancarios
                .Where(x =>
                    x.PagoProveedorId == pago.Id
                    && !x.AnuladoUtc.HasValue
                    && !x.AnulaMovimientoId.HasValue)
                .ToListAsync(ct);

        foreach (var movimiento in movimientosBanco)
        {
            RevertirMovimientoBancario(
                db,
                movimiento,
                motivo,
                usuarioId);
        }

        pago.AnuladoUtc = DateTimeOffset.UtcNow;
        pago.AnuladoPorUsuarioId = usuarioId;
        pago.MotivoAnulacion = motivo;
        pago.ModificadoUtc = DateTimeOffset.UtcNow;
        pago.ModificadoPorUsuarioId = usuarioId;

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }


    // =========================================================
    // CONTRA-ASIENTOS
    // =========================================================

    private static async Task RevertirMovimientoCajaAsync(
        AppDbContext db,
        MovimientoCaja original,
        string motivo,
        Guid usuarioId,
        CancellationToken ct)
    {
        if (original.Anulado)
        {
            throw new InvalidOperationException(
                "El movimiento de caja ya se encuentra anulado.");
        }

        if (original.EsAnulacion)
        {
            throw new InvalidOperationException(
                "No se puede anular un movimiento que ya es una anulación.");
        }

        var cajaHoy =
            await ObtenerCajaAbiertaHoyAsync(
                db,
                ct);

        original.AnuladoUtc = DateTimeOffset.UtcNow;
        original.AnuladoPorUsuarioId = usuarioId;
        original.MotivoAnulacion = motivo;
        original.ModificadoUtc = DateTimeOffset.UtcNow;
        original.ModificadoPorUsuarioId = usuarioId;

        db.MovimientosCaja.Add(
            new MovimientoCaja
            {
                Id = Guid.NewGuid(),
                CajaId = cajaHoy.Id,
                FechaUtc = DateTimeOffset.UtcNow,
                Tipo = original.Tipo == TipoMovimientoCaja.Ingreso
                    ? TipoMovimientoCaja.Egreso
                    : TipoMovimientoCaja.Ingreso,
                ConceptoCajaId = original.ConceptoCajaId,
                Concepto = Recortar($"Anulación: {original.Concepto}", 300),
                Detalle = Recortar(motivo, 500),
                Importe = original.Importe,
                Referencia = original.Referencia,
                PagoId = original.PagoId,
                PagoProveedorId = original.PagoProveedorId,
                AnulaMovimientoId = original.Id,
                CreadoPorUsuarioId = usuarioId
            });
    }


    private static void RevertirMovimientoBancario(
        AppDbContext db,
        MovimientoBancario original,
        string motivo,
        Guid usuarioId)
    {
        original.AnuladoUtc = DateTimeOffset.UtcNow;
        original.AnuladoPorUsuarioId = usuarioId;
        original.MotivoAnulacion = motivo;
        original.ModificadoUtc = DateTimeOffset.UtcNow;
        original.ModificadoPorUsuarioId = usuarioId;

        db.MovimientosBancarios.Add(
            new MovimientoBancario
            {
                Id = Guid.NewGuid(),
                CuentaBancariaId = original.CuentaBancariaId,
                FechaUtc = DateTimeOffset.UtcNow,
                Tipo = original.Tipo == TipoMovimientoBancario.Ingreso
                    ? TipoMovimientoBancario.Egreso
                    : TipoMovimientoBancario.Ingreso,
                Origen = original.Origen,
                Importe = original.Importe,
                Concepto = Recortar($"Anulación: {original.Concepto}", 300),
                PagoId = original.PagoId,
                PagoProveedorId = original.PagoProveedorId,
                ReferenciaExterna = original.ReferenciaExterna,
                Observaciones = motivo,
                AnulaMovimientoId = original.Id,
                CreadoPorUsuarioId = usuarioId
            });
    }


    /*
     * La devolución / reversa de efectivo siempre se registra en la
     * caja abierta de HOY (el día en que se anula):
     * - Si el original era de un día ya cerrado, esa caja queda intacta.
     * - Si quedó abierta la caja de un día anterior (se olvidaron de
     *   cerrarla), no se usa: hay que cerrarla y abrir la de hoy.
     *   Así cada arqueo tiene solo movimientos de su día.
     */
    private static async Task<CajaEntidad> ObtenerCajaAbiertaHoyAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var hoy =
            RegistradorCobros.ObtenerFechaArgentina();

        var caja =
            await db.Cajas
                .FirstOrDefaultAsync(
                    x => x.FechaOperativa == hoy,
                    ct);

        if (caja is not null && caja.Estado == EstadoCaja.Abierta)
        {
            return caja;
        }

        var cajaVieja =
            await db.Cajas
                .Where(x =>
                    x.Estado == EstadoCaja.Abierta
                    && x.FechaOperativa < hoy)
                .OrderByDescending(x => x.FechaOperativa)
                .FirstOrDefaultAsync(ct);

        if (cajaVieja is not null)
        {
            throw new InvalidOperationException(
                $"Quedó abierta la caja del {cajaVieja.FechaOperativa:dd/MM/yyyy}. " +
                "Cerrala y abrí la caja de hoy: la reversa del efectivo se registra " +
                "en la caja del día en que se anula.");
        }

        if (caja is not null)
        {
            throw new InvalidOperationException(
                "La caja de hoy está cerrada. Reabrila para registrar la reversa del efectivo.");
        }

        throw new InvalidOperationException(
            "Para anular un movimiento en efectivo tiene que estar abierta " +
            "la caja de hoy: ahí se registra la reversa.");
    }


    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static string ValidarMotivo(
        string motivo,
        string operacion = "la anulación")
    {
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException(
                $"Debe indicar el motivo de {operacion}.");
        }

        var normalizado =
            motivo.Trim();

        if (normalizado.Length > LargoMaximoMotivo)
        {
            throw new InvalidOperationException(
                $"El motivo no puede superar los {LargoMaximoMotivo} caracteres.");
        }

        return normalizado;
    }


    private static void ValidarUsuario(
        Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "No se pudo determinar el usuario que realiza la operación.");
        }
    }


    private static string Recortar(
        string texto,
        int largo)
    {
        return texto.Length <= largo
            ? texto
            : texto[..largo];
    }
}
