using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class CuotaReactivacionConfig
    : IEntityTypeConfiguration<CuotaReactivacion>
{
    public void Configure(
        EntityTypeBuilder<CuotaReactivacion> builder)
    {
        builder.ToTable("CuotasReactivaciones");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Motivo)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.MotivoAnulacionAnterior)
            .HasMaxLength(300);

        builder.HasIndex(x => new { x.CuotaId, x.FechaUtc });

        builder.HasOne(x => x.Cuota)
            .WithMany(x => x.Reactivaciones)
            .HasForeignKey(x => x.CuotaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Quién reactivó
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
