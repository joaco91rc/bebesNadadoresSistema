using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public class AptoMedicoConfig : IEntityTypeConfiguration<AptoMedico>
{
    public void Configure(EntityTypeBuilder<AptoMedico> builder)
    {
        builder.ToTable("AptosMedicos", t =>
        {
            t.HasCheckConstraint(
                "CK_AptosMedicos_Vigencia",
                "\"VigenciaHasta\" >= \"FechaRevision\"");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Observaciones).HasMaxLength(2000);
        builder.Property(x => x.DocumentoStorageKey).HasMaxLength(500);
        builder.Property(x => x.DocumentoNombreOriginal).HasMaxLength(255);

        builder.HasIndex(x => new { x.BebeId, x.VigenciaHasta });
        builder.HasIndex(x => new { x.Resultado, x.VigenciaHasta });

        builder.HasOne(x => x.Bebe)
            .WithMany(x => x.AptosMedicos)
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.MedicoUsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
