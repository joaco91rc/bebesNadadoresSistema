using System.Text;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Migrador;

/// <summary>
/// Grilla oficial de 2026 integrada en la importación inicial.
/// NO es el reparador de bases previamente importadas.
/// Usa la misma transacción creada por LegacyMigrationService.
/// </summary>
public static class TurneraOficialMigracion
{
    private sealed record Horario(int Dia, string Hora, string Nivel)
    {
        public string Clave => $"{Dia}|{TimeOnly.Parse(Hora):HH:mm}";
        public int Cupo => Nivel is "Bebés 3 a 12 meses" or "Bebés 12 a 24 meses" or "2 años" ? 10 : 12;
        public int Duracion => Nivel is "Bebés 3 a 12 meses" or "Bebés 12 a 24 meses" ? 30 : 45;
    }

    // Horarios 2026 informados por la administrativa. Un registro por columna/horario.
    private static readonly Horario[] Oficiales =
    [
        new(1,"15:00","Bebés 12 a 24 meses"), new(1,"15:30","Bebés 12 a 24 meses"),
        new(1,"16:00","2 años"), new(1,"16:45","3 y 4 años"),
        new(1,"17:30","3 a 6 años"), new(1,"18:15","3 y 4 años"),
        new(1,"19:00","3 a 6 años"), new(1,"19:45","Bebés 12 a 24 meses"),
        new(1,"20:15","Bebés 12 a 24 meses"),
        new(2,"15:15","2 años"), new(2,"16:00","Bebés 12 a 24 meses"),
        new(2,"16:30","Bebés 12 a 24 meses"), new(2,"17:00","Bebés 3 a 12 meses"),
        new(2,"17:30","3 y 4 años"), new(2,"18:15","3 a 6 años"),
        new(2,"19:00","3 a 6 años"), new(2,"19:45","5 y 6 años"),
        new(3,"15:00","Bebés 3 a 12 meses"), new(3,"15:30","Bebés 12 a 24 meses"),
        new(3,"16:00","2 años"), new(3,"16:45","3 y 4 años"),
        new(3,"17:30","3 y 4 años"), new(3,"18:15","3 a 6 años"),
        new(3,"19:00","3 a 6 años"), new(3,"19:45","2 años"),
        new(4,"15:00","Bebés 3 a 12 meses"), new(4,"15:30","Bebés 12 a 24 meses"),
        new(4,"16:00","2 años"), new(4,"16:45","3 y 4 años"),
        new(4,"17:30","3 a 6 años"), new(4,"18:15","5 y 6 años"),
        new(4,"19:00","3 y 4 años"), new(4,"19:45","2 años"),
        new(5,"15:15","Bebés 12 a 24 meses"), new(5,"15:45","Bebés 12 a 24 meses"),
        new(5,"16:15","2 años"), new(5,"17:00","Bebés 12 a 24 meses"),
        new(5,"17:30","Bebés 3 a 12 meses"), new(5,"18:00","2 años"),
        new(5,"18:45","2 años"), new(5,"19:30","Bebés 12 a 24 meses"),
        new(5,"20:00","2 años"),
        new(6,"09:00","2 años"), new(6,"09:45","Bebés 12 a 24 meses"),
        new(6,"10:15","Bebés 3 a 12 meses"), new(6,"10:45","2 años"),
        new(6,"11:30","Bebés 12 a 24 meses"), new(6,"12:00","Bebés 12 a 24 meses"),
        new(6,"12:30","Bebés 3 a 12 meses"), new(6,"13:00","2 años")
    ];

