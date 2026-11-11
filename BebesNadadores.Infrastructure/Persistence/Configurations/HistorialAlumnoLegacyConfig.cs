using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace BebesNadadores.Infrastructure.Persistence.Configurations;
public sealed class HistorialAlumnoLegacyConfig : IEntityTypeConfiguration<HistorialAlumnoLegacy>
{
    public void Configure(EntityTypeBuilder<HistorialAlumnoLegacy> b)
    {
        b.ToTable("HistorialAlumnosLegacy"); b.HasKey(x=>x.Id);
        b.HasIndex(x=>x.LegacyPersonaId).IsUnique();
        b.HasIndex(x=>x.BebeId); b.HasIndex(x=>new {x.Apellido,x.Nombre});
        b.Property(x=>x.NombreOriginal).HasMaxLength(250).IsRequired();
        b.Property(x=>x.Nombre).HasMaxLength(120).IsRequired();
        b.Property(x=>x.Apellido).HasMaxLength(120).IsRequired();
        b.HasOne(x=>x.Bebe).WithMany().HasForeignKey(x=>x.BebeId).OnDelete(DeleteBehavior.SetNull);
    }
}
public sealed class HistorialAlumnoAnioLegacyConfig : IEntityTypeConfiguration<HistorialAlumnoAnioLegacy>
{
    public void Configure(EntityTypeBuilder<HistorialAlumnoAnioLegacy> b)
    {
        b.ToTable("HistorialAlumnosAniosLegacy"); b.HasKey(x=>x.Id);
        b.HasIndex(x=>new {x.HistorialAlumnoLegacyId,x.Anio}).IsUnique();
        b.Property(x=>x.Niveles).HasMaxLength(500);
        b.HasOne(x=>x.HistorialAlumno).WithMany(x=>x.Anios)
            .HasForeignKey(x=>x.HistorialAlumnoLegacyId).OnDelete(DeleteBehavior.Cascade);
    }
}
