using BebesNadadores.Domain.Entities;

using BebesNadadores.Domain.Enums;

using BebesNadadores.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;



namespace BebesNadadores.Migrador;



public sealed class LegacyMigrationService(

    AppDbContext db,

    LegacyRepository legacy,

    MigrationReport report,

    MigrationOptions opt)

{

    private readonly Dictionary<long, Guid> _bebeIds = [];

    private readonly Dictionary<long, Guid> _responsableByPersona = [];

    private readonly Dictionary<(long Persona,int Year), Guid> _inscripcionIds = [];

    private List<Nivel> _levels = [];

    private List<Plan> _plans = [];



    public async Task RunAsync(CancellationToken ct = default)

    {

        Console.WriteLine("Leyendo MySQL legacy...");

        var personas = await legacy.PersonasAsync(ct);

        var resp = await legacy.ResponsablesAsync(ct);

        var clases = await legacy.ClasesAsync(opt.HistoricalFromYear,opt.FullYear,opt.Cutoff,ct);

        var pagos = await legacy.PagosAsync(opt.FullYear,opt.Cutoff,ct);

        var morosos = await legacy.MorososAsync(opt.FullYear,ct);

        var lista = await legacy.ListaEsperaAsync(opt.FullYear,opt.Cutoff,ct);
        // La Turnera vigente procede de turnopersona.ACTIVO=S, no de asistencias del año.
        var turnosVigentes = await legacy.TurnosVigentesAsync(ct);
        // V4: auditoría completa, sin modificar pagos, cuotas ni derechos a recuperar.
        var vinculosRecupero = await legacy.CodigosVinculoRecuperoAsync(opt.FullYear,opt.Cutoff,ct);
        var tablasCandidatas = await legacy.TablasCandidatasClasesAsync(ct);
        foreach (var (tabla, columnas) in tablasCandidatas)
            report.DateSourceCandidate(tabla, columnas,
                "Tabla candidata: verificar manualmente registros 2026-11-01 y 2026-11-02. No se asume que la información exista.");

        var codigosPorPersonaDia = clases.Where(x => x.Fecha.Year == opt.FullYear)
            .GroupBy(x => (x.PersonaId, x.Fecha))
            .ToDictionary(g => g.Key, g => g.ToList());
        foreach (var reserva in lista)
        {
            var mismoDia = codigosPorPersonaDia.GetValueOrDefault((reserva.PersonaId, reserva.Fecha)) ?? [];
            var directCode = vinculosRecupero.GetValueOrDefault(reserva.Id) ?? "";
            var codigos = string.Join(",", mismoDia.Select(x => x.Codigo).Distinct().OrderBy(x => x));
            // El circuito fue confirmado por la administrativa, pero los códigos NO.
            // La misma persona puede tener varios movimientos ese día; no consumimos derechos.
            var motivo = string.IsNullOrWhiteSpace(directCode)
                ? "Sin vínculo directo vigente a presenteausente; requiere conciliación manual."
                : "Vínculo directo encontrado, pero no identifica inequívocamente asistencia a la clase de recupero.";
            report.RecoveryAudit(reserva, directCode, codigos, mismoDia.Count,
                "PENDIENTE_HOMOLOGACION", motivo);
        }
        Console.WriteLine($"V4: {lista.Count:N0} recuperos/reservas auditados; vínculos directos con código: {vinculosRecupero.Values.Count(x => !string.IsNullOrWhiteSpace(x)):N0}; tablas candidatas noviembre: {tablasCandidatas.Count:N0}.");
        // Solo los movimientos de 2026 generan registros operativos.
        var relevant = clases.Where(x=>x.Fecha.Year==opt.FullYear).Select(x=>x.PersonaId)

            .Concat(personas.Where(x=>x.FechaInscripcion?.Year==opt.FullYear).Select(x=>x.Id))

            .Concat(pagos.Select(x=>x.PersonaId))

            .Concat(morosos.Select(x=>x.PersonaId))

            .Concat(lista.Select(x=>x.PersonaId))
            .Concat(turnosVigentes.Select(x=>x.PersonaId))

            .ToHashSet();



        // El origen tiene 31 niños registrados con IDTIPOPERSONA=1.
        // Solo recuperamos los que tienen movimientos 2026; se conserva la advertencia.
        var originIds = personas.Select(x => x.Id).ToHashSet();
        var candidates = relevant.Where(x => !originIds.Contains(x)).ToArray();
        var adicionales = await legacy.PersonasAdicionalesAsync(candidates, ct);
        foreach (var p in adicionales)
        {
            report.ReclassifiedPerson(p.Id, p.ApellidoNombre, p.FechaNacimiento?.ToString("yyyy-MM-dd") ?? "",
                "IDTIPOPERSONA=1 con actividad 2026: validar tipo original antes de producción");
        }
        personas.AddRange(adicionales);

        // Auditar solo identificadores que realmente no existen en el origen.
        var personIds = personas.Select(x => x.Id).ToHashSet();
        foreach (var id in relevant.Where(x => !personIds.Contains(x)).OrderBy(x => x))
            report.MissingPerson(id, "Movimiento 2026 sin registro de alumno IDTIPOPERSONA=2");

        // Fecha de nacimiento es obligatoria en Bebes. Excluir de la operatoria y pedir validación.
        var invalidBirth = personas.Where(x => relevant.Contains(x.Id) &&
            (x.FechaNacimiento is null || x.FechaNacimiento.Value.Year <= 1901 ||
             x.FechaNacimiento.Value.Year > opt.FullYear)).ToList();
        foreach (var p in invalidBirth)
            report.InvalidBirth(p.Id, p.ApellidoNombre, p.FechaNacimiento?.ToString("yyyy-MM-dd") ?? "", "No se creó ficha operativa: nacimiento inválido");
        var invalidIds = invalidBirth.Select(x => x.Id).ToHashSet();
        var validOperational = personas.Where(x => relevant.Contains(x.Id) && !invalidIds.Contains(x.Id)).ToList();

        Console.WriteLine($"Personas legacy: {personas.Count:N0} (adicionales tipo 1: {adicionales.Count:N0}); relevantes 2026: {relevant.Count:N0}; sin ficha origen: {relevant.Count(x => !personIds.Contains(x)):N0}; nacimientos inválidos: {invalidBirth.Count:N0}");

        Console.WriteLine($"Clases {opt.HistoricalFromYear}-{opt.FullYear}: {clases.Count:N0}; pagos {opt.FullYear}: {pagos.Count:N0}; deuda: {morosos.Count:N0}");

        var lastArchive=clases.Where(x=>x.Fecha.Year==opt.FullYear).Select(x=>(DateOnly?)x.Fecha).Max();

        if (opt.Cutoff is not null && lastArchive is not null && lastArchive < opt.Cutoff)

            report.Warn("ASISTENCIA_2026","TRAMO_SIN_ARCHIVAR",$"Última clase en historicopersonaclase: {lastArchive:yyyy-MM-dd}. El corte es {opt.Cutoff:yyyy-MM-dd}; revisar las clases de la tabla operativa posterior al archivo. No se importan ausencias A precargadas.");



        await db.Database.OpenConnectionAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        try

        {

            // Preparar los niveles oficiales antes de resolver niveles de alumnos o turnos.
            // Se ejecuta DENTRO de la transacción: --dry-run también revierte estos maestros.
            await EnsureOfficialLevelsAsync(ct);

            _levels = await db.Niveles.AsNoTracking().OrderBy(x=>x.Orden).ToListAsync(ct);

            _plans = await db.Planes.AsNoTracking().OrderBy(x=>x.VecesPorSemana).ToListAsync(ct);

            if (_levels.Count == 0 || _plans.Count == 0)
                throw new InvalidOperationException("La base destino debe tener Niveles y Planes cargados.");



            await EnsureCyclesAsync(ct);

            await MigrateBabiesAsync(validOperational, clases, ct);

            await MigrateResponsiblesAsync(personas, resp, relevant, ct);

            await MigrateHistoricalArchiveAsync(personas, clases, ct);

            await MigrateFullYearClassesAsync(personas, clases.Where(x=>x.Fecha.Year==opt.FullYear).ToList(), ct);

            await MigratePaymentsAsync(pagos, morosos, ct);

            // Crear inscripciones faltantes de personas ACTIVAS del origen que no
            // necesariamente tuvieron una clase archivada ni un pago en el período.
            foreach (var personaId in turnosVigentes.Select(x => x.PersonaId).Distinct())
                if (_bebeIds.TryGetValue(personaId, out var bebeId))
                    await EnsureFullYearInscriptionAsync(personaId, bebeId, null, ct);
            await db.SaveChangesAsync(ct);

            // V4.3: turnos vigentes y cupos desde la grilla oficial y ACTIVO='S'.
            // Se ejecuta DENTRO de la misma transacción de migración, por lo que
            // --dry-run revierte también todas estas asignaciones.
            await TurneraOficialMigracion.IntegrarAsync(db, turnosVigentes,
                _bebeIds, _inscripcionIds, opt.FullYear, opt.ReportDirectory, ct);

            ExportRecoveryCandidates(lista);

            // En la primera versión mejorada los casos no homologados se conservan en CSV.
            // Evitar que una ejecución real deje datos financieros/asistencias incompletos sin aviso.
            await db.SaveChangesAsync(ct);
            report.Save(); // Aun con pendientes se conservan los CSV para conciliación.

            // No resolver diferencias históricas inventando créditos ni deudas.
            // Solo un commit productivo con destino explícitamente habilitado puede continuar;
            // los registros no homologados permanecen en reportes para revisión.
            var isMigrationTest = string.Equals(db.Database.GetDbConnection().Database,
                "BebesNadadores_MigracionTest", StringComparison.OrdinalIgnoreCase);
            if (!opt.DryRun && !isMigrationTest)
                ProductionCommitGuard.Check(db, opt.ConfirmProduction);
            if (!opt.DryRun && report.HasBlockingIssues)
                Console.WriteLine($"REVISIÓN ADMINISTRATIVA: {report.BlockingCount:N0} incidencias históricas exportadas; no se crean valores supuestos.");



            if(opt.DryRun)

            {

                await tx.RollbackAsync(ct);

                Console.WriteLine("DRY-RUN finalizado: ROLLBACK realizado. No se modificó PostgreSQL.");

            }

            else

            {

                await tx.CommitAsync(ct);

                Console.WriteLine("Migración confirmada (COMMIT)." );

            }

        }

        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error durante la migración: {ex.Message}");
            try
            {
                await tx.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackEx)
            {
                // La conexión puede invalidar la transacción tras fallar una escritura.
                Console.Error.WriteLine($"Rollback no disponible: {rollbackEx.Message}");
            }

            throw;
        }
    }


    // Niveles oficiales de la grilla 2026. Se crean solo si faltan; no se reescriben
    // configuraciones existentes. Los rangos de edad son propuestas operativas a
    // confirmar con la administración antes del COMMIT productivo.
    private async Task EnsureOfficialLevelsAsync(CancellationToken ct)
    {
        var niveles = await db.Niveles.ToListAsync(ct);

        var viejo = niveles.FirstOrDefault(x =>
            string.Equals(x.Nombre, "3 a 12 meses", StringComparison.OrdinalIgnoreCase));
        var canonico = niveles.FirstOrDefault(x =>
            string.Equals(x.Nombre, "Bebés 3 a 12 meses", StringComparison.OrdinalIgnoreCase));

        if (viejo is not null && canonico is not null && viejo.Id != canonico.Id)
            throw new InvalidOperationException(
                "Existen dos niveles equivalentes de 3 a 12 meses; revisar antes de importar.");

        if (viejo is not null && canonico is null)
        {
            viejo.Nombre = "Bebés 3 a 12 meses";
            Console.WriteLine("Nivel maestro existente renombrado: Bebés 3 a 12 meses");
        }

        // Nombre, orden, edad mínima/máxima en meses, duración y cupo.
        var oficiales = new (string Nombre, int Orden, int Min, int Max, int Duracion, int Cupo)[]
        {
            ("Bebés 3 a 12 meses", 1, 3, 12, 30, 10),
            ("Bebés 12 a 24 meses", 2, 12, 24, 30, 10),
            ("2 años", 3, 24, 35, 45, 10),
            ("3 y 4 años", 4, 36, 59, 45, 12),
            ("3 a 6 años", 5, 36, 83, 45, 12),
            ("5 y 6 años", 6, 60, 83, 45, 12)
        };

        foreach (var nivel in oficiales)
        {
            if (niveles.Any(x => string.Equals(x.Nombre, nivel.Nombre,
                    StringComparison.OrdinalIgnoreCase)))
                continue;

            var nuevo = new Nivel
            {
                Id = MigrationHelpers.StableGuid($"nivel-oficial:{nivel.Nombre}"),
                Nombre = nivel.Nombre,
                Orden = nivel.Orden,
                EdadMinMeses = nivel.Min,
                EdadMaxMeses = nivel.Max,
                DuracionMinutos = nivel.Duracion,
                CupoPredeterminado = nivel.Cupo,
                ProfesoresMinimos = 1,
                ProfesoresMaximos = 2,
                Activo = true,
                CreadoUtc = DateTimeOffset.UtcNow
            };

            db.Niveles.Add(nuevo);
            niveles.Add(nuevo);
            Console.WriteLine($"Nivel maestro creado: {nivel.Nombre}");
        }

        await db.SaveChangesAsync(ct);
    }


    private async Task EnsureCyclesAsync(CancellationToken ct)

    {

        var existing=await db.CiclosLectivos.ToDictionaryAsync(x=>x.Anio,ct);

        for(var y=opt.FullYear;y<=opt.FullYear;y++)

        {

            if(existing.ContainsKey(y)) continue;

            db.CiclosLectivos.Add(new CicloLectivo

            {

                Id=MigrationHelpers.StableGuid($"cycle:{y}"), Anio=y,

                FechaInicio=new DateOnly(y,1,1), FechaFin=new DateOnly(y,12,31),

                Estado=y<opt.FullYear?EstadoCicloLectivo.Cerrado:EstadoCicloLectivo.Publicado,

                Observaciones="Creado por migración legacy. Fechas administrativas; el histórico real está en las inscripciones.",

                CreadoUtc=DateTimeOffset.UtcNow

            });

        }

        await db.SaveChangesAsync(ct);

    }



    private async Task MigrateBabiesAsync(List<LegacyPersona> personas,List<LegacyClase> clases,CancellationToken ct)

    {

        var current=await db.Bebes.AsNoTracking().ToListAsync(ct);

        var lastClass=clases.GroupBy(x=>x.PersonaId).ToDictionary(g=>g.Key,g=>g.OrderByDescending(x=>x.Fecha).First());

        foreach(var p in personas)

        {

            var split=MigrationHelpers.SplitLegacyName(p.ApellidoNombre);

            if(split.Dudoso) report.NameReview(p.Id,p.ApellidoNombre,split.Nombre,split.Apellido,"bebe");

            var legacyTokens = Tokens(p.ApellidoNombre);

            var existing=current.FirstOrDefault(x=>x.FechaNacimiento==p.FechaNacimiento &&

                (Norm(x.Nombre)==Norm(split.Nombre) && Norm(x.Apellido)==Norm(split.Apellido)

                 || Tokens($"{x.Apellido} {x.Nombre}").SetEquals(legacyTokens)));

            if(existing is not null) { _bebeIds[p.Id]=existing.Id; continue; }



            var id=MigrationHelpers.StableGuid($"bebe:{p.Id}");

            if(current.Any(x=>x.Id==id)) { _bebeIds[p.Id]=id; continue; }

            var lc=lastClass.GetValueOrDefault(p.Id);

            var level=lc is null?null:MigrationHelpers.ResolveLevel(lc.NivelNombre,lc.Fecha,p.FechaNacimiento,_levels);

            var alta=p.FechaInscripcion is { Year: > 1901 } fi ? DateOnly.FromDateTime(fi) : (lc?.Fecha ?? new DateOnly(opt.HistoricalFromYear,1,1));

            var b=new Bebe

            {

                Id=id, Nombre=split.Nombre, Apellido=split.Apellido,

                FechaNacimiento=p.FechaNacimiento!.Value,

                Sexo=Sexo.NoInformado, Dni=null, FechaAlta=alta,

                Estado=p.Activo?EstadoBebe.Activo:EstadoBebe.Baja,

                FechaBaja=p.Activo?null:lc?.Fecha,

                MotivoBaja=p.Activo?null:"Estado inactivo en sistema anterior",

                NivelActualId=level?.Id,

                CreadoUtc=DateTimeOffset.UtcNow

            };

            db.Bebes.Add(b); _bebeIds[p.Id]=id;



        }

        await db.SaveChangesAsync(ct);

        Console.WriteLine($"Bebés vinculados/migrados: {_bebeIds.Count:N0}");

    }



    private async Task MigrateResponsiblesAsync(List<LegacyPersona> personas,List<LegacyResponsable> source,HashSet<long> relevant,CancellationToken ct)

    {

        var current=await db.Responsables.AsNoTracking().ToListAsync(ct);

        var pmap=personas.ToDictionary(x=>x.Id);

        foreach(var r in source.Where(x=>relevant.Contains(x.AlumnoId)).GroupBy(x=>x.AlumnoId).Select(g=>g.Last()))

        {

            if(!_bebeIds.TryGetValue(r.AlumnoId,out var bebeId)) continue;

            pmap.TryGetValue(r.AlumnoId,out var lp);

            var dni=MigrationHelpers.Digits(r.Dni); if(dni.Length<6) dni="";

            var existing=!string.IsNullOrEmpty(dni)?current.FirstOrDefault(x=>MigrationHelpers.Digits(x.Dni)==dni):null;

            Guid rid;

            if(existing is not null) rid=existing.Id;

            else

            {

                var s=MigrationHelpers.SplitLegacyName(r.Nombre);

                if(s.Dudoso)report.NameReview(r.AlumnoId,r.Nombre,s.Nombre,s.Apellido,"responsable");

                rid=MigrationHelpers.StableGuid(!string.IsNullOrEmpty(dni)?$"responsable:dni:{dni}":$"responsable:alumno:{r.AlumnoId}");

                if(!current.Any(x=>x.Id==rid))

                {

                    var tel=MigrationHelpers.Digits(lp?.TelefonoAlternativo); if(string.IsNullOrEmpty(tel)) tel=MigrationHelpers.Digits(lp?.TelefonoFijo);

                    db.Responsables.Add(new Responsable{Id=rid,Nombre=s.Nombre,Apellido=s.Apellido,Dni=string.IsNullOrEmpty(dni)?null:dni,

                        Telefono=tel,WhatsApp=tel,Email=lp?.Email??"",Domicilio=lp?.Domicilio,Activo=true,CreadoUtc=DateTimeOffset.UtcNow});

                }

            }

            _responsableByPersona[r.AlumnoId]=rid;

            var exists=await db.BebesResponsables.AnyAsync(x=>x.BebeId==bebeId && x.ResponsableId==rid,ct);

            if(!exists) db.BebesResponsables.Add(new BebeResponsable{BebeId=bebeId,ResponsableId=rid,Relacion=RelacionResponsable.Otro,EsContactoPrincipal=true,AutorizaComunicaciones=true});

        }

        await db.SaveChangesAsync(ct);

    }



    private async Task MigrateHistoricalArchiveAsync(List<LegacyPersona> personas,List<LegacyClase> clases,CancellationToken ct)

    {
        // Guardamos la primera inscripción incluso si ocurrió antes de 2019;        // solo almacenamos resúmenes de actividad entre 2019 y 2025.
        var pmap=personas.ToDictionary(x=>x.Id);

        var groups=clases.Where(x=>x.Fecha.Year>=opt.HistoricalFromYear && x.Fecha.Year<opt.FullYear)

            .GroupBy(x=>x.PersonaId).ToList();

        var existing=await db.HistorialAlumnosLegacy.AsNoTracking().ToDictionaryAsync(x=>x.LegacyPersonaId,ct);

        var existingYears=(await db.HistorialAlumnosAniosLegacy.AsNoTracking()

            .Select(x=>new {x.HistorialAlumnoLegacyId,x.Anio}).ToListAsync(ct))

            .Select(x=>(x.HistorialAlumnoLegacyId,x.Anio)).ToHashSet();

        var currentBabies=await db.Bebes.AsNoTracking().Select(x=>new {x.Id,x.Nombre,x.Apellido,x.FechaNacimiento}).ToListAsync(ct);

        foreach(var pg in groups)

        {

            if(!pmap.TryGetValue(pg.Key,out var p))continue;

            var split=MigrationHelpers.SplitLegacyName(p.ApellidoNombre);

            var id=existing.TryGetValue(p.Id,out var previous)?previous.Id:MigrationHelpers.StableGuid($"historial-alumno:{p.Id}");

            Guid? bebeId=_bebeIds.TryGetValue(p.Id,out var linked)?linked:null;

            if(bebeId is null && p.FechaNacimiento is not null)

            {

                var candidates=currentBabies.Where(x=>x.FechaNacimiento==p.FechaNacimiento &&

                    Tokens($"{x.Apellido} {x.Nombre}").SetEquals(Tokens(p.ApellidoNombre))).ToList();

                if(candidates.Count==1)bebeId=candidates[0].Id;

                else if(candidates.Count>1)report.Warn("HISTORIAL",p.Id.ToString(),"Coincidencia ambigua de ficha activa: no vinculada.");

            }

            if(previous is null)

            {

                db.HistorialAlumnosLegacy.Add(new HistorialAlumnoLegacy

                {

                    Id=id,LegacyPersonaId=p.Id,BebeId=bebeId,NombreOriginal=p.ApellidoNombre,

                    Nombre=split.Nombre,Apellido=split.Apellido,FechaNacimiento=p.FechaNacimiento,

                    FechaPrimeraInscripcion=p.FechaInscripcion is { Year: >1901 } fi?DateOnly.FromDateTime(fi):null

                });

            }

            else if(previous.BebeId is null && bebeId is not null)

            {

                var tracked=new HistorialAlumnoLegacy{Id=id,BebeId=bebeId};

                db.HistorialAlumnosLegacy.Attach(tracked);

                db.Entry(tracked).Property(x=>x.BebeId).IsModified=true;

            }

            foreach(var yg in pg.GroupBy(x=>x.Fecha.Year))

            {

                var idYear=MigrationHelpers.StableGuid($"historial-anio:{p.Id}:{yg.Key}");

                if(!existingYears.Add((id,yg.Key)))continue;

                var rows=yg.ToList();

                var levels=string.Join(", ",rows.Select(x=>x.NivelNombre).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct().Take(8));

                if(levels.Length>500)levels=levels[..500];

                db.HistorialAlumnosAniosLegacy.Add(new HistorialAlumnoAnioLegacy

                {

                    Id=idYear,HistorialAlumnoLegacyId=id,Anio=yg.Key,PrimeraClase=rows.Min(x=>x.Fecha),

                    UltimaClase=rows.Max(x=>x.Fecha),CantidadRegistros=rows.Count,Niveles=levels,

                    FrecuenciaEstimada=MigrationHelpers.Frequency(rows)

                });

            }

        }

        await db.SaveChangesAsync(ct);

        Console.WriteLine($"Archivo histórico: {groups.Count:N0} alumnos con actividad {opt.HistoricalFromYear}-{opt.FullYear-1}.");

    }



    private async Task MigrateFullYearClassesAsync(List<LegacyPersona> personas,List<LegacyClase> rows,CancellationToken ct)

    {

        var pmap=personas.ToDictionary(x=>x.Id);

        var cycle=await db.CiclosLectivos.SingleAsync(x=>x.Anio==opt.FullYear,ct);
        // Inscripción anual 2026 por toda persona con clase/pago se completa también en pagos si faltara.
        foreach(var g in rows.GroupBy(x=>x.PersonaId))

        {

            if(!_bebeIds.TryGetValue(g.Key,out var bebeId))continue;

            await EnsureFullYearInscriptionAsync(g.Key,bebeId,g.ToList(),ct);

        }

        await db.SaveChangesAsync(ct);



        var turnMap=new Dictionary<string,Turno>();

        var latestAvailable = rows.Count == 0 ? new DateOnly(opt.FullYear,1,1) : rows.Max(x=>x.Fecha);

        foreach(var g in rows.GroupBy(x=>TurnKey(x,pmap.GetValueOrDefault(x.PersonaId))))

        {

            var sample=g.First();

            var birth=pmap.GetValueOrDefault(sample.PersonaId)?.FechaNacimiento;

            var level=MigrationHelpers.ResolveLevel(sample.NivelNombre,sample.Fecha,birth,_levels);

            if(level is null){report.Warn("TURNO",g.Key,$"No se pudo mapear nivel '{sample.NivelNombre}'");continue;}

            var day=MigrationHelpers.NormalizeDay(sample.Dia); var time=MigrationHelpers.ParseTime(sample.Hora);

            if(day==0||time==default){report.Warn("TURNO",g.Key,"Día/hora inválidos");continue;}

            var id=MigrationHelpers.StableGuid($"turno:{opt.FullYear}:{(int)day}:{time:HHmm}:{level.Id}");

            var t=await db.Turnos.FirstOrDefaultAsync(x=>x.CicloLectivoId==cycle.Id && x.DiaSemana==day && x.HoraInicio==time && x.NivelId==level.Id,ct)

                  ?? await db.Turnos.FirstOrDefaultAsync(x=>x.Id==id,ct);

            if(t is null)

            {

                t=new Turno{Id=id,Nombre=$"Legacy {MigrationHelpers.ToTitle(sample.Dia)} {time:HH\\:mm} - {level.Nombre}",DiaSemana=day,HoraInicio=time,

                    DuracionMinutos=level.DuracionMinutos,NivelId=level.Id,Cupo=level.CupoPredeterminado,VigenteDesde=g.Min(x=>x.Fecha),

                    Estado=g.Max(x=>x.Fecha)>=latestAvailable.AddDays(-14)?EstadoTurno.Activo:EstadoTurno.Baja,

                    VigenteHasta=g.Max(x=>x.Fecha)>=latestAvailable.AddDays(-14)?null:g.Max(x=>x.Fecha),

                    CicloLectivoId=cycle.Id,CreadoUtc=DateTimeOffset.UtcNow}; db.Turnos.Add(t);

            }

            turnMap[g.Key]=t;

        }

        await db.SaveChangesAsync(ct);
        // V4.3: NO vincular turnos históricos a las inscripciones vigentes.
        // Los Turnos Legacy y sus SesionesClase se conservan para consultar las
        // asistencias pasadas. Los 50 turnos actuales se construyen más adelante
        // desde turnopersona.ACTIVO='S' y la grilla oficial.

        var sessions=new Dictionary<string,SesionClase>();

        foreach(var g in rows.GroupBy(x=>SessionKey(x,pmap.GetValueOrDefault(x.PersonaId))))

        {

            var s0=g.First(); var tk=TurnKey(s0,pmap.GetValueOrDefault(s0.PersonaId)); if(!turnMap.TryGetValue(tk,out var turno))continue;

            var sid=MigrationHelpers.StableGuid($"sesion:{g.Key}");

            var ses=await db.SesionesClase.FirstOrDefaultAsync(x=>x.TurnoId==turno.Id && x.Fecha==s0.Fecha,ct)

                    ?? await db.SesionesClase.FirstOrDefaultAsync(x=>x.Id==sid,ct);

            if(ses is null){ses=new SesionClase{Id=sid,TurnoId=turno.Id,Fecha=s0.Fecha,HoraInicio=turno.HoraInicio,DuracionMinutos=turno.DuracionMinutos,NivelId=turno.NivelId,Cupo=turno.Cupo,Sector=turno.Sector,Estado=EstadoSesionClase.Finalizada,CreadoUtc=DateTimeOffset.UtcNow};db.SesionesClase.Add(ses);}

            sessions[g.Key]=ses;

        }

        await db.SaveChangesAsync(ct);



        foreach(var g in rows.GroupBy(x=>new{x.PersonaId,Session=SessionKey(x,pmap.GetValueOrDefault(x.PersonaId))}))

        {

            var x=g.OrderByDescending(z=>z.Id).First(); if(!_bebeIds.TryGetValue(x.PersonaId,out var bebeId)||!_inscripcionIds.TryGetValue((x.PersonaId,opt.FullYear),out var insId)||!sessions.TryGetValue(g.Key.Session,out var ses))continue;

            var state=MigrationHelpers.Attendance(x.Codigo,out var rec,out var known);
            if (!known)
            {
                report.UnknownAttendance(x);
                continue; // Código no homologado: no crear falso Presente/Ausente/Pendiente operativo.
            }

            var id=MigrationHelpers.StableGuid($"asistencia:{x.Id}");

            if(await db.Asistencias.AnyAsync(a=>a.SesionClaseId==ses.Id&&a.BebeId==bebeId,ct))continue;

            db.Asistencias.Add(new Asistencia{Id=id,SesionClaseId=ses.Id,BebeId=bebeId,InscripcionId=insId,Estado=state,EsRecupero=rec,

                Observaciones=known?"Migrado desde sistema anterior.":$"Migrado sin equivalencia exacta. Código legacy: {x.Codigo}",RegistradaUtc=DateTimeOffset.UtcNow,CreadoUtc=DateTimeOffset.UtcNow});

        }

        await db.SaveChangesAsync(ct);

    }



    private async Task<Guid> EnsureFullYearInscriptionAsync(long personaId,Guid bebeId,List<LegacyClase>? classes,CancellationToken ct)

    {

        if(_inscripcionIds.TryGetValue((personaId,opt.FullYear),out var cached))return cached;

        var existing = await db.Inscripciones.AsNoTracking()

            .Where(x=>x.BebeId==bebeId && x.FechaInicio.Year==opt.FullYear)

            .OrderByDescending(x=>x.Estado==EstadoInscripcion.Activa)

            .ThenByDescending(x=>x.FechaInicio)

            .FirstOrDefaultAsync(ct);

        if(existing is not null)

        {

            _inscripcionIds[(personaId,opt.FullYear)]=existing.Id;

            return existing.Id;

        }

        var id=MigrationHelpers.StableGuid($"inscripcion:{personaId}:{opt.FullYear}"); _inscripcionIds[(personaId,opt.FullYear)]=id;

        if(await db.Inscripciones.AnyAsync(x=>x.Id==id,ct))return id;

        var rows=classes??[]; var freq=rows.Count==0?1:MigrationHelpers.Frequency(rows); var plan=_plans.OrderBy(x=>Math.Abs(x.VecesPorSemana-freq)).First();

        if (rows.Count == 0)
            report.Warn("INSCRIPCION",personaId.ToString(),"Inscripción 2026 inferida solo de pagos/deuda. Fecha y plan requieren verificación.");
        db.Inscripciones.Add(new Inscripcion{Id=id,BebeId=bebeId,PlanId=plan.Id,FechaInicio=rows.Count>0?rows.Min(x=>x.Fecha):new DateOnly(opt.FullYear,1,1),

            FechaFin=null,Estado=EstadoInscripcion.Activa,MedioPagoPreferido=MedioPago.Otro,

            Observaciones="Migrado desde sistema anterior. Inscripción 2026 reconstruida a partir de actividad/pagos.",CreadoUtc=DateTimeOffset.UtcNow});

        return id;

    }



    private async Task MigratePaymentsAsync(List<LegacyPago> pagos,List<LegacyMoroso> morosos,CancellationToken ct)

    {

        foreach(var person in pagos.Select(x=>x.PersonaId).Concat(morosos.Select(x=>x.PersonaId)).Distinct())

            if(_bebeIds.TryGetValue(person,out var bid)) await EnsureFullYearInscriptionAsync(person,bid,null,ct);

        await db.SaveChangesAsync(ct);



        // Clasificación confirmada por la tabla de catálogo tipopago de MySQL.
        // Tipo 1 = CUOTA; tipo 5 = CLASE; tipo 6 = 1/2 MES.
        // No se crean cuotas ni imputaciones porque el importe tarifario original
        // y la semántica de "moroso" todavía requieren conciliación.
        var agrupados = pagos.Where(x => x.Mes is >= 1 and <= 12)
            .GroupBy(x => (x.PersonaId, x.Mes)).ToDictionary(g => g.Key, g => g.ToList());
        var deudas = morosos.Where(x => x.Mes is >= 1 and <= 12)
            .GroupBy(x => (x.PersonaId, x.Mes)).ToDictionary(g => g.Key, g => g.ToList());
        foreach (var key in agrupados.Keys.Concat(deudas.Keys).Distinct().OrderBy(x => x.PersonaId).ThenBy(x => x.Mes))
        {
            agrupados.TryGetValue(key, out var pagados);
            deudas.TryGetValue(key, out var impagos);
            pagados ??= [];
            impagos ??= [];
            var mensual = pagados.Where(x => x.TipoPagoId == 1).Sum(x => x.Importe);
            var clase = pagados.Where(x => x.TipoPagoId == 5).Sum(x => x.Importe);
            var medioMes = pagados.Where(x => x.TipoPagoId == 6).Sum(x => x.Importe);
            var otros = pagados.Where(x => x.TipoPagoId is not (1 or 5 or 6)).Sum(x => x.Importe);
            var deuda = impagos.Sum(x => x.Importe);
            report.MonthlyReconciliation(key.PersonaId, opt.FullYear, key.Mes, mensual, clase, medioMes, otros, deuda,
                pagados.Count, impagos.Count);
            report.QuotaReview(key.PersonaId, key.Mes, opt.FullYear,
                mensual, deuda, "CUOTA=1; CLASE=5 y 1/2 MES=6 separados. Sin imputación automática.");
            if (mensual > 0 && deuda > 0)
                report.FinancialRisk(key.PersonaId, key.Mes, mensual, deuda,
                    "Cobros CUOTA y morosidad en el mismo mes: no asumir que deuda es saldo actual sin validar su origen.");
            if (deuda < 0)
                report.FinancialRisk(key.PersonaId, key.Mes, mensual, deuda,
                    "Morosidad negativa: verificar ajuste o crédito.");
        }

        // Cuadratura 100% de filas leídas, incluidas cero y negativas, anticipos y sin ficha.
        foreach (var p in pagos)
        {
            var result = p.Importe < 0 ? "NEGATIVO_NO_OPERATIVO"
                : p.Importe == 0 ? "CERO_NO_OPERATIVO"
                : !_bebeIds.ContainsKey(p.PersonaId) ? "SIN_FICHA_OPERATIVA"
                : "PAGO_POSITIVO_CANDIDATO";
            report.PaymentComplete(p, result);
        }
        Console.WriteLine($"V4: pagos leídos {pagos.Count:N0}; positivos {pagos.Count(x=>x.Importe>0):N0}; " +
            $"cero {pagos.Count(x=>x.Importe==0):N0}; negativos {pagos.Count(x=>x.Importe<0):N0}.");

        foreach(var p in pagos.Where(x=>x.Importe>0))

        {

            if(!_bebeIds.TryGetValue(p.PersonaId,out var bid))
            {
                report.UnlinkedPayment(p.Id, p.PersonaId, p.Importe, "Alumno sin ficha operativa válida");
                continue;
            }

            var pid=MigrationHelpers.StableGuid($"pago:{p.Id}"); if(await db.Pagos.AnyAsync(x=>x.Id==pid,ct))continue;

            var pay=new Pago{Id=pid,BebeId=bid,ResponsableId=_responsableByPersona.TryGetValue(p.PersonaId,out var respId)?respId:null,FechaUtc=LegacyLocalToUtc(p.FechaPago),

                Importe=p.Importe,MedioPago=MedioPago.Otro,ReferenciaExterna=$"LEGACY-PAGO-{p.Id}",Observaciones=$"Migrado. Concepto: {LegacyPaymentType(p.TipoPagoId)}; tipoId={p.TipoPagoId}; periodo={p.AnioPago}-{p.Mes:00}; turnos={p.TurnosPago}; descuento={p.Descuento?.ToString()??"-"}. Sin imputación a cuota.",CreadoUtc=DateTimeOffset.UtcNow};

            db.Pagos.Add(pay);

            // Sin imputación hasta reconciliar la naturaleza del pago y el saldo de cuota.
            report.PaymentAudit(p.Id, p.PersonaId, p.AnioPago, p.Mes, p.Importe, p.FechaPago, LegacyPaymentType(p.TipoPagoId), p.TipoPagoId);

        }

        await db.SaveChangesAsync(ct);

    }



    private static string LegacyPaymentType(long id) => id switch
    {
        1 => "CUOTA",
        2 => "MATRICULA",
        3 => "COLONIA",
        4 => "SUELDO",
        5 => "CLASE",
        6 => "MEDIO_MES",
        9 => "JORNADA_DISCAPACIDAD_I",
        10 => "JORNADA_COLONIA",
        11 => "C_A_GRUPO_1",
        12 => "C_A_GRUPO_2",
        15 => "JORNADA_DISCAPACIDAD_II",
        18 => "C_A_GRUPO_3",
        _ => $"SIN_CLASIFICAR_{id}"
    };

    private void ExportRecoveryCandidates(List<LegacyListaEspera> lista)

    {

        foreach(var x in lista) report.Waitlist(x);

        if(lista.Count>0) report.Warn("RECUPEROS","2026",$"{lista.Count} registros de recuperos/reservas del origen quedan para conciliación. Circuito confirmado: ausencia con aviso -> derecho -> reserva -> presente (consume) / nuevo aviso (reprograma) / ausente sin aviso (consume). Los códigos y enlaces de origen no permiten adjudicar estos resultados automáticamente. No se generan recuperos disponibles ni reservas automáticas.");

    }



    // Las fechas de MySQL representan hora local argentina. PostgreSQL timestamptz exige UTC.
    // No usar DateTimeOffset(-03:00) directamente en una columna timestamp with time zone.
    private static DateTimeOffset LegacyLocalToUtc(DateTime localDateTime)
    {
        if (localDateTime.Kind == DateTimeKind.Utc)
            return new DateTimeOffset(localDateTime);

        var argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");
        var unspecified = DateTime.SpecifyKind(localDateTime, DateTimeKind.Unspecified);
        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, argentina);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }

    private string TurnKey(LegacyClase x,LegacyPersona? p)

    {

        var level=MigrationHelpers.ResolveLevel(x.NivelNombre,x.Fecha,p?.FechaNacimiento,_levels);

        return $"{opt.FullYear}|{(int)MigrationHelpers.NormalizeDay(x.Dia)}|{MigrationHelpers.ParseTime(x.Hora):HHmm}|{level?.Id}";

    }

    private string SessionKey(LegacyClase x,LegacyPersona? p)=>$"{TurnKey(x,p)}|{x.Fecha:yyyyMMdd}";

    private static string Norm(string s)=>MigrationHelpers.Strip(s).Replace(" ","");

    private static HashSet<string> Tokens(string s)=>MigrationHelpers.Strip(s).Split(' ',StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);

}
