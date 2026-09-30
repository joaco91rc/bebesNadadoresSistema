using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public class AsistenciaConfig
    : IEntityTypeConfiguration<Asistencia>
{
    public void Configure(
        EntityTypeBuilder<Asistencia> builder)
    {
        builder.ToTable("Asistencias");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Estado)
            .IsRequired();

        builder.Property(x => x.EsRecupero)
            .IsRequired();

        builder.Property(x => x.Observaciones)
            .HasMaxLength(1000);

        builder.HasIndex(x => new
        {
            x.SesionClaseId,
            x.BebeId
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.BebeId,
            x.Estado
        });

        builder.HasOne(x => x.SesionClase)
            .WithMany(x => x.Asistencias)
            .HasForeignKey(x => x.SesionClaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Bebe)
            .WithMany(x => x.Asistencias)
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Inscripcion)
            .WithMany()
            .HasForeignKey(x => x.InscripcionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RecuperacionConfig
    : IEntityTypeConfiguration<Recuperacion>
{
    public void Configure(
        EntityTypeBuilder<Recuperacion> builder)
    {
        builder.ToTable("Recuperaciones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo)
            .IsRequired();

        builder.Property(x => x.FechaGeneracion)
            .IsRequired();

        builder.Property(x => x.Estado)
            .IsRequired();


        // -----------------------------------------------------
        // Un bebé solamente puede obtener un recupero
        // por la misma sesión origen.
        // -----------------------------------------------------

        builder.HasIndex(x => new
        {
            x.BebeId,
            x.SesionOrigenId
        })
        .IsUnique();


        // -----------------------------------------------------
        // Una asistencia con aviso solamente puede generar
        // un recupero.
        //
        // AsistenciaOrigenId ahora puede ser NULL porque
        // los recuperos por feriado/cierre no tienen
        // una asistencia de origen.
        // PostgreSQL permite múltiples NULL en este índice.
        // -----------------------------------------------------

        builder.HasIndex(x => x.AsistenciaOrigenId)
            .IsUnique();


        builder.HasIndex(x => new
        {
            x.BebeId,
            x.Estado,
            x.FechaVencimiento
        });


        builder.HasOne(x => x.Bebe)
            .WithMany(x => x.Recuperaciones)
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasOne(x => x.Inscripcion)
            .WithMany()
            .HasForeignKey(x => x.InscripcionId)
            .OnDelete(DeleteBehavior.Restrict);


        // Clase que originó el recupero.
        builder.HasOne(x => x.SesionOrigen)
            .WithMany()
            .HasForeignKey(x => x.SesionOrigenId)
            .OnDelete(DeleteBehavior.Restrict);


        // Nullable:
        // solo existe para AusenciaConAviso.
        builder.HasOne(x => x.AsistenciaOrigen)
            .WithOne()
            .HasForeignKey<Recuperacion>(
                x => x.AsistenciaOrigenId)
            .OnDelete(DeleteBehavior.SetNull);


        // Clase en la cual finalmente utiliza el recupero.
        builder.HasOne(x => x.SesionUso)
            .WithMany()
            .HasForeignKey(x => x.SesionUsoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PreferenciaRecuperacionConfig : IEntityTypeConfiguration<PreferenciaRecuperacion>
{
    public void Configure(EntityTypeBuilder<PreferenciaRecuperacion> builder)
    {
        builder.ToTable("PreferenciasRecuperacion");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.BebeId,
            x.DiaSemana,
            x.FranjaHoraria,
            x.Activa
        });

        builder.HasOne(x => x.Bebe)
            .WithMany(x => x.PreferenciasRecuperacion)
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ListaEsperaConfig : IEntityTypeConfiguration<ListaEspera>
{
    public void Configure(EntityTypeBuilder<ListaEspera> builder)
    {
        builder.ToTable("ListaEspera");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        builder.HasIndex(x => new { x.Estado, x.NivelId, x.DiaPreferido, x.FranjaHoraria });
        builder.HasIndex(x => new { x.BebeId, x.Estado });

        builder.HasOne(x => x.Bebe)
            .WithMany()
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Nivel)
            .WithMany()
            .HasForeignKey(x => x.NivelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Turno)
            .WithMany()
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Recuperacion)
            .WithMany()
            .HasForeignKey(x => x.RecuperacionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InvitacionVacanteConfig : IEntityTypeConfiguration<InvitacionVacante>
{
    public void Configure(EntityTypeBuilder<InvitacionVacante> builder)
    {
        builder.ToTable("InvitacionesVacante");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Token).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => new { x.SesionClaseId, x.Estado });
        builder.HasIndex(x => new { x.BebeId, x.Estado });

        builder.HasOne(x => x.SesionClase)
            .WithMany(x => x.InvitacionesVacante)
            .HasForeignKey(x => x.SesionClaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Bebe)
            .WithMany()
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Responsable)
            .WithMany()
            .HasForeignKey(x => x.ResponsableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Recuperacion)
            .WithMany()
            .HasForeignKey(x => x.RecuperacionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
