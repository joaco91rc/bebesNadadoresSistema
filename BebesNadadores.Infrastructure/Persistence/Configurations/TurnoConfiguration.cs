using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class TurnoConfiguration
    : IEntityTypeConfiguration<Turno>
{
    public void Configure(
        EntityTypeBuilder<Turno> builder)
    {
        builder.ToTable("Turnos");


        builder.HasKey(x => x.Id);


        builder.Property(x => x.Nombre)
            .HasMaxLength(150)
            .IsRequired();


       builder.Property(x => x.Sector)
    .HasMaxLength(150);


        builder.Property(x => x.DiaSemana)
            .IsRequired();


        builder.Property(x => x.HoraInicio)
            .IsRequired();


        builder.Property(x => x.DuracionMinutos)
            .IsRequired();


        builder.Property(x => x.Cupo)
            .IsRequired();


        builder.Property(x => x.VigenteDesde)
            .IsRequired();


        builder.Property(x => x.Estado)
            .IsRequired();


        builder.Ignore(x => x.HoraFin);


        builder.HasOne(x => x.Nivel)
            .WithMany(x => x.Turnos)
            .HasForeignKey(x => x.NivelId)
            .OnDelete(DeleteBehavior.Restrict);


        builder.HasIndex(x => new
        {
            x.DiaSemana,
            x.HoraInicio
        });


        builder.HasIndex(x => new
        {
            x.Estado,
            x.VigenteDesde
        });
    }
}