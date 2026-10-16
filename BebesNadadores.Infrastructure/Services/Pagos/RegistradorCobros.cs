using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CajaEntidad = BebesNadadores.Domain.Entities.Caja;

namespace BebesNadadores.Infrastructure.Services.Pagos;

/*
 * =============================================================
 * REGISTRADOR DE COBROS
 * =============================================================
 *
 * Único lugar donde se registra dinero que ingresa por un bebé.
 *
 * Lo usan:
 *   - PagoService.RegistrarAsync  (pago desde la pantalla Pagos)
 *   - BebeService.CrearAsync      (pago inicial de la inscripción)
 *
 * Se encarga de:
 *   1. Resolver el destino del dinero:
 *        Efectivo                          -> caja abierta del día
 *        Transferencia / Tarjeta / MP      -> cuenta bancaria activa
 *        Otro                              -> sin destino físico
 *   2. Crear el Pago.
 *   3. Imputarlo a las cuotas recibidas (en el orden recibido).
 *   4. Generar el MovimientoCaja o el MovimientoBancario.
 *
 * NO abre transacción ni guarda: trabaja sobre el DbContext del
 * llamador para quedar dentro de su misma transacción.
 */
public sealed class RegistradorCobros
{
    public async Task<CobroRegistrado> RegistrarAsync(
        AppDbContext db,
        NuevoCobro cobro,
        CancellationToken ct)
    {
        if (cobro.Importe <= 0)
        {
            throw new InvalidOperationException(
                "El importe del pago debe ser mayor a cero.");
        }


        // =========================================================
        // DESTINO DEL DINERO
        // =========================================================

        CajaEntidad? caja =
            null;

        CuentaBancaria? cuentaBancaria =
            null;


        if (cobro.MedioPago.IngresaACaja())
        {
            caja =
                await ObtenerCajaAbiertaHoyAsync(
                    db,
                    ct);
        }
        else if (cobro.MedioPago.RequiereCuentaBancaria())
        {
            if (!cobro.CuentaBancariaId.HasValue)
            {
                throw new InvalidOperationException(
                    "Debe seleccionar la cuenta que recibió el pago.");
            }

            cuentaBancaria =
                await db.CuentasBancarias
                    .Include(x => x.Banco)
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == cobro.CuentaBancariaId.Value
                            && x.Activa
                            && x.Banco.Activo,
                        ct)
                ?? throw new InvalidOperationException(
                    "La cuenta seleccionada no existe o se encuentra inactiva.");
        }


        // =========================================================
        // PAGO
        // =========================================================

        var pago =
            new Pago
            {
                Id = Guid.NewGuid(),
                BebeId = cobro.Bebe.Id,
                Bebe = cobro.Bebe,
                ResponsableId = cobro.ResponsableId,
                FechaUtc = DateTimeOffset.UtcNow,
                Importe = cobro.Importe,
                MedioPago = cobro.MedioPago,
                ReferenciaExterna = Normalizar(cobro.ReferenciaExterna),
                Observaciones = Normalizar(cobro.Observaciones),
                CajaId = caja?.Id,
                CuentaBancariaId = cuentaBancaria?.Id,
                CreadoPorUsuarioId = cobro.UsuarioId
            };

        db.Pagos.Add(pago);


        // =========================================================
        // IMPUTACIÓN A CUOTAS
        // =========================================================

        var restante =
            pago.Importe;

        foreach (var cuota in cobro.CuotasAImputar)
        {
            if (restante <= 0)
                break;

            var saldoCuota =
                ObtenerSaldoCuota(cuota);

            if (saldoCuota <= 0)
                continue;

            var importeAplicar =
                Math.Min(
                    restante,
                    saldoCuota);

            var imputacion =
                new ImputacionPago
                {
                    PagoId = pago.Id,
                    Pago = pago,
                    CuotaId = cuota.Id,
                    Cuota = cuota,
                    Importe = importeAplicar
                };

            db.ImputacionesPago.Add(imputacion);

            /*
             * EF ya agrega la imputación a cuota.Imputaciones al hacer
             * el Add (fix-up de navegaciones). Agregarla a mano la
             * duplicaba en memoria y un pago PARCIAL dejaba la cuota
             * como "Pagada".
             */
            AgregarSiFalta(cuota.Imputaciones, imputacion);

            restante -= importeAplicar;

            ActualizarEstadoCuota(cuota);
        }


