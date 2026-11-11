using System.Text;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Migrador;

public sealed record LegacyTurnoVigente(long PersonaId, int TurnoId, int Dia, string Hora,
    string NivelOriginal, string TurnoActivo);

// Reparación puntual de la base de pruebas YA poblada, NO un migrador de producción.
public static class TurneraTestFix
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

    public static async Task RunAsync(AppDbContext db, LegacyRepository legacy,
        string reportDir, bool apply, DateOnly corte, bool authorizeProduction = false, CancellationToken ct = default)
    {
        if (!string.Equals(db.Database.GetDbConnection().Database,
                "BebesNadadores_MigracionTest", StringComparison.OrdinalIgnoreCase) && apply)
            ProductionCommitGuard.Check(db, authorizeProduction);
        Directory.CreateDirectory(reportDir);
        if (Oficiales.Length != 50 || Oficiales.Select(x => x.Clave).Distinct().Count() != 50)
            throw new InvalidOperationException("Grilla oficial inconsistente.");

        var fuentes = await legacy.TurnosVigentesAsync(ct);
        var levels = await db.Niveles.AsNoTracking().ToListAsync(ct);
        var ciclo = await db.CiclosLectivos.SingleAsync(x => x.Anio == 2026, ct);
        var allTurns = await db.Turnos.Where(x => x.CicloLectivoId == ciclo.Id).ToListAsync(ct);
        var inscritos = await db.Inscripciones.AsNoTracking()
            .Where(x => x.FechaInicio.Year == 2026).ToListAsync(ct);
        var idsInscripcionesImportadas = inscritos.Where(x =>
            (x.Observaciones ?? "").Contains("Migrado desde sistema anterior"))
            .Select(x => x.Id).ToHashSet();
        if (idsInscripcionesImportadas.Count < 500)
            throw new InvalidOperationException("No parece una base test importada. No se modifica nada.");

        var historial = await db.HistorialAlumnosLegacy.AsNoTracking()
            .Where(x => x.BebeId != null)
            .Select(x => new { x.LegacyPersonaId, x.BebeId }).ToListAsync(ct);
        var historialMap = historial.GroupBy(x => x.LegacyPersonaId)
            .ToDictionary(x => x.Key, x => x.First().BebeId!.Value);
        var inscripcionesPorBebe = inscritos
            .GroupBy(x => x.BebeId)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(z => z.Estado == EstadoInscripcion.Activa)
                .ThenByDescending(z => z.FechaInicio).First());

        var mapaOficial = Oficiales.ToDictionary(x => x.Clave);
        var nivelPorNombre = levels.ToDictionary(x => x.Nombre, StringComparer.OrdinalIgnoreCase);
        foreach (var h in Oficiales)
            if (!nivelPorNombre.ContainsKey(h.Nivel))
                throw new InvalidOperationException($"Falta nivel maestro '{h.Nivel}'.");

        // La clave de la fuente es IDTURNO (relación real del alumno), validada contra día/hora/nivel.
        var porTurnoFuente = new Dictionary<int, Horario>();
        var issues = new List<string>();
        foreach (var item in fuentes)
        {
            if (!TimeOnly.TryParse(item.Hora, out var hora))
            {
                issues.Add($"Hora inválida: turno={item.TurnoId}");
                continue;
            }
            var key = $"{item.Dia}|{hora:HH:mm}";
            if (!mapaOficial.TryGetValue(key, out var h))
            {
                issues.Add($"Fuera de grilla oficial: turno={item.TurnoId}, persona={item.PersonaId}, {key}");
                continue;
            }
            // El nivel del IDTURNO debe coincidir con el afiche oficial.
            var real = MigrationHelpers.ResolveLevel(item.NivelOriginal,
                corte, null, levels);
            if (real is null || !string.Equals(real.Nombre, h.Nivel, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Nivel diferente: turno={item.TurnoId}, fuente='{item.NivelOriginal}', oficial='{h.Nivel}'");
                continue;
            }
            if (!string.Equals(item.TurnoActivo, "S", StringComparison.OrdinalIgnoreCase))
            {
                issues.Add($"Asignación activa en turno deshabilitado en origen: turno={item.TurnoId}");
                continue;
            }
            porTurnoFuente[item.TurnoId] = h;
        }
        // Si hay discrepancias de día/nivel/horario, detener: no crear vínculos inventados.
        if (issues.Count > 0)
        {
            File.WriteAllLines(Path.Combine(reportDir, "turnera_discrepancias_bloqueantes.txt"), issues, Encoding.UTF8);
            throw new InvalidOperationException($"Se encontraron {issues.Count} discrepancias entre MySQL y la grilla. Ver reporte. Sin cambios.");
        }

        var enlaces = new List<(long Persona, int LegacyTurno, Guid InscripcionId, Horario Horario)>();
        var sinFicha = new List<string>();
        foreach (var item in fuentes)
        {
            var bebeId = MigrationHelpers.StableGuid($"bebe:{item.PersonaId}");
            if (!inscripcionesPorBebe.ContainsKey(bebeId) && historialMap.TryGetValue(item.PersonaId, out var linked))
                bebeId = linked;
            if (!inscripcionesPorBebe.TryGetValue(bebeId, out var ins))
            {
                sinFicha.Add($"{item.PersonaId};{item.TurnoId};SIN_INSCRIPCION_OPERATIVA");
                continue;
            }
            if (!idsInscripcionesImportadas.Contains(ins.Id))
            {
                sinFicha.Add($"{item.PersonaId};{item.TurnoId};INSCRIPCION_NO_IMPORTADA");
                continue;
            }
            enlaces.Add((item.PersonaId, item.TurnoId, ins.Id, porTurnoFuente[item.TurnoId]));
        }
        File.WriteAllLines(Path.Combine(reportDir, "turnera_sin_ficha_v4_2.csv"),
            new[] { "IDPERSONA;IDTURNO;MOTIVO" }.Concat(sinFicha), Encoding.UTF8);

        // No tocar historiales, asistencia ni pagos. Los turnos antiguos solo se dan de baja.
        var nuevos = new List<Turno>();
        var oficiales = new Dictionary<string, Turno>();
        foreach (var h in Oficiales)
        {
            var nivelId = nivelPorNombre[h.Nivel].Id;
            var hour = TimeOnly.Parse(h.Hora);
            var candidates = allTurns.Where(x => (int)x.DiaSemana == h.Dia &&
                x.HoraInicio == hour && x.NivelId == nivelId).ToList();
            if (candidates.Count > 1)
                throw new InvalidOperationException($"Turnos duplicados en PostgreSQL: {h.Clave} {h.Nivel}");
            var t = candidates.FirstOrDefault();
            if (t is null)
            {
                t = new Turno
                {
                    Id = MigrationHelpers.StableGuid($"turno:2026:{h.Dia}:{hour:HHmm}:{nivelId}"),
                    DiaSemana = (DiaSemana)h.Dia, HoraInicio = hour, NivelId = nivelId,
                    CicloLectivoId = ciclo.Id, VigenteDesde = new DateOnly(2026, 2, 1),
                    CreadoUtc = DateTimeOffset.UtcNow
                };
                if (allTurns.Any(x => x.Id == t.Id))
                    throw new InvalidOperationException($"Colisión GUID en turno {h.Clave}");
                nuevos.Add(t);
            }
            else if (!t.Nombre.StartsWith("Legacy", StringComparison.OrdinalIgnoreCase) &&
                     !t.Nombre.StartsWith("Oficial 2026", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Turno ajeno al migrador en horario {h.Clave}. Se requiere revisión.");
            t.Nombre = $"Oficial 2026 - {((DiaSemana)h.Dia)} {h.Hora} - {h.Nivel}";
            t.DuracionMinutos = h.Duracion;
            t.Cupo = h.Cupo;
            t.Estado = EstadoTurno.Activo;
            t.VigenteHasta = null;
            oficiales[h.Clave] = t;
        }
        var officialIds = oficiales.Values.Select(x => x.Id).ToHashSet();
        var extras = allTurns.Where(x => !officialIds.Contains(x.Id) &&
            (x.Nombre.StartsWith("Legacy", StringComparison.OrdinalIgnoreCase) ||
             x.Nombre.StartsWith("Oficial 2026", StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var t in extras) { t.Estado = EstadoTurno.Baja; t.VigenteHasta = corte; }

        var over = enlaces.GroupBy(x => x.Horario.Clave).Select(g => new
        {
            Horario = mapaOficial[g.Key], Cantidad = g.Select(x => x.Persona).Distinct().Count()
        }).Where(x => x.Cantidad > x.Horario.Cupo).OrderByDescending(x => x.Cantidad - x.Horario.Cupo).ToList();
        var overCsv = new[] { "DIA;HORA;NIVEL;ALUMNOS;CUPO_NORMAL;EXCESO" }.Concat(over.Select(x =>
            $"{x.Horario.Dia};{x.Horario.Hora};{x.Horario.Nivel};{x.Cantidad};{x.Horario.Cupo};{x.Cantidad - x.Horario.Cupo}"));
        File.WriteAllLines(Path.Combine(reportDir, "turnera_sobrecupos_v4_2.csv"), overCsv, Encoding.UTF8);

        var oldLinks = await db.InscripcionesTurnos
            .Where(x => idsInscripcionesImportadas.Contains(x.InscripcionId))
            .ToListAsync(ct);
        Console.WriteLine($"Turnos oficiales: {Oficiales.Length}; origen activo: {fuentes.Count}; " +
                          $"vinculables: {enlaces.Count}; sin ficha: {sinFicha.Count}; " +
                          $"vínculos erróneos actuales: {oldLinks.Count}; turnos extra a dar de baja: {extras.Count}; " +
                          $"sobrecupos reales: {over.Count}.");
        Console.WriteLine($"Modo: {(apply ? "APLICAR EN DESTINO CONFIRMADO" : "VISTA PREVIA - SIN MODIFICAR")}");
        if (!apply) return;

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            // Solo los vínculos de inscripciones creadas por la migración; no usuarios ni maestros.
            db.InscripcionesTurnos.RemoveRange(oldLinks);
            await db.SaveChangesAsync(ct);
            db.Turnos.AddRange(nuevos);
            await db.SaveChangesAsync(ct);
            foreach (var link in enlaces.DistinctBy(x => (x.InscripcionId, x.Horario.Clave)))
                db.InscripcionesTurnos.Add(new InscripcionTurno
                {
                    InscripcionId = link.InscripcionId,
                    TurnoId = oficiales[link.Horario.Clave].Id,
                    EsPrincipal = true
                });
            // No se borran sesiones históricas. Limpieza conservadora de sesiones FUTURAS
            // generadas en turnos Legacy ya dados de baja, solo cuando no tienen relaciones.
            var extraIds = extras.Select(x => x.Id).ToHashSet();
            var futurasExtras = await db.SesionesClase
                .Where(s => extraIds.Contains(s.TurnoId) && s.Fecha > corte &&
                            !s.EsModificadaManualmente).ToListAsync(ct);
            var eliminadas = 0;
            var conservadas = 0;
            foreach (var s in futurasExtras)
            {
                var ocupada = await db.Asistencias.AnyAsync(x => x.SesionClaseId == s.Id, ct)
                    || await db.ReservasSesionesClase.AnyAsync(x => x.SesionClaseId == s.Id, ct)
                    || await db.InvitacionesVacante.AnyAsync(x => x.SesionClaseId == s.Id, ct)
                    || await db.SesionesClaseProfesores.AnyAsync(x => x.SesionClaseId == s.Id, ct);
                if (ocupada) { conservadas++; continue; }
                db.SesionesClase.Remove(s);
                eliminadas++;
            }
            // Ajustar snapshot de cupo SOLO para sesiones no modificadas posteriores al corte.
            var futureOfficial = await db.SesionesClase
                .Where(s => officialIds.Contains(s.TurnoId) && s.Fecha > corte
                            && !s.EsModificadaManualmente).ToListAsync(ct);
            var cuposPorId = oficiales.Values.ToDictionary(t => t.Id, t => t.Cupo);
            foreach (var s in futureOfficial) s.Cupo = cuposPorId[s.TurnoId];
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            Console.WriteLine($"COMMIT Turnera: {enlaces.Count} vínculos activos de origen, " +
                              $"{eliminadas} sesiones futuras sin dependencias eliminadas, " +
                              $"{conservadas} preservadas por tener vínculos. Histórico intacto.");
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); } catch { /* Conexión invalidada */ }
            throw;
        }
    }
}
