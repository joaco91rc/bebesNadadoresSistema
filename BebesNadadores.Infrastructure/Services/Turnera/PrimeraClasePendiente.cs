using BebesNadadores.Domain.Common;
using BebesNadadores.Domain.Entities;
using BebesNadadores.Domain.Enums;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Services.Turnera;

public static class PrimeraClasePendiente
{
    public static IQueryable<ReservaSesionClase> Consultar(AppDbContext db)
    {
        var hoy = FechaArgentina.Hoy;
        return db.ReservasSesionesClase.AsNoTracking().Where(r =>
            r.Tipo == TipoReservaSesion.InscripcionInicial && r.Estado == EstadoReservaSesion.Activa
            && r.Bebe.Estado == EstadoBebe.Activo && r.Inscripcion.Estado == EstadoInscripcion.Activa
            && (r.Inscripcion.FechaFin == null || r.Inscripcion.FechaFin >= hoy)
            && db.Asistencias.Any(a => a.InscripcionId == r.InscripcionId && a.BebeId == r.BebeId
                && a.SesionClaseId == r.SesionClaseId && a.Estado == EstadoAsistencia.AusenteConAviso)
            && !db.Asistencias.Any(a => a.InscripcionId == r.InscripcionId && a.Estado == EstadoAsistencia.Presente));
    }
}
