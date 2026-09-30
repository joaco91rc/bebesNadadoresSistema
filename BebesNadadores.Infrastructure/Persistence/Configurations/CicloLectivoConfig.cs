using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public class CicloLectivoConfig :
    IEntityTypeConfiguration<CicloLectivo>
{
    public void Configure(
        EntityTypeBuilder<CicloLectivo> builder)
    {
        builder.ToTable("CiclosLectivos");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Observaciones)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.Anio)
            .IsUnique();

        builder.ToTable(
            "CiclosLectivos",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_CiclosLectivos_Fechas",
                    "\"FechaFin\" >= \"FechaInicio\"");
            });
    }
}