        // =========================================================
        // MOVIMIENTO DE CAJA
        // =========================================================

        if (caja is not null)
        {
            var conceptoCuota =
                await ObtenerConceptoCuotaAsync(
                    db,
                    ct);

            db.MovimientosCaja.Add(
                new MovimientoCaja
                {
                    Id = Guid.NewGuid(),
                    CajaId = caja.Id,
                    FechaUtc = pago.FechaUtc,
                    Tipo = conceptoCuota.Tipo,
                    ConceptoCajaId = conceptoCuota.Id,
                    Concepto = conceptoCuota.Nombre,
                    Detalle = cobro.DetalleMovimiento,
                    Importe = pago.Importe,
                    Referencia = pago.ReferenciaExterna,
                    PagoId = pago.Id,
                    Pago = pago,
                    CreadoPorUsuarioId = cobro.UsuarioId
                });
        }


        // =========================================================
        // MOVIMIENTO BANCARIO
        // =========================================================

        if (cuentaBancaria is not null)
        {
            db.MovimientosBancarios.Add(
                new MovimientoBancario
                {
                    Id = Guid.NewGuid(),
                    CuentaBancariaId = cuentaBancaria.Id,
                    FechaUtc = pago.FechaUtc,
                    Tipo = TipoMovimientoBancario.Ingreso,
                    Origen = OrigenMovimientoBancario.PagoBebe,
                    Importe = pago.Importe,
                    Concepto = cobro.DetalleMovimiento,
                    PagoId = pago.Id,
                    Pago = pago,
                    ReferenciaExterna = pago.ReferenciaExterna,
                    Observaciones = pago.Observaciones,
                    CreadoPorUsuarioId = cobro.UsuarioId
                });
        }