    public static async Task IntegrarAsync(
        AppDbContext db,
        IReadOnlyList<LegacyTurnoVigente> fuentes,
        IReadOnlyDictionary<long, Guid> bebeIds,
        IReadOnlyDictionary<(long Persona,int Year), Guid> inscripcionIds,
        int anio,
        string reportDirectory,
        CancellationToken ct = default)
    {
        if (anio != 2026)
            throw new InvalidOperationException("La grilla oficial suministrada corresponde exclusivamente a 2026.");
        if (Oficiales.Length != 50 || Oficiales.Select(x => x.Clave).Distinct().Count() != 50)
            throw new InvalidOperationException("Grilla oficial inconsistente: deben existir 50 horarios únicos.");

        Directory.CreateDirectory(reportDirectory);
        var mapa = Oficiales.ToDictionary(x => x.Clave);
        var levels = await db.Niveles.AsNoTracking().ToListAsync(ct);
        var nivelPorNombre = levels.ToDictionary(x => x.Nombre, StringComparer.OrdinalIgnoreCase);
        foreach (var h in Oficiales)
            if (!nivelPorNombre.ContainsKey(h.Nivel))
                throw new InvalidOperationException($"No existe el nivel maestro '{h.Nivel}'.");

        // Prevalidar todas las asignaciones: jamás reubicar por proximidad
        // de horarios ni convertir un nivel distinto automáticamente.
        var inconsistencias = new List<string>();
        var asignaciones = new List<(long Persona, int TurnoLegacy, Guid InscripcionId, Horario Horario)>();
        var noVinculados = new List<string>();
        foreach (var f in fuentes)
        {
            if (!TimeOnly.TryParse(f.Hora, out var hora))
            {
                inconsistencias.Add($"HORA_INVALIDA;{f.PersonaId};{f.TurnoId};{f.Hora}");
                continue;
            }
            var key = $"{f.Dia}|{hora:HH:mm}";
            if (!mapa.TryGetValue(key, out var h))
            {
                inconsistencias.Add($"FUERA_DE_GRILLA;{f.PersonaId};{f.TurnoId};{key}");
                continue;
            }
            var real = MigrationHelpers.ResolveLevel(f.NivelOriginal,
                new DateOnly(2026, 10, 31), null, levels);
            if (real is null || !string.Equals(real.Nombre, h.Nivel, StringComparison.OrdinalIgnoreCase))
            {
                inconsistencias.Add($"NIVEL_NO_COINCIDE;{f.PersonaId};{f.TurnoId};{f.NivelOriginal};{h.Nivel}");
                continue;
            }
            if (!string.Equals(f.TurnoActivo, "S", StringComparison.OrdinalIgnoreCase))
            {
                inconsistencias.Add($"TURNO_DESHABILITADO;{f.PersonaId};{f.TurnoId};{key}");
                continue;
            }
            if (!bebeIds.TryGetValue(f.PersonaId, out _) ||
                !inscripcionIds.TryGetValue((f.PersonaId, anio), out var insId))
            {
                noVinculados.Add($"{f.PersonaId};{f.TurnoId};SIN_FICHA_O_INSCRIPCION_VALIDADA");
                continue;
            }
            asignaciones.Add((f.PersonaId, f.TurnoId, insId, h));
        }
        File.WriteAllLines(Path.Combine(reportDirectory, "turnera_integrada_discrepancias.csv"),
            new[] { "MOTIVO;IDPERSONA;IDTURNO;DETALLE" }.Concat(inconsistencias), Encoding.UTF8);
        File.WriteAllLines(Path.Combine(reportDirectory, "turnera_integrada_sin_ficha.csv"),
            new[] { "IDPERSONA;IDTURNO;MOTIVO" }.Concat(noVinculados), Encoding.UTF8);
        if (inconsistencias.Count != 0)
            throw new InvalidOperationException($"Grilla y MySQL presentan {inconsistencias.Count} discrepancias. Ver turnera_integrada_discrepancias.csv. Se revierte la transacción.");

        var ciclo = await db.CiclosLectivos.SingleAsync(x => x.Anio == anio, ct);
        var existing = await db.Turnos.Where(x => x.CicloLectivoId == ciclo.Id).ToListAsync(ct);
        var oficiales = new Dictionary<string, Turno>();
        var nuevos = new List<Turno>();
        foreach (var h in Oficiales)
        {
            var levelId = nivelPorNombre[h.Nivel].Id;
            var time = TimeOnly.Parse(h.Hora);
            var candidatos = existing.Where(x => (int)x.DiaSemana == h.Dia &&
                x.HoraInicio == time && x.NivelId == levelId).ToList();
            if (candidatos.Count > 1)
                throw new InvalidOperationException($"Turnos duplicados en {h.Clave} - {h.Nivel}.");
            var turno = candidatos.FirstOrDefault();
            if (turno is null)
            {
                turno = new Turno
                {
                    Id = MigrationHelpers.StableGuid($"turno:{anio}:{h.Dia}:{time:HHmm}:{levelId}"),
                    DiaSemana = (DiaSemana)h.Dia, HoraInicio = time,
                    NivelId = levelId, CicloLectivoId = ciclo.Id,
                    VigenteDesde = new DateOnly(2026, 2, 1),
                    CreadoUtc = DateTimeOffset.UtcNow
                };
                if (existing.Any(x => x.Id == turno.Id))
                    throw new InvalidOperationException($"Colisión de ID de turno {h.Clave}.");
                nuevos.Add(turno);
            }
            else if (!turno.Nombre.StartsWith("Legacy", StringComparison.OrdinalIgnoreCase) &&
                     !turno.Nombre.StartsWith("Oficial 2026", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Turno no migrado en {h.Clave}: intervención manual requerida.");

            turno.Nombre = $"Oficial 2026 - {((DiaSemana)h.Dia)} {h.Hora} - {h.Nivel}";
            turno.DuracionMinutos = h.Duracion;
            turno.Cupo = h.Cupo;  // No ampliar el cupo habitual por sobrecupo real.
            turno.Estado = EstadoTurno.Activo;
            turno.VigenteHasta = null;
            oficiales[h.Clave] = turno;
        }
        db.Turnos.AddRange(nuevos);
        await db.SaveChangesAsync(ct);

        var officialIds = oficiales.Values.Select(x => x.Id).ToHashSet();
        var extra = existing.Where(x => !officialIds.Contains(x.Id) &&
            (x.Nombre.StartsWith("Legacy", StringComparison.OrdinalIgnoreCase) ||
             x.Nombre.StartsWith("Oficial 2026", StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var t in extra)
        {
            t.Estado = EstadoTurno.Baja;
            t.VigenteHasta = new DateOnly(2026, 11, 2);
        }

        // En importación inicial no hay enlaces válidos previos. Un origen
        // restaurado sobre base ya migrada debe detenerse, no duplicar alumnos.
        var ids = asignaciones.Select(x => x.InscripcionId).ToHashSet();
        var previos = await db.InscripcionesTurnos
            .Where(x => ids.Contains(x.InscripcionId)).AnyAsync(ct);
        if (previos)
            throw new InvalidOperationException("Ya existen enlaces vigentes para estas inscripciones. Usar una base destino nueva, no repetir la importación.");

        foreach (var a in asignaciones.DistinctBy(x => (x.InscripcionId, x.Horario.Clave)))
        {
            db.InscripcionesTurnos.Add(new InscripcionTurno
            {
                InscripcionId = a.InscripcionId,
                TurnoId = oficiales[a.Horario.Clave].Id,
                EsPrincipal = true
            });
        }
        await db.SaveChangesAsync(ct);

        var sobrecupos = asignaciones.GroupBy(x => x.Horario.Clave)
            .Select(g => new { Horario = mapa[g.Key], Personas = g.Select(x => x.Persona).Distinct().Count() })
            .Where(x => x.Personas > x.Horario.Cupo)
            .OrderByDescending(x => x.Personas - x.Horario.Cupo).ToList();
        File.WriteAllLines(Path.Combine(reportDirectory, "turnera_integrada_sobrecupos.csv"),
            new[] { "DIA;HORA;NIVEL;ALUMNOS;CUPO_NORMAL;EXCESO" }.Concat(sobrecupos.Select(x =>
                $"{x.Horario.Dia};{x.Horario.Hora};{x.Horario.Nivel};{x.Personas};{x.Horario.Cupo};{x.Personas - x.Horario.Cupo}")), Encoding.UTF8);

        Console.WriteLine($"Turnera oficial integrada: {Oficiales.Length} horarios, " +
            $"{fuentes.Count} relaciones activas MySQL, {asignaciones.Count} vinculadas, " +
            $"{noVinculados.Count} sin ficha, {extra.Count} turnos históricos fuera de grilla dados de baja, " +
            $"{sobrecupos.Count} sobrecupos. Sin alterar asistencias anteriores.");
    }
}
