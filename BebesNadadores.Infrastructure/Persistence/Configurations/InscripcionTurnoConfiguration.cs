using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class InscripcionTurnoConfiguration
    : IEntityTypeConfiguration<InscripcionTurno>
{
    public void Configure(
        EntityTypeBuilder<InscripcionTurno> builder)
    {
        builder.ToTable("InscripcionesTurnos");


        builder.HasKey(x => new
        {
            x.InscripcionId,
            x.TurnoId
        });


        builder.HasOne(x => x.Inscripcion)
            .WithMany(x => x.Turnos)
            .HasForeignKey(x => x.InscripcionId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Inscripciones)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}