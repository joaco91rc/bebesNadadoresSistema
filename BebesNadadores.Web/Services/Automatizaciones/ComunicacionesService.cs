using System.Globalization;
using System.Net.Mail;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using BebesNadadores.Web.Services.Comprobantes;
using BebesNadadores.Web.Services.Cumpleanos;
using BebesNadadores.Web.Services.Finanzas;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Web.Services.Automatizaciones;

public sealed class ConfigComunicacion
{
    public string Tipo { get; set; } = "";
    public bool Habilitada { get; set; }
    public DateTimeOffset DesdeUtc { get; set; }
}
public sealed class RegistroComunicacion
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = "";
    public string Clave { get; set; } = "";
    public Guid BebeId { get; set; }
    public string Destinatario { get; set; } = "";
    public string Estado { get; set; } = "";
    public DateTimeOffset CreadoUtc { get; set; }
    public DateTimeOffset? FinalizadoUtc { get; set; }
}
public sealed record MensajeComunicacion(Guid Id, string Destinatario, string Asunto,
    string Nombre, string Mensaje, string? Enlace, string? TextoEnlace);

public sealed class ComunicacionesService(
    IDbContextFactory<AppDbContext> factory,
    IConfiguration config,
    IIndicadorFinancieroService finanzas,
    EnlaceComprobantePagoService comprobantes,
    TarjetaCumpleanosService cumpleanos)
{
    public static readonly string[] Tipos = ["ausencias", "pagos", "morosos", "cumpleanos", "recuperos"];
    public bool HabilitadaGlobal => config.GetValue("Automatizaciones:Habilitadas", false);
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private sealed record Candidato(Guid BebeId, string Clave, string Asunto, string Mensaje, Guid? PagoId = null, bool Cumple = false);

    public async Task<List<ConfigComunicacion>> ConfiguracionAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Database.SqlQueryRaw<ConfigComunicacion>("SELECT * FROM \"ConfiguracionComunicaciones\"").ToListAsync(ct);
    }
    public async Task CambiarAsync(string tipo, bool habilitada, CancellationToken ct = default)
    {
        if (!Tipos.Contains(tipo)) throw new ArgumentException("Tipo inválido.");
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ConfiguracionComunicaciones" SET "Habilitada"={habilitada},
            "DesdeUtc"=CASE WHEN {habilitada} AND NOT "Habilitada" THEN CURRENT_TIMESTAMP ELSE "DesdeUtc" END
            WHERE "Tipo"={tipo}
            """, ct);
    }
    public async Task<List<RegistroComunicacion>> HistorialAsync(CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        return await db.Database.SqlQueryRaw<RegistroComunicacion>("SELECT * FROM \"NotificacionesAutomaticas\" ORDER BY \"CreadoUtc\" DESC LIMIT 100").ToListAsync(ct);
    }
    public async Task<bool> ResultadoAsync(Guid id, bool enviado, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var estado = enviado ? "Enviado" : "Error";
        var n = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "NotificacionesAutomaticas" SET "Estado"={estado}, "FinalizadoUtc"=CURRENT_TIMESTAMP
            WHERE "Id"={id} AND "Estado"='EnProceso'
            """, ct);
        return n == 1;
    }
    public async Task<MensajeComunicacion?> TomarAsync(string tipo, CancellationToken ct)
    {
        if (!HabilitadaGlobal || !Tipos.Contains(tipo)) return null;
        await using var db = await factory.CreateDbContextAsync(ct);
        var regla = await db.Database.SqlQuery<ConfigComunicacion>($"SELECT * FROM \"ConfiguracionComunicaciones\" WHERE \"Tipo\"={tipo}").SingleOrDefaultAsync(ct);
        if (regla is null || !regla.Habilitada) return null;
        var ahora = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3));
        var hoy = DateOnly.FromDateTime(ahora.DateTime);
        var desde = regla.DesdeUtc;
        var periodo = new DateOnly(hoy.Year, hoy.Month, 1);
        var candidatos = new List<Candidato>();
        var activos = db.Bebes.AsNoTracking().Where(b => b.Estado == EstadoBebe.Activo);
        if (tipo == "ausencias")
        {
            var filas = await db.Asistencias.AsNoTracking().Where(a => a.Estado == EstadoAsistencia.Ausente && a.RegistradaUtc >= desde && a.SesionClase.Estado != EstadoSesionClase.Cancelada && a.SesionClase.Fecha <= hoy && a.Bebe.Estado == EstadoBebe.Activo)
                .Select(a => new { a.Id, a.BebeId, a.SesionClase.Fecha, a.SesionClase.HoraInicio, a.SesionClase.DuracionMinutos }).ToListAsync(ct);
            foreach (var a in filas.Where(a => a.Fecha.ToDateTime(a.HoraInicio).AddMinutes(a.DuracionMinutos) <= ahora.DateTime))
                candidatos.Add(new(a.BebeId, a.Id.ToString("N"), "Ausencia sin aviso", $"Registramos una ausencia sin aviso en la clase del {a.Fecha:dd/MM/yyyy}. Si necesitás informarnos algo, podés responder a este correo."));
        }
        else if (tipo == "pagos")
        {
            var filas = await db.Pagos.AsNoTracking().Where(p => p.CreadoUtc >= desde && p.AnuladoUtc == null && p.Importe > 0).OrderBy(p => p.CreadoUtc)
                .Select(p => new { p.Id, p.BebeId, p.Importe, p.FechaUtc, p.MedioPago }).ToListAsync(ct);
            foreach (var p in filas)
                candidatos.Add(new(p.BebeId, p.Id.ToString("N"), "Comprobante de pago", $"Registramos tu pago de {p.Importe.ToString("C2", Cultura)} del {p.FechaUtc.ToOffset(TimeSpan.FromHours(-3)):dd/MM/yyyy} ({p.MedioPago}). Podés descargar el comprobante desde el enlace.", p.Id));
        }
        else if (tipo == "morosos" && hoy.Day >= 16 && ahora.Hour >= 9)
        {
            var ids = await activos.Where(b => b.Inscripciones.Any(i => i.Estado == EstadoInscripcion.Activa && i.FechaInicio <= hoy && (i.FechaFin == null || i.FechaFin >= hoy) && i.Turnos.Any())).Select(b => b.Id).ToListAsync(ct);
            var indicadores = await finanzas.ObtenerAsync(ids, ct);
            foreach (var (id, f) in indicadores)
                if ((f.Tipo == IndicadorFinancieroTipo.DebeUna || f.Tipo == IndicadorFinancieroTipo.DebeVarias) && f.SaldoVencido > 0)
                    candidatos.Add(new(id, $"{id:N}/{periodo:yyyy-MM}", "Recordatorio de cuota pendiente", $"Al día de hoy figuran {f.CuotasVencidas} cuota(s) vencida(s), con saldo pendiente de {f.SaldoVencido.ToString("C2", Cultura)}. Si ya abonaste, contactanos para verificarlo."));
        }
        else if (tipo == "cumpleanos" && ahora.Hour >= 9)
        {
            var ids = await activos.Where(b => b.FechaNacimiento.Month == hoy.Month && b.FechaNacimiento.Day == hoy.Day).Select(b => b.Id).ToListAsync(ct);
            foreach (var id in ids) candidatos.Add(new(id, $"{id:N}/{hoy.Year}", "¡Feliz cumpleaños!", "Todos tus amigos de Bebés Nadadores te deseamos un súper cumple. ¡Tenemos un saludo especial para vos!", Cumple: true));
        }
        else if (tipo == "recuperos" && ahora.Hour >= 10)
        {
            var lunes = hoy.AddDays(-((int)hoy.DayOfWeek + 6) % 7);
            // Un recordatorio semanal por alumno; se consulta el saldo real al reclamarlo.
            var filas = await db.Recuperaciones.AsNoTracking().Where(r => r.Estado == EstadoRecuperacion.Disponible && (r.FechaVencimiento == null || r.FechaVencimiento >= hoy) && r.Bebe.Estado == EstadoBebe.Activo && r.Inscripcion.Estado == EstadoInscripcion.Activa)
                .Select(r => new { r.BebeId, r.FechaVencimiento }).ToListAsync(ct);
            foreach (var g in filas.GroupBy(r => r.BebeId))
            {
                var vence = g.Where(r => r.FechaVencimiento.HasValue).Select(r => r.FechaVencimiento).Min();
                candidatos.Add(new(g.Key, $"{g.Key:N}/{lunes:yyyy-MM-dd}", "Recuperos disponibles", $"Tenés {g.Count()} recupero(s) disponible(s)." + (vence.HasValue ? $" El próximo vencimiento es el {vence:dd/MM/yyyy}." : "") + " Contactanos para coordinar un horario."));
            }
        }
        if (candidatos.Count == 0) return null;
        var idsBebes = candidatos.Select(c => c.BebeId).Distinct().ToArray();
        var contactos = await db.BebesResponsables.AsNoTracking().Where(r => idsBebes.Contains(r.BebeId) && r.AutorizaComunicaciones && r.Responsable.Activo && r.Responsable.Email != "")
            .OrderByDescending(r => r.EsContactoPrincipal).ThenBy(r => r.ResponsableId)
            .Select(r => new { r.BebeId, r.Bebe.Nombre, r.Responsable.Email }).ToListAsync(ct);
        var porBebe = contactos.Where(r => MailAddress.TryCreate(r.Email.Trim(), out var addr) && addr.Address == r.Email.Trim()).GroupBy(r => r.BebeId).ToDictionary(g => g.Key, g => g.First());
        var claves = await db.Database.SqlQuery<string>($"SELECT \"Clave\" AS \"Value\" FROM \"NotificacionesAutomaticas\" WHERE \"Tipo\"={tipo}").ToListAsync(ct);
        var existentes = claves.ToHashSet(StringComparer.Ordinal);
        foreach (var c in candidatos)
        {
            if (existentes.Contains(c.Clave) || !porBebe.TryGetValue(c.BebeId, out var contacto)) continue;
            const string baseUri = "https://bebesnadadores.com/";
            var enlace = c.PagoId.HasValue ? comprobantes.CrearEnlace(c.PagoId.Value, baseUri) : c.Cumple ? cumpleanos.CrearEnlace(c.BebeId, baseUri) : null;
            var id = Guid.NewGuid();
            var email = contacto.Email.Trim();
            // Reserva indivisible: dos ejecuciones concurrentes no reciben el mismo mensaje.
            var n = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "NotificacionesAutomaticas" ("Id","Tipo","Clave","BebeId","Destinatario","Estado")
                SELECT {id},{tipo},{c.Clave},{c.BebeId},{email},'EnProceso'
                WHERE EXISTS (SELECT 1 FROM "ConfiguracionComunicaciones" WHERE "Tipo"={tipo} AND "Habilitada" AND "DesdeUtc"={desde})
                ON CONFLICT ("Tipo","Clave") DO NOTHING
                """, ct);
            if (n == 1) return new(id, email, c.Asunto, contacto.Nombre, c.Mensaje, enlace, c.PagoId.HasValue ? "Descargar comprobante" : c.Cumple ? "Ver mi saludo" : null);
        }
        return null;
    }
}
