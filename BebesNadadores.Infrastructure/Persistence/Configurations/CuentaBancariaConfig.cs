using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class CuentaBancariaConfig
    : IEntityTypeConfiguration<CuentaBancaria>
{
    public void Configure(
        EntityTypeBuilder<CuentaBancaria> b)
    {
        b.ToTable("CuentasBancarias");

        b.HasKey(x =>
            x.Id);


        b.Property(x =>
                x.Nombre)
            .HasMaxLength(150)
            .IsRequired();


        b.Property(x =>
                x.NumeroCuenta)
            .HasMaxLength(100);


        b.Property(x =>
                x.CbuCvu)
            .HasMaxLength(50);


        b.Property(x =>
                x.Alias)
            .HasMaxLength(100);


        b.Property(x =>
                x.Titular)
            .HasMaxLength(200)
            .IsRequired();


        b.Property(x =>
                x.Moneda)
            .HasMaxLength(10)
            .IsRequired();


        b.Property(x =>
                x.SaldoInicial)
            .HasPrecision(
                18,
                2);


        b.Property(x =>
                x.Tipo)
            .IsRequired();


        b.Property(x =>
                x.Activa)
            .IsRequired();


        b.HasOne(x =>
                x.Banco)
            .WithMany(x =>
                x.Cuentas)
            .HasForeignKey(x =>
                x.BancoId)
            .OnDelete(
                DeleteBehavior.Restrict);


        b.HasIndex(x =>
            new
            {
                x.BancoId,
                x.Nombre
            });


        b.HasIndex(x =>
                x.CbuCvu)
            .IsUnique();


        b.HasIndex(x =>
                x.Alias)
            .IsUnique();
    }
}