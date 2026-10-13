using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class ProveedorConfig
    : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(
        EntityTypeBuilder<Proveedor> builder)
    {
        builder.ToTable(
            "Proveedores");


        builder.HasKey(x =>
            x.Id);


        builder.Property(x =>
                x.RazonSocial)
            .IsRequired()
            .HasMaxLength(200);


        builder.Property(x =>
                x.NombreFantasia)
            .HasMaxLength(200);


        builder.Property(x =>
                x.Cuit)
            .HasMaxLength(20);


        builder.Property(x =>
                x.Telefono)
            .HasMaxLength(50);


        builder.Property(x =>
                x.Email)
            .HasMaxLength(200);


        builder.Property(x =>
                x.Direccion)
            .HasMaxLength(300);


        builder.Property(x =>
                x.Observaciones)
            .HasMaxLength(1000);


        builder.Property(x =>
                x.Activo)
            .IsRequired();


        builder.HasIndex(x =>
                x.Cuit)
            .IsUnique();


        builder.HasIndex(x =>
            new
            {
                x.Activo,
                x.RazonSocial
            });
    }
}