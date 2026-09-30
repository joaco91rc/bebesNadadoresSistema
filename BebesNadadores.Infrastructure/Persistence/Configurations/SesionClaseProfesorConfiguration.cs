using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BebesNadadores.Infrastructure.Identity;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class SesionClaseProfesorConfig
    : IEntityTypeConfiguration<SesionClaseProfesor>
{
    public void Configure(
        EntityTypeBuilder<SesionClaseProfesor> builder)
    {
        builder.ToTable(
            "SesionesClaseProfesores");


        builder.HasKey(x =>
            new
            {
                x.SesionClaseId,
                x.ProfesorId
            });


        builder.HasOne(x =>
                x.SesionClase)
            .WithMany(x =>
                x.Profesores)
            .HasForeignKey(x =>
                x.SesionClaseId)
            .OnDelete(
                DeleteBehavior.Cascade);


        builder.HasOne(x =>
                x.Profesor)
            .WithMany()
            .HasForeignKey(x =>
                x.ProfesorId)
            .OnDelete(
                DeleteBehavior.Restrict);


        builder.HasIndex(x =>
            x.ProfesorId);

            builder.Property(x =>
        x.EsReemplazo)
    .HasDefaultValue(false);


builder.Property(x =>
        x.ControlConfirmado)
    .HasDefaultValue(false);


builder.Property(x =>
        x.DictoClase)
    .HasDefaultValue(false);


builder.HasOne<AppUser>()
    .WithMany()
    .HasForeignKey(x =>
        x.ConfirmadoPorUsuarioId)
    .OnDelete(DeleteBehavior.Restrict);

    builder.HasOne<Profesor>()
    .WithMany()
    .HasForeignKey(x =>
        x.ReemplazaAProfesorId)
    .OnDelete(DeleteBehavior.Restrict);
    }
}