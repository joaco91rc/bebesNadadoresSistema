using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class MovimientoBancarioConfig
    : IEntityTypeConfiguration<MovimientoBancario>
{
    public void Configure(
        EntityTypeBuilder<MovimientoBancario> b)
    {
        b.ToTable(
            "MovimientosBancarios");


        b.HasKey(x =>
            x.Id);


        b.Property(x =>
                x.Importe)
            .HasPrecision(
                18,
                2)
            .IsRequired();


        b.Property(x =>
                x.Concepto)
            .HasMaxLength(300)
            .IsRequired();


        b.Property(x =>
                x.ReferenciaExterna)
            .HasMaxLength(200);


        b.Property(x =>
                x.Observaciones)
            .HasMaxLength(500);


        b.Property(x =>
                x.Tipo)
            .IsRequired();


        b.Property(x =>
                x.Origen)
            .IsRequired();


        b.HasOne(x =>
                x.CuentaBancaria)
            .WithMany(x =>
                x.Movimientos)
            .HasForeignKey(x =>
                x.CuentaBancariaId)
            .OnDelete(
                DeleteBehavior.Restrict);

                b.HasOne(x =>
        x.PagoProveedor)
    .WithMany()
    .HasForeignKey(x =>
        x.PagoProveedorId)
    .OnDelete(
        DeleteBehavior.Restrict);


b.HasIndex(x =>
    x.PagoProveedorId);


        b.HasOne(x =>
                x.Pago)
            .WithMany()
            .HasForeignKey(x =>
                x.PagoId)
            .OnDelete(
                DeleteBehavior.Restrict);


        b.HasIndex(x =>
            new
            {
                x.CuentaBancariaId,
                x.FechaUtc
            });


        b.HasIndex(x =>
            x.PagoId);
    }
}