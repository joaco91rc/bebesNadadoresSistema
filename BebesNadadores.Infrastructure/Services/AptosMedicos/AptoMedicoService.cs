using BebesNadadores.Application.AptosMedicos;
using BebesNadadores.Application.AptosMedicos.Dtos;
using BebesNadadores.Application.Turnera;
using BebesNadadores.Application.Turnera.Dtos;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using BebesNadadores.Application.Configuracion;

namespace BebesNadadores.Infrastructure.Services.AptosMedicos;

public sealed class AptoMedicoService(
    IDbContextFactory<AppDbContext> factory,
    ITurneraService turneraService,
    IConfiguracionSistemaService configuracionService)
    : IAptoMedicoService
{
    /*
     * Más adelante esto saldrá de Configuración.
     *
     * Por ahora consideramos "por vencer"
     * todo apto cuya vigencia termine dentro
     * de los próximos 15 días.
     */
    

    // =========================================================
    // RESUMEN
    // =========================================================

    public async Task<AptoMedicoResumenDto> ObtenerResumenAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
var configuracion =
    await configuracionService.ObtenerAsync(ct);
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var bebes = await db.Bebes.AsNoTracking().Select(x => new { x.Id }).ToListAsync(ct);

        if (bebes.Count == 0)
        {
            return new AptoMedicoResumenDto(0, 0, 0, 0, 0, 0, 0);
        }

        var bebeIds = bebes.Select(x => x.Id).ToList();

        var aptos = await db
            .AptosMedicos.AsNoTracking()
            .Where(x => bebeIds.Contains(x.BebeId))
            .OrderByDescending(x => x.FechaRevision)
            .ThenByDescending(x => x.VigenciaHasta)
            .ToListAsync(ct);

        var ultimos = aptos.GroupBy(x => x.BebeId).ToDictionary(x => x.Key, x => x.First());

        var sinApto = 0;
        var vigentes = 0;
        var porVencer = 0;
        var vencidos = 0;
        var observados = 0;
        var noAptos = 0;

        foreach (var bebe in bebes)
        {
            ultimos.TryGetValue(bebe.Id, out var apto);

            var situacion = CalcularSituacion(apto, hoy, configuracion.DiasAlertaApto);

            switch (situacion)
            {
                case SituacionAptoMedico.SinApto:

                    sinApto++;

                    break;

                case SituacionAptoMedico.Vigente:

                    vigentes++;

                    break;

                case SituacionAptoMedico.PorVencer:

                    porVencer++;

                    break;

                case SituacionAptoMedico.Vencido:

                    vencidos++;

                    break;

                case SituacionAptoMedico.Observado:

                    observados++;

                    break;

                case SituacionAptoMedico.NoApto:

                    noAptos++;

                    break;
            }
        }

        return new AptoMedicoResumenDto(
            bebes.Count,
            sinApto,
            vigentes,
            porVencer,
            vencidos,
            observados,
            noAptos
        );
    }

    // =========================================================
    // AGENDA MÉDICA
    // =========================================================
    //
    // La pantalla muestra como máximo tres horarios:
    //
    //      horario anterior
    //      horario actual / próximo
    //      horario siguiente
    //
    // IMPORTANTE:
    //
    // agrupamos por HoraInicio.
    //
    // Si existen dos o más clases simultáneas,
    // pertenecen a la misma franja médica.
    // =========================================================

    public async Task<IReadOnlyList<FranjaMedicaDto>> ObtenerAgendaMedicaAsync(
        DateOnly fecha,
        TimeOnly horaActual,
        CancellationToken ct = default
    )
    {

        var configuracion =
        await configuracionService
            .ObtenerAsync(ct);
        var agenda = await turneraService.ObtenerAgendaAsync(fecha, fecha, ct);

        /*
         * No mostramos sesiones canceladas.
         *
         * Una clase finalizada del mismo día sí puede seguir
         * siendo útil para la médica si forma parte del horario
         * inmediatamente anterior.
         */
        var sesiones = agenda
            .Where(x => x.Estado != EstadoSesionClase.Cancelada)
            .OrderBy(x => x.HoraInicio)
            .ThenBy(x => x.Nivel)
            .ToList();

        if (sesiones.Count == 0)
        {
            return [];
        }

        // =====================================================
        // AGRUPAR CLASES QUE COMIENZAN A LA MISMA HORA
        // =====================================================

        var grupos = sesiones
            .GroupBy(x => x.HoraInicio)
            .OrderBy(x => x.Key)
            .Select(x => new GrupoHorario(x.Key, x.Max(s => s.HoraFin), x.ToList()))
            .ToList();

        if (grupos.Count == 0)
        {
            return [];
        }

        // =====================================================
        // ENCONTRAR HORARIO ACTUAL
        // =====================================================

        var indiceActual = grupos.FindIndex(x =>
            horaActual >= x.HoraInicio && horaActual < x.HoraFin
        );

        List<int> indicesSeleccionados;

        if (indiceActual >= 0)
        {
            /*
             * Hay una clase en curso.
             *
             * Queremos:
             *
             * anterior / actual / siguiente
             */
            indicesSeleccionados = ConstruirIndicesAlrededor(grupos.Count, indiceActual);
        }
        else
        {
            /*
             * No hay una clase exactamente en curso.
             *
             * Buscamos el primer horario posterior.
             */
            var indiceProximo = grupos.FindIndex(x => x.HoraInicio > horaActual);

            if (indiceProximo < 0)
            {
                /*
                 * Ya terminó toda la jornada:
                 * mostramos los últimos 3 horarios.
                 */
                indicesSeleccionados = Enumerable
                    .Range(Math.Max(0, grupos.Count - 3), Math.Min(3, grupos.Count))
                    .ToList();
            }
            else if (indiceProximo == 0)
            {
                /*
                 * Todavía no comenzó la primera clase:
                 * mostramos los primeros 3.
                 */
                indicesSeleccionados = Enumerable.Range(0, Math.Min(3, grupos.Count)).ToList();
            }
            else
            {
                /*
                 * Estamos entre dos horarios.
                 *
                 * Ejemplo:
                 *
                 * ahora 14:40
                 *
                 * 14:15 -> anterior
                 * 15:00 -> próximo
                 * 15:45 -> siguiente
                 */
                indicesSeleccionados = new List<int> { indiceProximo - 1, indiceProximo };

                if (indiceProximo + 1 < grupos.Count)
                {
                    indicesSeleccionados.Add(indiceProximo + 1);
                }
                else if (indiceProximo - 2 >= 0)
                {
                    indicesSeleccionados.Insert(0, indiceProximo - 2);
                }
            }
        }

        indicesSeleccionados = indicesSeleccionados
            .Distinct()
            .Where(x => x >= 0 && x < grupos.Count)
            .OrderBy(x => x)
            .ToList();

        // =====================================================
        // IDs DE BEBÉS ESPERADOS
        // =====================================================

        var detalles = new Dictionary<Guid, SesionClaseDetalleDto>();

        var bebeIds = new HashSet<Guid>();

        foreach (var indice in indicesSeleccionados)
        {
            foreach (var sesion in grupos[indice].Sesiones)
            {
                var detalle = await turneraService.ObtenerSesionAsync(sesion.Id, ct);

                if (detalle is null)
                {
                    continue;
                }

                detalles[sesion.Id] = detalle;

                foreach (var bebe in detalle.Bebes)
                {
                    bebeIds.Add(bebe.BebeId);
                }
            }
        }

        // =====================================================
        // BEBÉS
        // =====================================================

        await using var db = await factory.CreateDbContextAsync(ct);

        var bebes =
            bebeIds.Count == 0
                ? []
                : await db.Bebes.AsNoTracking().Where(x => bebeIds.Contains(x.Id)).ToListAsync(ct);

        var bebesPorId = bebes.ToDictionary(x => x.Id);

        // =====================================================
        // APTOS
        // =====================================================

        var aptos =
            bebeIds.Count == 0
                ? []
                : await db
                    .AptosMedicos.AsNoTracking()
                    .Where(x => bebeIds.Contains(x.BebeId))
                    .OrderByDescending(x => x.FechaRevision)
                    .ThenByDescending(x => x.VigenciaHasta)
                    .ToListAsync(ct);

        var ultimoAptoPorBebe = aptos
            .GroupBy(x => x.BebeId)
            .ToDictionary(x => x.Key, x => x.First());

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // =====================================================
        // ARMAR LAS TRES FRANJAS
        // =====================================================

        var resultado = new List<FranjaMedicaDto>();

        foreach (var indice in indicesSeleccionados)
        {
            var grupo = grupos[indice];

            var clases = new List<ClaseMedicaDto>();

            foreach (var sesion in grupo.Sesiones.OrderBy(x => x.Nivel).ThenBy(x => x.Turno))
            {
                detalles.TryGetValue(sesion.Id, out var detalle);

                var bebesClase = new List<AptoMedicoBebeDto>();

                if (detalle is not null)
                {
                    foreach (var bebeSesion in detalle.Bebes.OrderBy(x => x.NombreCompleto))
                    {
                        if (!bebesPorId.TryGetValue(bebeSesion.BebeId, out var bebe))
                        {
                            continue;
                        }

                        ultimoAptoPorBebe.TryGetValue(bebe.Id, out var apto);

                        MapearBebe(
    bebe.Id,
    $"{bebe.Nombre} {bebe.Apellido}".Trim(),
    bebe.FechaNacimiento,
    sesion.NivelId,
    sesion.Nivel,
    apto,
    hoy,
    configuracion.DiasAlertaApto);
                    }
                }

                var profesores = sesion
                    .Profesores.Select(x => x.NombreCompleto)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                clases.Add(
                    new ClaseMedicaDto(
                        sesion.Id,
                        sesion.TurnoId,
                        sesion.Turno,
                        sesion.Nivel,
                        sesion.HoraInicio,
                        sesion.HoraFin,
                        profesores,
                        bebesClase
                    )
                );
            }

            var esActual = horaActual >= grupo.HoraInicio && horaActual < grupo.HoraFin;

            var posicion = ObtenerPosicionHorario(grupos, indice, horaActual, esActual);

            resultado.Add(new FranjaMedicaDto(grupo.HoraInicio, posicion, esActual, clases));
        }

        return resultado;
    }

    // =========================================================
    // LISTADO GENERAL DE BEBÉS
    // =========================================================

    public async Task<IReadOnlyList<AptoMedicoBebeDto>> ListarBebesAsync(
        SituacionAptoMedico? situacion = null,
        Guid? nivelId = null,
        string? buscar = null,
        CancellationToken ct = default
    )
    {
        var configuracion =
    await configuracionService
        .ObtenerAsync(ct);
        await using var db = await factory.CreateDbContextAsync(ct);

        var query = db.Bebes.AsNoTracking().AsQueryable();

        if (nivelId.HasValue)
        {
            query = query.Where(x => x.NivelActualId == nivelId.Value);
        }

        var bebes = await query.OrderBy(x => x.Apellido).ThenBy(x => x.Nombre).ToListAsync(ct);

        if (bebes.Count == 0)
        {
            return [];
        }

        // =====================================================
        // NIVELES
        // =====================================================

        var nivelIds = bebes
            .Where(x => x.NivelActualId.HasValue)
            .Select(x => x.NivelActualId!.Value)
            .Distinct()
            .ToList();

        var niveles =
            nivelIds.Count == 0
                ? []
                : await db
                    .Niveles.AsNoTracking()
                    .Where(x => nivelIds.Contains(x.Id))
                    .ToListAsync(ct);

        var nivelesPorId = niveles.ToDictionary(x => x.Id, x => x.Nombre);

        // =====================================================
        // APTOS
        // =====================================================

        var bebeIds = bebes.Select(x => x.Id).ToList();

        var aptos = await db
            .AptosMedicos.AsNoTracking()
            .Where(x => bebeIds.Contains(x.BebeId))
            .OrderByDescending(x => x.FechaRevision)
            .ThenByDescending(x => x.VigenciaHasta)
            .ToListAsync(ct);

        var ultimoAptoPorBebe = aptos
            .GroupBy(x => x.BebeId)
            .ToDictionary(x => x.Key, x => x.First());

        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var resultado = new List<AptoMedicoBebeDto>();

        foreach (var bebe in bebes)
        {
            ultimoAptoPorBebe.TryGetValue(bebe.Id, out var apto);

            string? nivel = null;

            if (bebe.NivelActualId.HasValue)
            {
                nivelesPorId.TryGetValue(bebe.NivelActualId.Value, out nivel);
            }

           var dto =
    MapearBebe(
        bebe.Id,
        $"{bebe.Nombre} {bebe.Apellido}".Trim(),
        bebe.FechaNacimiento,
        bebe.NivelActualId,
        nivel,
        apto,
        hoy,
        configuracion.DiasAlertaApto);

            if (situacion.HasValue && dto.Situacion != situacion.Value)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(buscar))
            {
                var texto = buscar.Trim();

                var coincide =
                    dto.Bebe.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    || (
                        dto.Nivel is not null
                        && dto.Nivel.Contains(texto, StringComparison.OrdinalIgnoreCase)
                    );

                if (!coincide)
                {
                    continue;
                }
            }

            resultado.Add(dto);
        }

        /*
         * Prioridad médica:
         *
         * No apto
         * Sin apto
         * Vencido
         * Observado
         * Por vencer
         * Vigente
         */
        return resultado
            .OrderBy(x => ObtenerOrdenSituacion(x.Situacion))
            .ThenBy(x => x.VigenciaHasta ?? DateOnly.MinValue)
            .ThenBy(x => x.Bebe)
            .ToList();
    }

    // =========================================================
    // HISTORIAL DE UN BEBÉ
    // =========================================================

    public async Task<IReadOnlyList<AptoMedicoHistorialDto>> ObtenerHistorialAsync(
        Guid bebeId,
        CancellationToken ct = default
    )
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        var existeBebe = await db.Bebes.AsNoTracking().AnyAsync(x => x.Id == bebeId, ct);

        if (!existeBebe)
        {
            throw new InvalidOperationException("El bebé indicado no existe.");
        }

        var aptos = await db
            .AptosMedicos.AsNoTracking()
            .Where(x => x.BebeId == bebeId)
            .OrderByDescending(x => x.FechaRevision)
            .ThenByDescending(x => x.VigenciaHasta)
            .ToListAsync(ct);

        return aptos
            .Select(x => new AptoMedicoHistorialDto(
                x.Id,
                x.FechaRevision,
                x.VigenciaHasta,
                x.Resultado,
                x.Observaciones,
                x.MedicoUsuarioId.ToString()
            ))
            .ToList();
    }

    // =========================================================
    // REGISTRAR REVISACIÓN
    // =========================================================

   public async Task<Guid> RegistrarAsync(
    GuardarAptoMedicoDto dto,
    Guid medicoUsuarioId,
    CancellationToken ct = default)
{
    if (dto.BebeId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "Debe indicar el bebé.");
    }

    if (medicoUsuarioId == Guid.Empty)
    {
        throw new InvalidOperationException(
            "No se pudo identificar al usuario médico.");
    }

    if (dto.FechaRevision == default)
    {
        throw new InvalidOperationException(
            "Debe indicar la fecha de revisión.");
    }

    if (dto.VigenciaHasta == default)
    {
        throw new InvalidOperationException(
            "Debe indicar la fecha de vigencia.");
    }

    if (dto.VigenciaHasta < dto.FechaRevision)
    {
        throw new InvalidOperationException(
            "La vigencia del apto no puede finalizar antes de la revisión.");
    }


    await using var db =
        await factory.CreateDbContextAsync(ct);


    var existeBebe =
        await db.Bebes
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == dto.BebeId,
                ct);

    if (!existeBebe)
    {
        throw new InvalidOperationException(
            "El bebé indicado no existe.");
    }


    // =========================================================
    // ¿YA EXISTE UNA REVISIÓN DEL MISMO DÍA?
    // =========================================================
    //
    // Si existe, no generamos otro registro histórico.
    // Interpretamos que la médica está corrigiendo
    // la revisión realizada ese mismo día.
    //
    // Además, si por la lógica anterior quedaron duplicados,
    // conservamos uno y eliminamos los demás.
    // =========================================================

    var revisionesMismaFecha =
        await db.AptosMedicos
            .Where(x =>
                x.BebeId == dto.BebeId
                &&
                x.FechaRevision == dto.FechaRevision)
            .ToListAsync(ct);


    if (revisionesMismaFecha.Count > 0)
    {
        var apto =
            revisionesMismaFecha[0];


        apto.VigenciaHasta =
            dto.VigenciaHasta;

        apto.Resultado =
            dto.Resultado;

        apto.Observaciones =
            Normalizar(
                dto.Observaciones);

        apto.MedicoUsuarioId =
            medicoUsuarioId;


        // -----------------------------------------------------
        // LIMPIAR DUPLICADOS GENERADOS ANTES DE ESTA CORRECCIÓN
        // -----------------------------------------------------

        if (revisionesMismaFecha.Count > 1)
        {
            var duplicados =
                revisionesMismaFecha
                    .Skip(1)
                    .ToList();

            db.AptosMedicos.RemoveRange(
                duplicados);
        }


        await db.SaveChangesAsync(ct);

        return apto.Id;
    }


    // =========================================================
    // NUEVA REVISIÓN
    // =========================================================

    var nuevoApto =
        new AptoMedico
        {
            Id =
                Guid.NewGuid(),

            BebeId =
                dto.BebeId,

            FechaRevision =
                dto.FechaRevision,

            VigenciaHasta =
                dto.VigenciaHasta,

            Resultado =
                dto.Resultado,

            Observaciones =
                Normalizar(
                    dto.Observaciones),

            MedicoUsuarioId =
                medicoUsuarioId,

            DocumentoStorageKey =
                null,

            DocumentoNombreOriginal =
                null
        };


    db.AptosMedicos.Add(
        nuevoApto);


    await db.SaveChangesAsync(ct);


    return nuevoApto.Id;
}

    // =========================================================
    // MAPEAR BEBÉ
    // =========================================================

   private static AptoMedicoBebeDto MapearBebe(
    Guid bebeId,
    string nombre,
    DateOnly fechaNacimiento,
    Guid? nivelId,
    string? nivel,
    AptoMedico? apto,
    DateOnly hoy,
    int diasAlertaApto)
{
    return new AptoMedicoBebeDto(
        bebeId,
        nombre,
        fechaNacimiento,
        nivelId,
        nivel,
        CalcularSituacion(
            apto,
            hoy,
            diasAlertaApto),
        apto?.Id,
        apto?.Resultado,
        apto?.FechaRevision,
        apto?.VigenciaHasta,
        apto?.Observaciones);
}
    // =========================================================
    // SITUACIÓN DEL APTO
    // =========================================================

  private static SituacionAptoMedico CalcularSituacion(
    AptoMedico? apto,
    DateOnly hoy,
    int diasAlertaApto)
{
    if (apto is null)
    {
        return SituacionAptoMedico.SinApto;
    }

    if (apto.Resultado ==
        ResultadoAptoMedico.NoApto)
    {
        return SituacionAptoMedico.NoApto;
    }

    if (apto.Resultado ==
        ResultadoAptoMedico.Observado)
    {
        return SituacionAptoMedico.Observado;
    }

    if (apto.VigenciaHasta < hoy)
    {
        return SituacionAptoMedico.Vencido;
    }

    if (apto.VigenciaHasta <=
        hoy.AddDays(diasAlertaApto))
    {
        return SituacionAptoMedico.PorVencer;
    }

    return SituacionAptoMedico.Vigente;
}

    // =========================================================
    // ÍNDICES DE HORARIOS
    // =========================================================

    private static List<int> ConstruirIndicesAlrededor(int cantidad, int indiceActual)
    {
        if (cantidad <= 3)
        {
            return Enumerable.Range(0, cantidad).ToList();
        }

        if (indiceActual <= 0)
        {
            return [0, 1, 2];
        }

        if (indiceActual >= cantidad - 1)
        {
            return [cantidad - 3, cantidad - 2, cantidad - 1];
        }

        return [indiceActual - 1, indiceActual, indiceActual + 1];
    }

    // =========================================================
    // TEXTO DE POSICIÓN
    // =========================================================

    private static string ObtenerPosicionHorario(
        IReadOnlyList<GrupoHorario> grupos,
        int indice,
        TimeOnly horaActual,
        bool esActual
    )
    {
        if (esActual)
        {
            return "ACTUAL";
        }

        if (grupos[indice].HoraFin <= horaActual)
        {
            return "ANTERIOR";
        }

        /*
         * Primer horario futuro.
         */
        var primerFuturo = grupos
            .Select((x, i) => new { Grupo = x, Indice = i })
            .FirstOrDefault(x => x.Grupo.HoraInicio > horaActual);

        if (primerFuturo is not null && primerFuturo.Indice == indice)
        {
            return "PRÓXIMO";
        }

        return "SIGUIENTE";
    }

    // =========================================================
    // PRIORIDAD
    // =========================================================

    private static int ObtenerOrdenSituacion(SituacionAptoMedico situacion)
    {
        return situacion switch
        {
            SituacionAptoMedico.NoApto => 1,

            SituacionAptoMedico.SinApto => 2,

            SituacionAptoMedico.Vencido => 3,

            SituacionAptoMedico.Observado => 4,

            SituacionAptoMedico.PorVencer => 5,

            SituacionAptoMedico.Vigente => 6,

            _ => 99,
        };
    }

    // =========================================================
    // TEXTO
    // =========================================================

    private static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        return texto.Trim();
    }

    // =========================================================
    // MODELO INTERNO
    // =========================================================

   private sealed record GrupoHorario(
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    IReadOnlyList<SesionClaseDto> Sesiones);
}
