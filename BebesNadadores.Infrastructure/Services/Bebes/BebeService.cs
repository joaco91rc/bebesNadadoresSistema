using BebesNadadores.Application.Cuotas;
using BebesNadadores.Application.Recuperos;
using Microsoft.Extensions.Logging;
using BebesNadadores.Application.Bebes;
using BebesNadadores.Application.Bebes.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Infrastructure.Services.Pagos;
using BebesNadadores.Infrastructure.Services.Turnera;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Bebes;

public sealed class BebeService(
    IDbContextFactory<AppDbContext> factory,
    RegistradorCobros registradorCobros,
    IRecuperosInicioMitadMesService recuperosInicio,
    ILogger<BebeService> logger
) : IBebeService
{
    // =========================================================
    // LISTAR
    // =========================================================

    public async Task<IReadOnlyList<BebeDto>> ListarAsync(
        bool incluirBajas = false,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db
            .Bebes.AsNoTracking()
            .Include(x => x.NivelActual)
            .Include(x => x.Responsables)
                .ThenInclude(x => x.Responsable)
            .AsQueryable();

        if (!incluirBajas)
        {
            query = query.Where(x => x.Estado == EstadoBebe.Activo);
        }

        var bebes = await query.OrderBy(x => x.Apellido).ThenBy(x => x.Nombre).ToListAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        return bebes.Select(x => MapearBebe(x, hoy)).ToList();
    }

    // =========================================================
    // OBTENER
    // =========================================================

    public async Task<BebeDto?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var bebe = await db
            .Bebes.AsNoTracking()
            .Include(x => x.NivelActual)
            .Include(x => x.Responsables)
                .ThenInclude(x => x.Responsable)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (bebe is null)
            return null;

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        return MapearBebe(bebe, hoy);
    }

    // =========================================================
    // CREAR
    // =========================================================

    public async Task<Guid> CrearAsync(
        GuardarBebeDto dto,
        CancellationToken ct = default)
    {
        var bebeId =
            await ReintentoTransaccional.EjecutarAsync(
                () => CrearInternoAsync(
                    dto,
                    ct),
                ct);

        /*
         * Si arrancó a mitad de mes le corresponde un recupero de
         * regalo. Si falla (por ejemplo, la turnera todavía no tiene
         * su primera clase), lo vuelve a intentar el job diario: el
         * alta no se cae por esto.
         */
        try
        {
            await recuperosInicio.GenerarPendientesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "No se pudo generar el recupero de inicio a mitad de mes del bebé {BebeId}; lo reintenta el job diario.",
                bebeId);
        }

        return bebeId;
    }


    private async Task<Guid> CrearInternoAsync(GuardarBebeDto dto, CancellationToken ct = default)
    {
        ValidarDto(dto);

        await using var db = await factory.CreateDbContextAsync(ct);

        /*
         * Evitamos que dos administrativas tomen
         * simultáneamente el último cupo.
         */
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            ct
        );

        // =========================================================
        // FECHAS BÁSICAS
        // =========================================================

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // =========================================================
        // MEDIO DEL PAGO INICIAL
        //
        // Se valida al principio para no hacer todo el trabajo y
        // fallar al final (ej.: caja cerrada).
        // =========================================================

        if (dto.MedioPagoInicial.IngresaACaja())
        {
            await RegistradorCobros.ObtenerCajaAbiertaHoyAsync(db, ct);
        }
        else if (dto.MedioPagoInicial.RequiereCuentaBancaria()
            && !dto.CuentaBancariaId.HasValue)
        {
            throw new InvalidOperationException(
                "Debe seleccionar la cuenta que recibió el pago inicial."
            );
        }

        if (dto.FechaNacimiento > hoy)
        {
            throw new InvalidOperationException("La fecha de nacimiento no puede ser futura.");
        }

        if (dto.FechaInicioInscripcion < dto.FechaNacimiento)
        {
            throw new InvalidOperationException(
                "La fecha de inicio de la inscripción no puede ser anterior al nacimiento."
            );
        }

        // =========================================================
        // DECLARACIÓN DE SALUD
        // =========================================================

        if (!dto.PermanecioHospitalMasDe5Dias.HasValue)
        {
            throw new InvalidOperationException(
                "Debe indicar si el bebé permaneció en el hospital más de 5 días."
            );
        }

        if (!dto.RecibeTratamiento.HasValue)
        {
            throw new InvalidOperationException(
                "Debe indicar si el bebé recibe algún tratamiento."
            );
        }

        // =========================================================
        // NIVEL
        // =========================================================

        if (!dto.NivelActualId.HasValue)
        {
            throw new InvalidOperationException("Debe seleccionar el nivel del bebé.");
        }

        var nivel =
            await db.Niveles.FirstOrDefaultAsync(
                x => x.Id == dto.NivelActualId.Value && x.Activo,
                ct
            )
            ?? throw new InvalidOperationException(
                "El nivel seleccionado no existe o se encuentra inactivo."
            );

        // =========================================================
        // EDAD / NIVEL
        // =========================================================

        var edadMesesAlInicio = CalcularEdadMeses(dto.FechaNacimiento, dto.FechaInicioInscripcion);

        if (edadMesesAlInicio < nivel.EdadMinMeses || edadMesesAlInicio > nivel.EdadMaxMeses)
        {
            throw new InvalidOperationException(
                $"La edad del bebé no corresponde al nivel {nivel.Nombre}. "
                    + $"El nivel admite entre {nivel.EdadMinMeses} y "
                    + $"{nivel.EdadMaxMeses} meses."
            );
        }

        if (edadMesesAlInicio >= 84)
        {
            throw new InvalidOperationException(
                "El bebé ya alcanzó la edad máxima de 7 años permitida."
            );
        }

        // =========================================================
        // DNI
        // =========================================================

        await ValidarDniBebeAsync(db, dto.Dni, null, ct);

        // =========================================================
        // PLAN
        // =========================================================

        if (!dto.PlanId.HasValue)
        {
            throw new InvalidOperationException("Debe seleccionar un plan.");
        }

        var plan =
            await db.Planes.FirstOrDefaultAsync(x => x.Id == dto.PlanId.Value && x.Activo, ct)
            ?? throw new InvalidOperationException(
                "El plan seleccionado no existe o se encuentra inactivo."
            );

        // =========================================================
        // TARIFA VIGENTE
        // =========================================================

        var tarifa =
            await db
                .TarifasPlan.AsNoTracking()
                .Where(x =>
                    x.PlanId == plan.Id
                    && x.VigenciaDesde <= dto.FechaInicioInscripcion
                    && (
                        !x.VigenciaHasta.HasValue
                        || x.VigenciaHasta.Value >= dto.FechaInicioInscripcion
                    )
                )
                .OrderByDescending(x => x.VigenciaDesde)
                .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(
                "El plan seleccionado no posee una tarifa vigente para la fecha de inscripción."
            );

        if (tarifa.PrecioMensual <= 0)
        {
            throw new InvalidOperationException(
                "La tarifa vigente del plan no posee un importe válido."
            );
        }

        // =========================================================
        // DESCUENTO
        // =========================================================

        if (dto.TipoDescuento.HasValue)
        {
            if (!dto.ValorDescuento.HasValue || dto.ValorDescuento.Value <= 0)
            {
                throw new InvalidOperationException("Debe indicar el valor del descuento.");
            }

            if (dto.TipoDescuento == TipoDescuento.Porcentaje && dto.ValorDescuento.Value > 100)
            {
                throw new InvalidOperationException(
                    "El porcentaje de descuento no puede superar el 100%."
                );
            }

            if (
                dto.TipoDescuento == TipoDescuento.MontoFijo
                && dto.ValorDescuento.Value > tarifa.PrecioMensual
            )
            {
                throw new InvalidOperationException(
                    "El descuento no puede superar la tarifa mensual."
                );
            }
        }
        else
        {
            dto.ValorDescuento = null;

            dto.MotivoDescuento = null;
        }

        // =========================================================
        // IMPORTE PRIMERA CUOTA
        // =========================================================

        /*
         * Regla del primer mes (ReglasCuota): del 1 al 14 cuota
         * completa; del 15 en adelante media cuota + 1 recupero.
         */
        var importeMensual = ReglasCuota.CalcularImporteMensual(
            tarifa.PrecioMensual,
            dto.TipoDescuento,
            dto.ValorDescuento
        );

        var inicioAMitadDeMes = ReglasCuota.EsInicioAMitadDeMes(dto.FechaInicioInscripcion);

        var importePrimeraCuota = ReglasCuota.CalcularImportePrimerMes(
            importeMensual,
            dto.FechaInicioInscripcion
        );

        if (importePrimeraCuota <= 0)
        {
            throw new InvalidOperationException(
                "El importe final de la primera cuota debe ser mayor a cero."
            );
        }

        // =========================================================
        // PAGO INICIAL
        //
        // Cuota completa: se cobra el 50 % para confirmar.
        // Media cuota: se cobra completa en el alta.
        // =========================================================

        var importePagoInicial = inicioAMitadDeMes
            ? importePrimeraCuota
            : Math.Round(importePrimeraCuota * 0.50m, 2, MidpointRounding.AwayFromZero);

        // =========================================================
        // HORARIO HABITUAL
        // =========================================================

        var turnos = new List<Turno>();

        if (dto.DefinirHorarioHabitualAhora)
        {
            // -----------------------------------------------------
            // CANTIDAD DE TURNOS SEGÚN PLAN
            // -----------------------------------------------------

            if (dto.TurnosIds.Count != plan.VecesPorSemana)
            {
                throw new InvalidOperationException(
                    $"El plan {plan.Nombre} requiere seleccionar "
                        + $"{plan.VecesPorSemana} "
                        + $"{(plan.VecesPorSemana == 1 ? "turno semanal" : "turnos semanales")}."
                );
            }

            if (dto.TurnosIds.Distinct().Count() != dto.TurnosIds.Count)
            {
                throw new InvalidOperationException(
                    "No puede seleccionar el mismo turno más de una vez."
                );
            }

            // -----------------------------------------------------
            // BUSCAR TURNOS
            // -----------------------------------------------------

            turnos = await db
                .Turnos.Include(x => x.Nivel)
                .Where(x =>
                    dto.TurnosIds.Contains(x.Id)
                    && x.Estado == EstadoTurno.Activo
                    && x.NivelId == nivel.Id
                    && x.VigenteDesde <= dto.FechaInicioInscripcion
                    && (
                        !x.VigenteHasta.HasValue
                        || x.VigenteHasta.Value >= dto.FechaInicioInscripcion
                    )
                )
                .ToListAsync(ct);

            if (turnos.Count != dto.TurnosIds.Count)
            {
                throw new InvalidOperationException(
                    "Uno o más horarios seleccionados no corresponden al nivel, "
                        + "no están activos o no están vigentes para la fecha de inicio."
                );
            }

            // -----------------------------------------------------
            // UN TURNO POR DÍA
            // -----------------------------------------------------

            var diasDuplicados = turnos
                .GroupBy(x => x.DiaSemana)
                .Where(x => x.Count() > 1)
                .Select(x => x.Key)
                .ToList();

            if (diasDuplicados.Count > 0)
            {
                throw new InvalidOperationException(
                    $"No puede seleccionar más de un horario el mismo día. "
                        + $"Revise: {string.Join(", ", diasDuplicados)}."
                );
            }

            // -----------------------------------------------------
            // CUPO ESTRUCTURAL
            // -----------------------------------------------------

            var turnosIds = turnos.Select(x => x.Id).ToList();

            var ocupacion = await db
                .InscripcionesTurnos.AsNoTracking()
                .Where(x =>
                    turnosIds.Contains(x.TurnoId)
                    && x.Inscripcion.Estado == EstadoInscripcion.Activa
                    && (
                        !x.Inscripcion.FechaFin.HasValue
                        || x.Inscripcion.FechaFin.Value >= dto.FechaInicioInscripcion
                    )
                )
                .GroupBy(x => x.TurnoId)
                .Select(x => new
                {
                    TurnoId = x.Key,

                    Ocupados = x.Count(),
                })
                .ToDictionaryAsync(x => x.TurnoId, x => x.Ocupados, ct);

            foreach (var turno in turnos)
            {
                var ocupados = ocupacion.TryGetValue(turno.Id, out var cantidad) ? cantidad : 0;

                if (ocupados >= turno.Cupo)
                {
                    throw new InvalidOperationException(
                        $"El turno {turno.DiaSemana} "
                            + $"{turno.HoraInicio:HH\\:mm} - "
                            + $"{turno.HoraFin:HH\\:mm} ya no posee cupos disponibles."
                    );
                }
            }
        }

        // =========================================================
        // PRIMERA CLASE
        // =========================================================

        /*
         * Sin horario habitual:
         *
         * obligatoriamente necesitamos una sesión inicial.
         */
        if (!dto.DefinirHorarioHabitualAhora && !dto.SesionInicialId.HasValue)
        {
            throw new InvalidOperationException(
                "Si todavía no se define un horario habitual, debe seleccionar la primera clase."
            );
        }

        SesionClase? sesionInicial = null;

        var crearReservaInicial = false;

        if (dto.SesionInicialId.HasValue)
        {
            sesionInicial =
                await db
                    .SesionesClase.AsNoTracking()
                    .Include(x => x.Nivel)
                    .Include(x => x.Turno)
                    .FirstOrDefaultAsync(x => x.Id == dto.SesionInicialId.Value, ct)
                ?? throw new InvalidOperationException("La primera clase seleccionada no existe.");

            // -----------------------------------------------------
            // ESTADO
            // -----------------------------------------------------

            if (sesionInicial.Estado != EstadoSesionClase.Programada)
            {
                throw new InvalidOperationException(
                    "La primera clase seleccionada no se encuentra programada."
                );
            }

            // -----------------------------------------------------
            // FECHA
            // -----------------------------------------------------

            if (sesionInicial.Fecha < hoy)
            {
                throw new InvalidOperationException(
                    "La primera clase no puede ser una clase pasada."
                );
            }

            if (sesionInicial.Fecha < dto.FechaInicioInscripcion)
            {
                throw new InvalidOperationException(
                    "La primera clase no puede ser anterior a la fecha de inicio de la inscripción."
                );
            }

            // -----------------------------------------------------
            // NIVEL
            // -----------------------------------------------------

            if (sesionInicial.NivelId != nivel.Id)
            {
                throw new InvalidOperationException(
                    $"La primera clase no corresponde al nivel {nivel.Nombre}."
                );
            }

            // -----------------------------------------------------
            // ¿NECESITAMOS RESERVA?
            // -----------------------------------------------------

            /*
             * CASO A:
             *
             * Tiene horario habitual y la sesión elegida pertenece
             * a uno de esos turnos.
             *
             * No hacemos ReservaSesionClase.
             * Ya es alumno regular de esa clase.
             */
            var perteneceAHorarioHabitual =
                dto.DefinirHorarioHabitualAhora && turnos.Any(x => x.Id == sesionInicial.TurnoId);

            /*
             * CASO B:
             *
             * No tiene horario habitual todavía.
             *
             * O bien tiene horario habitual pero quiere comenzar
             * excepcionalmente en otra clase.
             */
            crearReservaInicial = !perteneceAHorarioHabitual;

            // -----------------------------------------------------
            // SI ES RESERVA PUNTUAL, CONTROLAMOS CUPO DE LA SESIÓN
            // -----------------------------------------------------

            if (crearReservaInicial)
            {
                /*
                 * Misma cuenta que Turnera y Recuperos
                 * (CalculadoraCupoSesion).
                 */
                var ocupacion = await CalculadoraCupoSesion.CalcularAsync(
                    db,
                    sesionInicial,
                    ct
                );

                if (ocupacion.Libres <= 0)
                {
                    throw new InvalidOperationException(
                        $"La clase del {sesionInicial.Fecha:dd/MM/yyyy} "
                            + $"a las {sesionInicial.HoraInicio:HH\\:mm} "
                            + $"ya no posee cupos disponibles."
                    );
                }
            }
        }

        // =========================================================
        // CREAR BEBÉ
        // =========================================================

        var bebe = new Bebe
        {
            Id = Guid.NewGuid(),

            Nombre = NormalizarRequerido(dto.Nombre),

            Apellido = NormalizarRequerido(dto.Apellido),

            FechaNacimiento = dto.FechaNacimiento,

            Sexo = dto.Sexo,

            Dni = Normalizar(dto.Dni),

            PermanecioHospitalMasDe5Dias = dto.PermanecioHospitalMasDe5Dias,

            DetalleHospitalizacion =
                dto.PermanecioHospitalMasDe5Dias == true
                    ? Normalizar(dto.DetalleHospitalizacion)
                    : null,

            RecibeTratamiento = dto.RecibeTratamiento,

            DetalleTratamiento =
                dto.RecibeTratamiento == true ? Normalizar(dto.DetalleTratamiento) : null,

            FechaAlta = hoy,

            Estado = EstadoBebe.Activo,

            NivelActualId = dto.NivelActualId,
        };

        db.Bebes.Add(bebe);

        // =========================================================
        // RESPONSABLES
        // =========================================================

        await VincularResponsablesAsync(db, bebe, dto.Responsables, ct);

        var responsablePrincipalId = bebe
            .Responsables.First(x => x.EsContactoPrincipal)
            .ResponsableId;

        // =========================================================
        // INSCRIPCIÓN
        // =========================================================

        var inscripcion = new Inscripcion
        {
            Id = Guid.NewGuid(),

            Bebe = bebe,

            BebeId = bebe.Id,

            Plan = plan,

            PlanId = plan.Id,

            FechaInicio = dto.FechaInicioInscripcion,

            FechaFin = null,

            Estado = EstadoInscripcion.Activa,

            MedioPagoPreferido = dto.MedioPagoPreferido,

            TipoDescuento = dto.TipoDescuento,

            ValorDescuento = dto.ValorDescuento,

            MotivoDescuento = string.IsNullOrWhiteSpace(dto.MotivoDescuento)
                ? null
                : dto.MotivoDescuento.Trim(),

            BonificaRecuperoInicio = inicioAMitadDeMes,
        };

        // =========================================================
        // CAMINO 1:
        // YA TIENE HORARIO HABITUAL
        // =========================================================

        if (dto.DefinirHorarioHabitualAhora)
        {
            foreach (var turno in turnos.OrderBy(x => x.DiaSemana).ThenBy(x => x.HoraInicio))
            {
                inscripcion.Turnos.Add(
                    new InscripcionTurno
                    {
                        Inscripcion = inscripcion,

                        InscripcionId = inscripcion.Id,

                        Turno = turno,

                        TurnoId = turno.Id,

                        EsPrincipal = true,
                    }
                );
            }
        }

        db.Inscripciones.Add(inscripcion);

        // =========================================================
        // CAMINO 2:
        // RESERVA DE PRIMERA CLASE
        // =========================================================

        /*
         * Se crea solamente cuando:
         *
         * - todavía no tiene horario habitual
         *
         * O
         *
         * - tiene horario habitual pero la primera clase
         *   será excepcionalmente en otro turno.
         */
        if (crearReservaInicial && sesionInicial is not null)
        {
            var reservaInicial = new ReservaSesionClase
            {
                Id = Guid.NewGuid(),

                SesionClaseId = sesionInicial.Id,

                InscripcionId = inscripcion.Id,

                BebeId = bebe.Id,

                Tipo = TipoReservaSesion.InscripcionInicial,

                Estado = EstadoReservaSesion.Activa,
            };

            db.ReservasSesionesClase.Add(reservaInicial);
        }

        // =========================================================
        // PRIMERA CUOTA
        // =========================================================

        var periodoPrimeraCuota = new DateOnly(
            dto.FechaInicioInscripcion.Year,
            dto.FechaInicioInscripcion.Month,
            1
        );

        var cuota = new Cuota
        {
            Id = Guid.NewGuid(),

            InscripcionId = inscripcion.Id,

            Inscripcion = inscripcion,

            Periodo = periodoPrimeraCuota,

            FechaVencimiento = dto.FechaInicioInscripcion,

            Importe = importePrimeraCuota,

            Estado = EstadoCuota.Pendiente,

            Descripcion = inicioAMitadDeMes
                ? $"Media cuota {periodoPrimeraCuota:MM/yyyy} (inicio {dto.FechaInicioInscripcion:dd/MM})"
                : $"Cuota {periodoPrimeraCuota:MM/yyyy}",
        };

        db.Cuotas.Add(cuota);

        // =========================================================
        // PAGO INICIAL 50 %
        //
        // Se registra con el mismo componente que usa la pantalla
        // de Pagos: efectivo -> caja del día, transferencia /
        // tarjeta / Mercado Pago -> cuenta bancaria.
        // =========================================================

        await registradorCobros.RegistrarAsync(
            db,
            new NuevoCobro(
                Bebe: bebe,
                ResponsableId: responsablePrincipalId,
                Importe: importePagoInicial,
                MedioPago: dto.MedioPagoInicial,
                CuentaBancariaId: dto.MedioPagoInicial.RequiereCuentaBancaria()
                    ? dto.CuentaBancariaId
                    : null,
                ReferenciaExterna: null,
                Observaciones: inicioAMitadDeMes
                    ? "Pago inicial de inscripción - media cuota por inicio a mitad de mes."
                    : "Pago inicial de inscripción - 50% de la primera cuota.",
                CuotasAImputar: [cuota],
                DetalleMovimiento: $"Pago inicial - {bebe.Nombre} {bebe.Apellido}",
                UsuarioId: null),
            ct);

        // =========================================================
        // GUARDAR
        // =========================================================

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);

        return bebe.Id;
    }

    // =========================================================
    // ACTUALIZAR
    // =========================================================

    public async Task ActualizarAsync(Guid id, GuardarBebeDto dto, CancellationToken ct = default)
    {
        ValidarDto(dto);

        await using var db = await factory.CreateDbContextAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var bebe =
            await db
                .Bebes.Include(x => x.Responsables)
                    .ThenInclude(x => x.Responsable)
                .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("El bebé indicado no existe.");

        await ValidarNivelAsync(db, dto.NivelActualId, ct);

        await ValidarDniBebeAsync(db, dto.Dni, id, ct);

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        if (dto.FechaNacimiento > hoy)
        {
            throw new InvalidOperationException("La fecha de nacimiento no puede ser futura.");
        }

        bebe.Nombre = NormalizarRequerido(dto.Nombre);

        bebe.Apellido = NormalizarRequerido(dto.Apellido);

        bebe.FechaNacimiento = dto.FechaNacimiento;

        bebe.Sexo = dto.Sexo;

        bebe.Dni = Normalizar(dto.Dni);

        /*
         * Si un flujo viejo no manda estos campos, no borramos
         * una declaración que ya estaba guardada.
         */
        if (dto.PermanecioHospitalMasDe5Dias.HasValue)
        {
            bebe.PermanecioHospitalMasDe5Dias = dto.PermanecioHospitalMasDe5Dias;

            bebe.DetalleHospitalizacion =
                dto.PermanecioHospitalMasDe5Dias == true
                    ? Normalizar(dto.DetalleHospitalizacion)
                    : null;
        }

        if (dto.RecibeTratamiento.HasValue)
        {
            bebe.RecibeTratamiento = dto.RecibeTratamiento;

            bebe.DetalleTratamiento =
                dto.RecibeTratamiento == true ? Normalizar(dto.DetalleTratamiento) : null;
        }

        bebe.NivelActualId = dto.NivelActualId;

        // =========================================================
        // SINCRONIZAR RESPONSABLES
        //
        // Ya NO eliminamos todos para volver a insertarlos.
        // Actualizamos, agregamos o quitamos según corresponda.
        // =========================================================

        await SincronizarResponsablesAsync(db, bebe, dto.Responsables, ct);

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    private static async Task SincronizarResponsablesAsync(
        AppDbContext db,
        Bebe bebe,
        IReadOnlyCollection<GuardarResponsableBebeDto> responsables,
        CancellationToken ct
    )
    {
        if (responsables.Count == 0)
        {
            throw new InvalidOperationException("Debe indicar al menos un responsable.");
        }

        var principales = responsables.Count(x => x.EsContactoPrincipal);

        if (principales != 1)
        {
            throw new InvalidOperationException("Debe existir exactamente un contacto principal.");
        }

        /*
         * Sacamos una fotografía de las relaciones que
         * actualmente tiene el bebé.
         *
         * Es importante hacer ToList() porque la colección
         * puede modificarse durante la sincronización.
         */
        var relacionesActuales = bebe.Responsables.ToList();

        /*
         * Sirve para:
         *
         * - detectar responsables duplicados en el DTO
         * - saber qué relaciones debemos conservar
         * - detectar cuáles fueron quitadas
         */
        var responsablesProcesados = new HashSet<Guid>();

        foreach (var dto in responsables)
        {
            ValidarResponsable(dto);

            Responsable responsable;

            BebeResponsable? relacionExistente = null;

            // =====================================================
            // RESPONSABLE YA EXISTENTE
            // =====================================================

            if (dto.ResponsableId.HasValue)
            {
                /*
                 * Primero intentamos obtenerlo desde la relación
                 * que ya está cargada en memoria.
                 */
                relacionExistente = relacionesActuales.FirstOrDefault(x =>
                    x.ResponsableId == dto.ResponsableId.Value
                );

                if (relacionExistente is not null)
                {
                    responsable = relacionExistente.Responsable;
                }
                else
                {
                    /*
                     * También podría ser un responsable existente
                     * en el sistema pero que todavía no estaba
                     * vinculado a este bebé.
                     */
                    responsable =
                        await db.Responsables.FirstOrDefaultAsync(
                            x => x.Id == dto.ResponsableId.Value,
                            ct
                        )
                        ?? throw new InvalidOperationException(
                            "Uno de los responsables indicados no existe."
                        );
                }

                ActualizarResponsable(responsable, dto);
            }
            // =====================================================
            // RESPONSABLE NUEVO
            // =====================================================

            else
            {
                /*
                 * Intentamos reutilizar un responsable existente
                 * por DNI/email antes de crear otro registro.
                 */
                responsable =
                    await BuscarResponsableExistenteAsync(db, dto, ct) ?? CrearResponsable(dto);

                /*
                 * Si BuscarResponsableExistenteAsync encontró
                 * alguien que ya estaba relacionado con el bebé,
                 * recuperamos esa relación.
                 */
                relacionExistente = relacionesActuales.FirstOrDefault(x =>
                    x.ResponsableId == responsable.Id
                );

                /*
                 * Si acaba de crearse, todavía está Detached.
                 */
                if (db.Entry(responsable).State == EntityState.Detached)
                {
                    db.Responsables.Add(responsable);
                }
            }

            // =====================================================
            // DETECTAR DUPLICADOS REALES
            // =====================================================

            if (!responsablesProcesados.Add(responsable.Id))
            {
                throw new InvalidOperationException(
                    $"El responsable {responsable.Nombre} {responsable.Apellido} está repetido."
                );
            }

            // =====================================================
            // ACTUALIZAR RELACIÓN EXISTENTE
            // =====================================================

            if (relacionExistente is not null)
            {
                relacionExistente.Relacion = dto.Relacion;

                relacionExistente.EsContactoPrincipal = dto.EsContactoPrincipal;

                relacionExistente.AutorizaComunicaciones = dto.AutorizaComunicaciones;
            }
            // =====================================================
            // AGREGAR NUEVA RELACIÓN
            // =====================================================

            else
            {
                bebe.Responsables.Add(
                    new BebeResponsable
                    {
                        Bebe = bebe,

                        BebeId = bebe.Id,

                        Responsable = responsable,

                        ResponsableId = responsable.Id,

                        Relacion = dto.Relacion,

                        EsContactoPrincipal = dto.EsContactoPrincipal,

                        AutorizaComunicaciones = dto.AutorizaComunicaciones,
                    }
                );
            }
        }

        // =========================================================
        // ELIMINAR RESPONSABLES QUE EL USUARIO QUITÓ DEL BEBÉ
        // =========================================================

        var relacionesAEliminar = relacionesActuales
            .Where(x => !responsablesProcesados.Contains(x.ResponsableId))
            .ToList();

        if (relacionesAEliminar.Count > 0)
        {
            db.BebesResponsables.RemoveRange(relacionesAEliminar);
        }
    }

    // =========================================================
    // DAR DE BAJA
    // =========================================================

    public async Task DarDeBajaAsync(Guid id, DarDeBajaBebeDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Motivo))
        {
            throw new InvalidOperationException("Debe indicar el motivo de la baja.");
        }

        if (dto.Motivo.Trim().Length > 300)
        {
            throw new InvalidOperationException(
                "El motivo de baja no puede superar los 300 caracteres."
            );
        }

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        /*
         * La baja impacta en el momento (el bebé deja de figurar como
         * activo), por eso no se admite una fecha futura: si se va a
         * fin de mes, se registra la baja ese día.
         */
        if (dto.Fecha > hoy)
        {
            throw new InvalidOperationException(
                "La fecha de baja no puede ser futura. Registrá la baja el día que corresponda."
            );
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var bebe =
            await db.Bebes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("El bebé indicado no existe.");

        if (bebe.Estado == EstadoBebe.Baja)
        {
            return;
        }

        if (dto.Fecha < bebe.FechaAlta)
        {
            throw new InvalidOperationException(
                $"La fecha de baja no puede ser anterior al alta del bebé ({bebe.FechaAlta:dd/MM/yyyy})."
            );
        }

        var fechaBaja = dto.Fecha;

        bebe.DarDeBaja(fechaBaja, dto.Motivo.Trim());

        // =========================================================
        // 1. INSCRIPCIONES
        //
        // Se cierran con FechaFin = fecha de baja. Así dejan de:
        // - generar cuotas (GeneradorCuotasService),
        // - figurar en la turnera / asistencia,
        // - ocupar cupo en el turno.
        // =========================================================

        var inscripciones = await db
            .Inscripciones.Where(x =>
                x.BebeId == bebe.Id
                && (
                    x.Estado == EstadoInscripcion.Activa
                    || x.Estado == EstadoInscripcion.Pausada
                )
            )
            .ToListAsync(ct);

        foreach (var inscripcion in inscripciones)
        {
            if (inscripcion.FechaInicio > fechaBaja)
            {
                // Nunca llegó a empezar.
                inscripcion.Estado = EstadoInscripcion.Cancelada;
                inscripcion.FechaFin = inscripcion.FechaInicio;
            }
            else
            {
                inscripcion.Estado = EstadoInscripcion.Finalizada;

                if (!inscripcion.FechaFin.HasValue || inscripcion.FechaFin.Value > fechaBaja)
                {
                    inscripcion.FechaFin = fechaBaja;
                }
            }

            inscripcion.ModificadoUtc = DateTimeOffset.UtcNow;
        }

        // =========================================================
        // 2. CUOTAS POSTERIORES AL MES DE BAJA
        //
        // Si la baja se carga con atraso (ej.: baja 30/09 cargada el
        // 13/10), la cuota de octubre ya se generó. Se anula solo si
        // no tiene pagos imputados; si tiene, queda como está para no
        // perder el dinero recibido.
        // =========================================================

        var periodoBaja = new DateOnly(fechaBaja.Year, fechaBaja.Month, 1);

        var cuotasPosteriores = await db
            .Cuotas.Where(x =>
                x.Inscripcion.BebeId == bebe.Id
                && x.Periodo > periodoBaja
                && x.Estado != EstadoCuota.Anulada
                && !x.Imputaciones.Any()
            )
            .ToListAsync(ct);

        foreach (var cuota in cuotasPosteriores)
        {
            cuota.Estado = EstadoCuota.Anulada;
            cuota.AnuladaUtc = DateTimeOffset.UtcNow;
            cuota.MotivoAnulacion = "Baja del bebé";
            cuota.ModificadoUtc = DateTimeOffset.UtcNow;
        }

        // =========================================================
        // 3. RECUPEROS PENDIENTES
        //
        // Al anularse, los reservados liberan su lugar en la clase.
        // =========================================================

        var recuperos = await db
            .Recuperaciones.Where(x =>
                x.BebeId == bebe.Id
                && (
                    x.Estado == EstadoRecuperacion.Disponible
                    || x.Estado == EstadoRecuperacion.Reservada
                )
            )
            .ToListAsync(ct);

        foreach (var recupero in recuperos)
        {
            recupero.Estado = EstadoRecuperacion.Anulada;
            recupero.ModificadoUtc = DateTimeOffset.UtcNow;
        }

        // =========================================================
        // 4. RESERVAS DE CLASE INICIAL FUTURAS
        // =========================================================

        var reservas = await db
            .ReservasSesionesClase.Where(x =>
                x.BebeId == bebe.Id
                && x.Estado == EstadoReservaSesion.Activa
                && x.SesionClase.Fecha > fechaBaja
            )
            .ToListAsync(ct);

        foreach (var reserva in reservas)
        {
            reserva.Estado = EstadoReservaSesion.Cancelada;
            reserva.ModificadoUtc = DateTimeOffset.UtcNow;
        }

        // =========================================================
        // 5. LISTA DE ESPERA E INVITACIONES
        // =========================================================

        var solicitudes = await db
            .ListaEspera.Where(x =>
                x.BebeId == bebe.Id
                && (
                    x.Estado == EstadoListaEspera.Activa
                    || x.Estado == EstadoListaEspera.Notificada
                )
            )
            .ToListAsync(ct);

        foreach (var solicitud in solicitudes)
        {
            solicitud.Estado = EstadoListaEspera.Cancelada;
            solicitud.ModificadoUtc = DateTimeOffset.UtcNow;
        }

        var invitaciones = await db
            .InvitacionesVacante.Where(x =>
                x.BebeId == bebe.Id
                && (
                    x.Estado == EstadoInvitacionVacante.Pendiente
                    || x.Estado == EstadoInvitacionVacante.Enviada
                )
            )
            .ToListAsync(ct);

        foreach (var invitacion in invitaciones)
        {
            invitacion.Estado = EstadoInvitacionVacante.Vencida;
            invitacion.ModificadoUtc = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        await transaction.CommitAsync(ct);
    }

    // =========================================================
    // REACTIVAR
    // =========================================================

    /*
     * Reactivar NO revive la inscripción anterior (quedó Finalizada
     * con la fecha de baja). Para volver a cursar se crea una nueva
     * desde "Gestionar inscripción" (InscripcionService.CrearPrimeraAsync),
     * eligiendo plan y horarios vigentes.
     */
    public async Task ReactivarAsync(Guid id, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var bebe =
            await db.Bebes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("El bebé indicado no existe.");

        if (bebe.Estado == EstadoBebe.Activo)
        {
            return;
        }

        bebe.Reactivar();

        await db.SaveChangesAsync(ct);
    }

    // =========================================================
    // CUMPLEAÑOS
    // =========================================================

    public async Task<IReadOnlyList<CumpleaniosBebeDto>> ObtenerCumpleaniosMesAsync(
        int anio,
        int mes,
        CancellationToken ct = default
    )
    {
        if (mes is < 1 or > 12)
        {
            throw new InvalidOperationException("El mes indicado no es válido.");
        }

        await using var db = await factory.CreateDbContextAsync(ct);

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var bebes = await db
            .Bebes.AsNoTracking()
            .Where(x => x.Estado == EstadoBebe.Activo && x.FechaNacimiento.Month == mes)
            .OrderBy(x => x.FechaNacimiento.Day)
            .ThenBy(x => x.Apellido)
            .ThenBy(x => x.Nombre)
            .Select(x => new
            {
                x.Id,
                x.Nombre,
                x.Apellido,
                x.FechaNacimiento,
            })
            .ToListAsync(ct);

        return bebes
            .Select(x =>
            {
                var fechaCumple = new DateOnly(anio, mes, x.FechaNacimiento.Day);

                var edadQueCumple = anio - x.FechaNacimiento.Year;

                var diasFaltantes = fechaCumple.DayNumber - hoy.DayNumber;

                return new CumpleaniosBebeDto(
                    x.Id,
                    x.Nombre,
                    x.Apellido,
                    $"{x.Nombre} {x.Apellido}".Trim(),
                    x.FechaNacimiento,
                    edadQueCumple,
                    fechaCumple == hoy,
                    diasFaltantes
                );
            })
            .OrderBy(x => x.FechaNacimiento.Day)
            .ThenBy(x => x.Apellido)
            .ThenBy(x => x.Nombre)
            .ToList();
    }

    // =========================================================
    // RESPONSABLES
    // =========================================================

    private static async Task VincularResponsablesAsync(
        AppDbContext db,
        Bebe bebe,
        IReadOnlyCollection<GuardarResponsableBebeDto> responsables,
        CancellationToken ct
    )
    {
        if (responsables.Count == 0)
        {
            throw new InvalidOperationException("Debe indicar al menos un responsable.");
        }

        var principales = responsables.Count(x => x.EsContactoPrincipal);

        if (principales != 1)
        {
            throw new InvalidOperationException("Debe existir exactamente un contacto principal.");
        }

        foreach (var dto in responsables)
        {
            Responsable responsable;

            if (dto.ResponsableId.HasValue)
            {
                responsable =
                    await db.Responsables.FirstOrDefaultAsync(
                        x => x.Id == dto.ResponsableId.Value,
                        ct
                    )
                    ?? throw new InvalidOperationException(
                        "Uno de los responsables indicados no existe."
                    );

                if (!responsable.Activo)
                {
                    responsable.Activo = true;
                }

                ActualizarResponsable(responsable, dto);
            }
            else
            {
                responsable =
                    await BuscarResponsableExistenteAsync(db, dto, ct) ?? CrearResponsable(dto);

                if (db.Entry(responsable).State == EntityState.Detached)
                {
                    db.Responsables.Add(responsable);
                }
            }

            var repetido = bebe.Responsables.Any(x => x.ResponsableId == responsable.Id);

            if (repetido)
            {
                throw new InvalidOperationException(
                    $"El responsable {responsable.Nombre} {responsable.Apellido} está repetido."
                );
            }

            bebe.Responsables.Add(
                new BebeResponsable
                {
                    Bebe = bebe,

                    BebeId = bebe.Id,

                    Responsable = responsable,

                    ResponsableId = responsable.Id,

                    Relacion = dto.Relacion,

                    EsContactoPrincipal = dto.EsContactoPrincipal,

                    AutorizaComunicaciones = dto.AutorizaComunicaciones,
                }
            );
        }
    }

    private static async Task<Responsable?> BuscarResponsableExistenteAsync(
        AppDbContext db,
        GuardarResponsableBebeDto dto,
        CancellationToken ct
    )
    {
        var dni = Normalizar(dto.Dni);

        if (!string.IsNullOrWhiteSpace(dni))
        {
            var porDni = await db.Responsables.FirstOrDefaultAsync(x => x.Dni == dni, ct);

            if (porDni is not null)
            {
                ValidarQueSeaMismaPersona(porDni, dto, "DNI");

                ActualizarResponsable(porDni, dto);

                return porDni;
            }
        }

        var email = Normalizar(dto.Email);

        if (!string.IsNullOrWhiteSpace(email))
        {
            var emailLower = email.ToLower();

            var porEmail = await db.Responsables.FirstOrDefaultAsync(
                x => x.Email.ToLower() == emailLower,
                ct
            );

            if (porEmail is not null)
            {
                ValidarQueSeaMismaPersona(porEmail, dto, "email");

                ActualizarResponsable(porEmail, dto);

                return porEmail;
            }
        }

        return null;
    }

    private static void ValidarQueSeaMismaPersona(
        Responsable existente,
        GuardarResponsableBebeDto dto,
        string campoCoincidente
    )
    {
        var mismoNombre = string.Equals(
            existente.Nombre.Trim(),
            dto.Nombre.Trim(),
            StringComparison.OrdinalIgnoreCase
        );

        var mismoApellido = string.Equals(
            existente.Apellido.Trim(),
            dto.Apellido.Trim(),
            StringComparison.OrdinalIgnoreCase
        );

        if (mismoNombre && mismoApellido)
        {
            return;
        }

        throw new InvalidOperationException(
            $"El {campoCoincidente} ingresado ya pertenece a "
                + $"{existente.Nombre} {existente.Apellido}. "
                + $"Revise los datos antes de continuar."
        );
    }

    private static Responsable CrearResponsable(GuardarResponsableBebeDto dto)
    {
        ValidarResponsable(dto);

        return new Responsable
        {
            Id = Guid.NewGuid(),

            Nombre = NormalizarRequerido(dto.Nombre),

            Apellido = NormalizarRequerido(dto.Apellido),

            Dni = Normalizar(dto.Dni),

            Telefono = Normalizar(dto.Telefono) ?? string.Empty,

            WhatsApp = Normalizar(dto.WhatsApp) ?? string.Empty,

            Email = Normalizar(dto.Email) ?? string.Empty,

            Domicilio = Normalizar(dto.Domicilio),

            Activo = true,
        };
    }

    private static void ActualizarResponsable(
        Responsable responsable,
        GuardarResponsableBebeDto dto
    )
    {
        ValidarResponsable(dto);

        responsable.Nombre = NormalizarRequerido(dto.Nombre);

        responsable.Apellido = NormalizarRequerido(dto.Apellido);

        responsable.Dni = Normalizar(dto.Dni);

        responsable.Telefono = Normalizar(dto.Telefono) ?? string.Empty;

        responsable.WhatsApp = Normalizar(dto.WhatsApp) ?? string.Empty;

        responsable.Email = Normalizar(dto.Email) ?? string.Empty;

        responsable.Domicilio = Normalizar(dto.Domicilio);

        responsable.Activo = true;
    }

    // =========================================================
    // NIVEL
    // =========================================================

    private static async Task ValidarNivelAsync(
        AppDbContext db,
        Guid? nivelId,
        CancellationToken ct
    )
    {
        if (!nivelId.HasValue)
            return;

        var existe = await db
            .Niveles.AsNoTracking()
            .AnyAsync(x => x.Id == nivelId.Value && x.Activo, ct);

        if (!existe)
        {
            throw new InvalidOperationException(
                "El nivel seleccionado no existe o se encuentra inactivo."
            );
        }
    }

    // =========================================================
    // DNI BEBÉ
    // =========================================================

    private static async Task ValidarDniBebeAsync(
        AppDbContext db,
        string? dni,
        Guid? bebeId,
        CancellationToken ct
    )
    {
        var normalizado = Normalizar(dni);

        if (string.IsNullOrWhiteSpace(normalizado))
        {
            return;
        }

        var existe = await db
            .Bebes.AsNoTracking()
            .AnyAsync(x => x.Dni == normalizado && (!bebeId.HasValue || x.Id != bebeId.Value), ct);

        if (existe)
        {
            throw new InvalidOperationException("Ya existe un bebé registrado con ese DNI.");
        }
    }

    // =========================================================
    // VALIDACIONES
    // =========================================================

    private static void ValidarDto(GuardarBebeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            throw new InvalidOperationException("Debe indicar el nombre del bebé.");
        }

        if (string.IsNullOrWhiteSpace(dto.Apellido))
        {
            throw new InvalidOperationException("Debe indicar el apellido del bebé.");
        }

        if (dto.Nombre.Trim().Length > 100)
        {
            throw new InvalidOperationException("El nombre no puede superar los 100 caracteres.");
        }

        if (dto.Apellido.Trim().Length > 100)
        {
            throw new InvalidOperationException("El apellido no puede superar los 100 caracteres.");
        }

        if (dto.Dni is not null && dto.Dni.Trim().Length > 20)
        {
            throw new InvalidOperationException("El DNI no puede superar los 20 caracteres.");
        }

        if (dto.Responsables.Count > 0 && dto.Responsables.Count(x => x.EsContactoPrincipal) != 1)
        {
            throw new InvalidOperationException("Debe existir exactamente un contacto principal.");
        }

        if (
            dto.PermanecioHospitalMasDe5Dias == true
            && string.IsNullOrWhiteSpace(dto.DetalleHospitalizacion)
        )
        {
            throw new InvalidOperationException("Debe detallar la hospitalización del bebé.");
        }

        if (dto.RecibeTratamiento == true && string.IsNullOrWhiteSpace(dto.DetalleTratamiento))
        {
            throw new InvalidOperationException("Debe detallar el tratamiento que recibe el bebé.");
        }

        if (dto.DetalleHospitalizacion?.Length > 500)
        {
            throw new InvalidOperationException(
                "El detalle de hospitalización no puede superar los 500 caracteres."
            );
        }

        if (dto.DetalleTratamiento?.Length > 500)
        {
            throw new InvalidOperationException(
                "El detalle del tratamiento no puede superar los 500 caracteres."
            );
        }
    }

    private static void ValidarResponsable(GuardarResponsableBebeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
        {
            throw new InvalidOperationException("Debe indicar el nombre del responsable.");
        }

        if (string.IsNullOrWhiteSpace(dto.Apellido))
        {
            throw new InvalidOperationException("Debe indicar el apellido del responsable.");
        }

        if (dto.Nombre.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El nombre del responsable no puede superar los 100 caracteres."
            );
        }

        if (dto.Apellido.Trim().Length > 100)
        {
            throw new InvalidOperationException(
                "El apellido del responsable no puede superar los 100 caracteres."
            );
        }

        if (dto.Telefono?.Length > 50 || dto.WhatsApp?.Length > 50)
        {
            throw new InvalidOperationException(
                "El teléfono o WhatsApp supera la longitud permitida."
            );
        }

        if (dto.Email?.Length > 200)
        {
            throw new InvalidOperationException("El email supera la longitud permitida.");
        }

        if (dto.Domicilio?.Length > 300)
        {
            throw new InvalidOperationException("El domicilio supera la longitud permitida.");
        }
    }

    // =========================================================
    // MAPPING
    // =========================================================

    private static BebeDto MapearBebe(Bebe bebe, DateOnly hoy)
    {
        var edadMeses = bebe.EdadEnMeses(hoy);

        var edadAnios = edadMeses / 12;

        var responsables = bebe
            .Responsables.OrderByDescending(x => x.EsContactoPrincipal)
            .ThenBy(x => x.Responsable.Apellido)
            .ThenBy(x => x.Responsable.Nombre)
            .Select(x => new ResponsableBebeDto(
                x.Responsable.Id,
                x.Responsable.Nombre,
                x.Responsable.Apellido,
                $"{x.Responsable.Nombre} {x.Responsable.Apellido}".Trim(),
                x.Responsable.Dni,
                x.Responsable.Telefono,
                x.Responsable.WhatsApp,
                x.Responsable.Email,
                x.Responsable.Domicilio,
                x.Relacion,
                x.EsContactoPrincipal,
                x.AutorizaComunicaciones
            ))
            .ToList();

        return new BebeDto(
            bebe.Id,
            bebe.Nombre,
            bebe.Apellido,
            $"{bebe.Nombre} {bebe.Apellido}".Trim(),
            bebe.FechaNacimiento,
            bebe.Sexo,
            bebe.Dni,
            bebe.PermanecioHospitalMasDe5Dias,
            bebe.DetalleHospitalizacion,
            bebe.RecibeTratamiento,
            bebe.DetalleTratamiento,
            bebe.FechaAlta,
            bebe.Estado,
            bebe.FechaBaja,
            bebe.MotivoBaja,
            bebe.NivelActualId,
            bebe.NivelActual?.Nombre,
            edadMeses,
            edadAnios,
            bebe.AlcanzoEdadMaxima(hoy),
            responsables
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static string NormalizarRequerido(string texto)
    {
        return texto.Trim();
    }

    private static string? Normalizar(string? texto)
    {
        return string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
    }

    private static int CalcularEdadMeses(DateOnly nacimiento, DateOnly fecha)
    {
        var meses = (fecha.Year - nacimiento.Year) * 12 + fecha.Month - nacimiento.Month;

        if (fecha.Day < nacimiento.Day)
        {
            meses--;
        }

        return Math.Max(0, meses);
    }
}
