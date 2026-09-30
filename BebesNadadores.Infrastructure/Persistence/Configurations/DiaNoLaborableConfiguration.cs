using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class DiaNoLaborableConfiguration
    : IEntityTypeConfiguration<DiaNoLaborable>
{
    public void Configure(
        EntityTypeBuilder<DiaNoLaborable> builder)
    {
        builder.ToTable("DiasNoLaborables");


        builder.HasKey(x => x.Id);


        builder.Property(x => x.Fecha)
            .IsRequired();


        builder.Property(x => x.Tipo)
            .IsRequired();


        builder.Property(x => x.Descripcion)
            .HasMaxLength(300)
            .IsRequired();


        builder.HasIndex(x => new
        {
            x.Fecha,
            x.Activo
        });
    }
}