using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class TurnoProfesorConfiguration
    : IEntityTypeConfiguration<TurnoProfesor>
{
    public void Configure(
        EntityTypeBuilder<TurnoProfesor> builder)
    {
        builder.ToTable("TurnosProfesores");


        builder.HasKey(x => new
        {
            x.TurnoId,
            x.ProfesorId
        });


        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Profesores)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.HasOne(x => x.Profesor)
            .WithMany(x => x.Turnos)
            .HasForeignKey(x => x.ProfesorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}