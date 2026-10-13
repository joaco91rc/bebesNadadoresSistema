using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class ReservaSesionClaseConfig
    : IEntityTypeConfiguration<ReservaSesionClase>
{
    public void Configure(
        EntityTypeBuilder<ReservaSesionClase> builder)
    {
        builder.ToTable(
            "ReservasSesionesClase");


        builder.HasKey(x =>
            x.Id);


        builder.HasIndex(x =>
            new
            {
                x.SesionClaseId,
                x.BebeId
            })
            .IsUnique();


        builder.HasOne(x =>
                x.SesionClase)
            .WithMany(x =>
                x.Reservas)
            .HasForeignKey(x =>
                x.SesionClaseId)
            .OnDelete(
                DeleteBehavior.Restrict);


        builder.HasOne(x =>
                x.Inscripcion)
            .WithMany(x =>
                x.ReservasSesiones)
            .HasForeignKey(x =>
                x.InscripcionId)
            .OnDelete(
                DeleteBehavior.Restrict);


        builder.HasOne(x =>
                x.Bebe)
            .WithMany()
            .HasForeignKey(x =>
                x.BebeId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}