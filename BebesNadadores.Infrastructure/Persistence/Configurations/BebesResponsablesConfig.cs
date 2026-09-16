using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public class BebeConfig : IEntityTypeConfiguration<Bebe>
{
    public void Configure(EntityTypeBuilder<Bebe> builder)
    {
        builder.ToTable("Bebes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Apellido).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Dni).HasMaxLength(20);
        builder.Property(x => x.MotivoBaja).HasMaxLength(300);

        builder.HasIndex(x => x.Dni).IsUnique();
        builder.HasIndex(x => new { x.Estado, x.Apellido, x.Nombre });

        builder.HasOne(x => x.NivelActual)
            .WithMany()
            .HasForeignKey(x => x.NivelActualId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ResponsableConfig : IEntityTypeConfiguration<Responsable>
{
    public void Configure(EntityTypeBuilder<Responsable> builder)
    {
        builder.ToTable("Responsables");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Apellido).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Dni).HasMaxLength(20);
        builder.Property(x => x.Telefono).HasMaxLength(50).IsRequired();
        builder.Property(x => x.WhatsApp).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Domicilio).HasMaxLength(300);

        builder.HasIndex(x => x.Dni).IsUnique();
        builder.HasIndex(x => x.Email);
    }
}

public class BebeResponsableConfig : IEntityTypeConfiguration<BebeResponsable>
{
    public void Configure(EntityTypeBuilder<BebeResponsable> builder)
    {
        builder.ToTable("BebesResponsables");

        builder.HasKey(x => new { x.BebeId, x.ResponsableId });

        builder.HasOne(x => x.Bebe)
            .WithMany(x => x.Responsables)
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Responsable)
            .WithMany(x => x.Bebes)
            .HasForeignKey(x => x.ResponsableId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