        return new CobroRegistrado(
            pago,
            pago.Importe - restante,
            restante);
    }


    // =============================================================
    // CONCEPTO DE SISTEMA "CUOTA NATATORIO MENSUAL"
    // =============================================================

    /*
     * Es un concepto interno: si todavía no existe (base nueva,
     * nadie entró a Caja) se crea; si quedó inactivo se reactiva.
     * Mismo criterio que PagoProveedorService con "Pago a proveedor".
     */
    private static async Task<ConceptoCaja> ObtenerConceptoCuotaAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var concepto =
            await db.ConceptosCaja
                .FirstOrDefaultAsync(
                    x => x.Id == ConceptosCajaSistema.CuotaNatatorioMensualId,
                    ct);

        if (concepto is null)
        {
            concepto =
                new ConceptoCaja
                {
                    Id = ConceptosCajaSistema.CuotaNatatorioMensualId,
                    Nombre = ConceptosCajaSistema.CuotaNatatorioMensual,
                    Tipo = TipoMovimientoCaja.Ingreso,
                    Activo = true,
                    Orden = 0
                };

            db.ConceptosCaja.Add(concepto);
        }
        else if (!concepto.Activo)
        {
            concepto.Activo = true;
        }

        return concepto;
    }


    // =============================================================
    // CAJA DEL DÍA
    // =============================================================

    /*
     * Se expone para que los formularios puedan validar ANTES de
     * hacer todo el trabajo (ej.: inscripción con pago en efectivo).
     */
    public static async Task<CajaEntidad> ObtenerCajaAbiertaHoyAsync(
        AppDbContext db,
        CancellationToken ct)
    {
        var hoy =
            ObtenerFechaArgentina();

        var caja =
            await db.Cajas
                .FirstOrDefaultAsync(
                    x => x.FechaOperativa == hoy,
                    ct);

        if (caja is null)
        {
            throw new InvalidOperationException(
                "No hay una caja abierta para el día de hoy. " +
                "Debe abrirla antes de registrar un pago en efectivo.");
        }

        if (caja.Estado != EstadoCaja.Abierta)
        {
            throw new InvalidOperationException(
                "La caja del día ya se encuentra cerrada. " +
                "No pueden registrarse pagos en efectivo.");
        }

        return caja;
    }


    // =============================================================
    // ESTADO DE CUOTAS (regla única)
    // =============================================================

    public static void AgregarSiFalta(
        ICollection<ImputacionPago> imputaciones,
        ImputacionPago imputacion)
    {
        if (!imputaciones.Contains(imputacion))
        {
            imputaciones.Add(imputacion);
        }
    }



    public static void ActualizarEstadoCuota(
        Cuota cuota)
    {
        if (cuota.Estado == EstadoCuota.Anulada)
        {
            return;
        }

        var pagado =
            cuota.Imputaciones.Sum(x => x.Importe);

        cuota.Estado =
            CalcularEstadoCuota(
                cuota,
                pagado,
                ObtenerFechaArgentina());
    }


    public static EstadoCuota CalcularEstadoCuota(
        Cuota cuota,
        decimal importePagado,
        DateOnly hoy)
    {
        if (cuota.Estado == EstadoCuota.Anulada)
        {
            return EstadoCuota.Anulada;
        }

        if (importePagado >= cuota.Importe)
        {
            return EstadoCuota.Pagada;
        }

        if (cuota.FechaVencimiento < hoy)
        {
            return EstadoCuota.Vencida;
        }

        if (importePagado > 0)
        {
            return EstadoCuota.Parcial;
        }

        return EstadoCuota.Pendiente;
    }


    public static decimal ObtenerSaldoCuota(
        Cuota cuota)
    {
        var pagado =
            cuota.Imputaciones.Sum(x => x.Importe);

        return Math.Max(
            0,
            cuota.Importe - pagado);
    }


    // =============================================================
    // FECHA ARGENTINA
    // =============================================================

    private static readonly TimeZoneInfo ZonaArgentina =
        CrearZonaArgentina();


    public static DateOnly ObtenerFechaArgentina()
    {
        var ahoraArgentina =
            TimeZoneInfo.ConvertTime(
                DateTimeOffset.UtcNow,
                ZonaArgentina);

        return DateOnly.FromDateTime(
            ahoraArgentina.DateTime);
    }


    private static TimeZoneInfo CrearZonaArgentina()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "America/Argentina/Buenos_Aires");
        }
        catch
        {
            // Continúa con el id de Windows.
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "Argentina Standard Time");
        }
        catch
        {
            // Último recurso: Argentina no usa horario de verano.
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Argentina",
            TimeSpan.FromHours(-3),
            "Argentina",
            "Argentina");
    }


    private static string? Normalizar(
        string? valor)
    {
        return string.IsNullOrWhiteSpace(valor)
            ? null
            : valor.Trim();
    }
}


/*
 * Datos de un cobro a registrar.
 *
 * CuotasAImputar: cuotas ya trackeadas por el DbContext
 * (con sus Imputaciones cargadas), en el orden de imputación.
 */
public sealed record NuevoCobro(
    Bebe Bebe,
    Guid? ResponsableId,
    decimal Importe,
    MedioPago MedioPago,
    Guid? CuentaBancariaId,
    string? ReferenciaExterna,
    string? Observaciones,
    IReadOnlyList<Cuota> CuotasAImputar,
    string DetalleMovimiento,
    Guid? UsuarioId);


public sealed record CobroRegistrado(
    Pago Pago,
    decimal ImporteImputado,
    decimal Restante);
