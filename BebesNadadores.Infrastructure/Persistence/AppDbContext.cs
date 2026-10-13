using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BebesNadadores.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<Bebe> Bebes => Set<Bebe>();
    public DbSet<Responsable> Responsables => Set<Responsable>();
    public DbSet<BebeResponsable> BebesResponsables => Set<BebeResponsable>();

    public DbSet<Nivel> Niveles => Set<Nivel>();
    public DbSet<Profesor> Profesores => Set<Profesor>();
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<TarifaPlan> TarifasPlan => Set<TarifaPlan>();

    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<TurnoProfesor> TurnosProfesores => Set<TurnoProfesor>();
    public DbSet<Inscripcion> Inscripciones => Set<Inscripcion>();
    public DbSet<InscripcionTurno> InscripcionesTurnos => Set<InscripcionTurno>();
    public DbSet<SesionClase> SesionesClase => Set<SesionClase>();
    public DbSet<DiaNoLaborable> DiasNoLaborables => Set<DiaNoLaborable>();
    public DbSet<Asistencia> Asistencias => Set<Asistencia>();
    public DbSet<Recuperacion> Recuperaciones => Set<Recuperacion>();
    public DbSet<PreferenciaRecuperacion> PreferenciasRecuperacion =>
        Set<PreferenciaRecuperacion>();
    public DbSet<ListaEspera> ListaEspera => Set<ListaEspera>();
    public DbSet<InvitacionVacante> InvitacionesVacante => Set<InvitacionVacante>();

    public DbSet<AptoMedico> AptosMedicos => Set<AptoMedico>();

    public DbSet<Cuota> Cuotas => Set<Cuota>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<ImputacionPago> ImputacionesPago => Set<ImputacionPago>();
    public DbSet<Caja> Cajas => Set<Caja>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>();
    public DbSet<ConceptoCaja> ConceptosCaja => Set<ConceptoCaja>();

    public DbSet<CicloLectivo> CiclosLectivos => Set<CicloLectivo>();

    public DbSet<SesionClaseProfesor> SesionesClaseProfesores => Set<SesionClaseProfesor>();

    public DbSet<ReservaSesionClase> ReservasSesionesClase => Set<ReservaSesionClase>();

    public DbSet<Banco> Bancos => Set<Banco>();

    public DbSet<ConfiguracionSistema> ConfiguracionesSistema => Set<ConfiguracionSistema>();
    public DbSet<CuentaBancaria> CuentasBancarias => Set<CuentaBancaria>();

    public DbSet<MovimientoBancario> MovimientosBancarios => Set<MovimientoBancario>();

    public DbSet<Proveedor> Proveedores => Set<Proveedor>();

    public DbSet<PagoProveedor> PagosProveedores => Set<PagoProveedor>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        builder.Entity<AppUser>(entity =>
        {
            entity.Property(x => x.NombreCompleto).HasMaxLength(200).IsRequired();

            entity.HasIndex(x => x.Email);
        });
    }
}
