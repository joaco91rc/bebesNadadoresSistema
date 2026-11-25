using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class BonificacionCuotaConfig : IEntityTypeConfiguration<BonificacionCuota>
{
    public void Configure(EntityTypeBuilder<BonificacionCuota> builder)
    {
        builder.ToTable("BonificacionesCuota", t =>
            t.HasCheckConstraint("CK_BonificacionesCuota_Importe", "\"Importe\" > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Importe).HasPrecision(18, 2);
        builder.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
        builder.Property(x => x.MotivoAnulacion).HasMaxLength(500);
        builder.HasIndex(x => new { x.CuotaId, x.FechaUtc });
        builder.HasIndex(x => x.UsuarioId);
        builder.HasOne(x => x.Cuota)
            .WithMany(x => x.Bonificaciones)
            .HasForeignKey(x => x.CuotaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
