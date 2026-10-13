using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class ConfiguracionSistemaConfig
    : IEntityTypeConfiguration<ConfiguracionSistema>
{
    public void Configure(
        EntityTypeBuilder<ConfiguracionSistema> builder)
    {
        builder.ToTable(
            "ConfiguracionSistema",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_ConfiguracionSistema_DiasVigenciaRecupero",
                    "\"DiasVigenciaRecupero\" > 0");

                t.HasCheckConstraint(
                    "CK_ConfiguracionSistema_MesesVigenciaApto",
                    "\"MesesVigenciaApto\" > 0");

                t.HasCheckConstraint(
                    "CK_ConfiguracionSistema_DiasAlertaApto",
                    "\"DiasAlertaApto\" >= 0");

                t.HasCheckConstraint(
                    "CK_ConfiguracionSistema_DiaVencimientoCuota",
                    "\"DiaVencimientoCuota\" BETWEEN 1 AND 28");

                t.HasCheckConstraint(
                    "CK_ConfiguracionSistema_EdadMaximaMeses",
                    "\"EdadMaximaMeses\" BETWEEN 1 AND 84");
            });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.NombreInstitucion)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(x => x.WhatsApp)
            .HasMaxLength(40);

        builder.Property(x => x.Direccion)
            .HasMaxLength(300);

        builder.Property(x => x.LogoUrl)
            .HasMaxLength(500);
    }
}
