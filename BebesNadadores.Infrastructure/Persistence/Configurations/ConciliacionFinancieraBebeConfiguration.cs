using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class ConciliacionFinancieraBebeConfiguration : IEntityTypeConfiguration<ConciliacionFinancieraBebe>
{
    public void Configure(EntityTypeBuilder<ConciliacionFinancieraBebe> b)
    {
        b.ToTable("ConciliacionesFinancierasBebes");
        b.HasKey(x => x.BebeId);
        b.Property(x => x.FechaCorte).IsRequired();
        b.Property(x => x.MesesVencidosSinConciliar).IsRequired();
        b.Property(x => x.CodigoMigracion).HasMaxLength(64).IsRequired();
        b.Property(x => x.ImportadaUtc).IsRequired();
        b.HasIndex(x => x.CodigoMigracion);
        b.ToTable(t => t.HasCheckConstraint(
            "CK_ConciliacionesFinancierasBebes_MesesNoNegativos",
            "\"MesesVencidosSinConciliar\" >= 0"));
    }
}
