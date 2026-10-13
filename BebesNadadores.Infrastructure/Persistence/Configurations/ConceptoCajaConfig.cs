using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class ConceptoCajaConfig
    : IEntityTypeConfiguration<ConceptoCaja>
{
    public void Configure(
        EntityTypeBuilder<ConceptoCaja> builder)
    {
        builder.ToTable(
            "ConceptosCaja");


        builder.HasKey(x =>
            x.Id);


        builder.Property(x =>
                x.Nombre)
            .HasMaxLength(150)
            .IsRequired();


        builder.Property(x =>
                x.Tipo)
            .IsRequired();


        builder.Property(x =>
                x.Activo)
            .IsRequired();


        builder.Property(x =>
                x.Orden)
            .IsRequired();


        builder.HasIndex(x =>
            new
            {
                x.Tipo,
                x.Nombre
            })
            .IsUnique();


        builder.HasIndex(x =>
            new
            {
                x.Tipo,
                x.Activo,
                x.Orden
            });
    }
}