using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class SesionClaseConfiguration
    : IEntityTypeConfiguration<SesionClase>
{
    public void Configure(
        EntityTypeBuilder<SesionClase> builder)
    {
        builder.ToTable("SesionesClase");


        builder.HasKey(x => x.Id);


        builder.Property(x => x.Fecha)
            .IsRequired();


        builder.Property(x => x.HoraInicio)
            .IsRequired();


        builder.Property(x => x.DuracionMinutos)
            .IsRequired();


        builder.Property(x => x.Cupo)
            .IsRequired();


        builder.Property(x => x.Sector)
            .HasMaxLength(150);


        builder.Property(x => x.MotivoCancelacion)
            .HasMaxLength(500);


        builder.Property(x => x.Estado)
            .IsRequired();


        builder.Ignore(x => x.HoraFin);


        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Sesiones)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasOne(x => x.Nivel)
            .WithMany()
            .HasForeignKey(x => x.NivelId)
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasOne(x => x.DiaNoLaborable)
            .WithMany(x => x.SesionesAfectadas)
            .HasForeignKey(x => x.DiaNoLaborableId)
            .OnDelete(DeleteBehavior.SetNull);


        /*
         * Un turno solamente puede generar
         * una sesión por día.
         */
        builder.HasIndex(x => new
        {
            x.TurnoId,
            x.Fecha
        })
        .IsUnique();


        builder.HasIndex(x => new
        {
            x.Fecha,
            x.HoraInicio
        });
    }
}