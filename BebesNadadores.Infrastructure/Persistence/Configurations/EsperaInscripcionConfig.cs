using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class EsperaInscripcionConfig : IEntityTypeConfiguration<EsperaInscripcion>
{
    public void Configure(EntityTypeBuilder<EsperaInscripcion> b)
    {
        b.ToTable("EsperaInscripciones");
        b.HasKey(x => x.Id);
        b.Property(x => x.NombreBebe).HasMaxLength(200).IsRequired();
        b.Property(x => x.NombreResponsable).HasMaxLength(200).IsRequired();
        b.Property(x => x.Telefono).HasMaxLength(60).IsRequired();
        b.Property(x => x.Email).HasMaxLength(200);
        b.Property(x => x.Observaciones).HasMaxLength(1000);
        b.HasIndex(x => new { x.Estado, x.NivelId, x.DiaPreferido });
        b.HasIndex(x => x.Telefono);
        b.HasOne(x => x.Nivel).WithMany().HasForeignKey(x => x.NivelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.BebeInscripto).WithMany().HasForeignKey(x => x.BebeInscriptoId).OnDelete(DeleteBehavior.Restrict);
    }
}
