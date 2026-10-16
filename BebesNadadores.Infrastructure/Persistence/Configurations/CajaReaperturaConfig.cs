using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class CajaReaperturaConfig
    : IEntityTypeConfiguration<CajaReapertura>
{
    public void Configure(
        EntityTypeBuilder<CajaReapertura> builder)
    {
        builder.ToTable("CajasReaperturas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Motivo)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.SaldoFinalDeclaradoAnterior)
            .HasPrecision(18, 2);

        builder.HasIndex(x => new { x.CajaId, x.FechaUtc });

        builder.HasOne(x => x.Caja)
            .WithMany(x => x.Reaperturas)
            .HasForeignKey(x => x.CajaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Quién reabrió
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Quién había cerrado
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioCierreAnteriorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
