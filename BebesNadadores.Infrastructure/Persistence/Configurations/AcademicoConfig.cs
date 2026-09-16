using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public class NivelConfig : IEntityTypeConfiguration<Nivel>
{
    public void Configure(EntityTypeBuilder<Nivel> builder)
    {
        builder.ToTable("Niveles", t =>
        {
            t.HasCheckConstraint(
                "CK_Niveles_Edades",
                "\"EdadMinMeses\" >= 0 AND \"EdadMaxMeses\" >= \"EdadMinMeses\" AND \"EdadMaxMeses\" <= 84");

            t.HasCheckConstraint(
                "CK_Niveles_Duracion",
                "\"DuracionMinutos\" > 0");

            t.HasCheckConstraint(
                "CK_Niveles_Cupo",
                "\"CupoPredeterminado\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Descripcion).HasMaxLength(500);

        builder.HasIndex(x => x.Nombre).IsUnique();
        builder.HasIndex(x => x.Orden);
    }
}

public class ProfesorConfig : IEntityTypeConfiguration<Profesor>
{
    public void Configure(EntityTypeBuilder<Profesor> builder)
    {
        builder.ToTable("Profesores");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Apellido).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Telefono).HasMaxLength(50);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        builder.HasIndex(x => new { x.Activo, x.Apellido, x.Nombre });
        builder.HasIndex(x => x.UsuarioId).IsUnique();

        builder.HasOne<AppUser>()
            .WithOne()
            .HasForeignKey<Profesor>(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class PlanConfig : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("Planes", t =>
        {
            t.HasCheckConstraint(
                "CK_Planes_VecesPorSemana",
                "\"VecesPorSemana\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Descripcion).HasMaxLength(500);

        builder.HasIndex(x => x.Nombre).IsUnique();
    }
}

public class TarifaPlanConfig : IEntityTypeConfiguration<TarifaPlan>
{
    public void Configure(EntityTypeBuilder<TarifaPlan> builder)
    {
        builder.ToTable("TarifasPlan", t =>
        {
            t.HasCheckConstraint(
                "CK_TarifasPlan_Precio",
                "\"PrecioMensual\" >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PrecioMensual).HasPrecision(18, 2);

        builder.HasIndex(x => new { x.PlanId, x.VigenciaDesde }).IsUnique();

        builder.HasOne(x => x.Plan)
            .WithMany(x => x.Tarifas)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TurnoConfig : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("Turnos", t =>
        {
            t.HasCheckConstraint("CK_Turnos_Duracion", "\"DuracionMinutos\" > 0");
            t.HasCheckConstraint("CK_Turnos_Cupo", "\"Cupo\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Sector).HasMaxLength(150);

        builder.Ignore(x => x.HoraFin);

        builder.HasIndex(x => new { x.DiaSemana, x.HoraInicio, x.NivelId, x.Estado });

        builder.HasOne(x => x.Nivel)
            .WithMany(x => x.Turnos)
            .HasForeignKey(x => x.NivelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TurnoProfesorConfig : IEntityTypeConfiguration<TurnoProfesor>
{
    public void Configure(EntityTypeBuilder<TurnoProfesor> builder)
    {
        builder.ToTable("TurnosProfesores");

        builder.HasKey(x => new { x.TurnoId, x.ProfesorId });

        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Profesores)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Profesor)
            .WithMany(x => x.Turnos)
            .HasForeignKey(x => x.ProfesorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InscripcionConfig : IEntityTypeConfiguration<Inscripcion>
{
    public void Configure(EntityTypeBuilder<Inscripcion> builder)
    {
        builder.ToTable("Inscripciones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        builder.HasIndex(x => new { x.BebeId, x.Estado });
        builder.HasIndex(x => new { x.PlanId, x.Estado });

        builder.HasOne(x => x.Bebe)
            .WithMany(x => x.Inscripciones)
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Plan)
            .WithMany(x => x.Inscripciones)
            .HasForeignKey(x => x.PlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InscripcionTurnoConfig : IEntityTypeConfiguration<InscripcionTurno>
{
    public void Configure(EntityTypeBuilder<InscripcionTurno> builder)
    {
        builder.ToTable("InscripcionesTurnos");

        builder.HasKey(x => new { x.InscripcionId, x.TurnoId });

        builder.HasOne(x => x.Inscripcion)
            .WithMany(x => x.Turnos)
            .HasForeignKey(x => x.InscripcionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Inscripciones)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SesionClaseConfig : IEntityTypeConfiguration<SesionClase>
{
    public void Configure(EntityTypeBuilder<SesionClase> builder)
    {
        builder.ToTable("SesionesClase", t =>
        {
            t.HasCheckConstraint("CK_SesionesClase_Duracion", "\"DuracionMinutos\" > 0");
            t.HasCheckConstraint("CK_SesionesClase_Cupo", "\"Cupo\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Sector).HasMaxLength(150);
        builder.Property(x => x.MotivoCancelacion).HasMaxLength(500);

        builder.HasIndex(x => new { x.TurnoId, x.Fecha }).IsUnique();
        builder.HasIndex(x => new { x.Fecha, x.NivelId, x.Estado });

        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Sesiones)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Nivel)
            .WithMany()
            .HasForeignKey(x => x.NivelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
