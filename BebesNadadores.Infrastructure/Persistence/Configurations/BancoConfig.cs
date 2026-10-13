using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class BancoConfig
    : IEntityTypeConfiguration<Banco>
{
    public void Configure(
        EntityTypeBuilder<Banco> b)
    {
        b.ToTable("Bancos");

        b.HasKey(x =>
            x.Id);


        b.Property(x =>
                x.Nombre)
            .HasMaxLength(150)
            .IsRequired();


        b.Property(x =>
                x.Activo)
            .IsRequired();


        b.HasIndex(x =>
                x.Nombre)
            .IsUnique();
    }
}