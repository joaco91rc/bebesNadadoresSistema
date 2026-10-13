using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class PagoProveedorConfig
    : IEntityTypeConfiguration<PagoProveedor>
{
    public void Configure(
        EntityTypeBuilder<PagoProveedor> builder)
    {
        builder.ToTable(
            "PagosProveedores");


        builder.HasKey(x =>
            x.Id);


        builder.Property(x =>
                x.FechaUtc)
            .IsRequired();


        builder.Property(x =>
                x.Importe)
            .HasPrecision(
                18,
                2)
            .IsRequired();


        builder.Property(x =>
                x.MedioPago)
            .IsRequired();


        builder.Property(x =>
                x.Concepto)
            .IsRequired()
            .HasMaxLength(300);


        builder.Property(x =>
                x.NumeroComprobante)
            .HasMaxLength(100);


        builder.Property(x =>
                x.ReferenciaExterna)
            .HasMaxLength(200);


        builder.Property(x =>
                x.Observaciones)
            .HasMaxLength(1000);


        builder.ToTable(
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PagosProveedores_Importe",
                    "\"Importe\" > 0");
            });


        // =====================================================
        // PROVEEDOR
        // =====================================================

        builder.HasOne(x =>
                x.Proveedor)
            .WithMany(x =>
                x.Pagos)
            .HasForeignKey(x =>
                x.ProveedorId)
            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // CAJA
        // =====================================================

        builder.HasOne(x =>
                x.Caja)
            .WithMany()
            .HasForeignKey(x =>
                x.CajaId)
            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // CUENTA BANCARIA
        // =====================================================

        builder.HasOne(x =>
                x.CuentaBancaria)
            .WithMany()
            .HasForeignKey(x =>
                x.CuentaBancariaId)
            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // USUARIO
        // =====================================================

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x =>
                x.UsuarioRegistroId)
            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // ÍNDICES
        // =====================================================

        builder.HasIndex(x =>
            new
            {
                x.ProveedorId,
                x.FechaUtc
            });


        builder.HasIndex(x =>
            new
            {
                x.CuentaBancariaId,
                x.FechaUtc
            });


        builder.HasIndex(x =>
            new
            {
                x.CajaId,
                x.FechaUtc
            });
    }
}