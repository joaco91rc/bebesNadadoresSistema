using BebesNadadores.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public sealed class ReservaVacanteCicloConfig : IEntityTypeConfiguration<ReservaVacanteCiclo>
{
    public void Configure(EntityTypeBuilder<ReservaVacanteCiclo> e)
    {
        e.ToTable("ReservasVacantesCiclo");
        e.HasKey(x => x.Id);
        e.Property(x => x.Importe).HasPrecision(18, 2).IsRequired();
        // Mantiene la regla existente de una reserva por bebé y ciclo.
        // Si una reserva impaga se cancela, se puede reactivar la misma fila.
        e.HasIndex(x => new { x.BebeId, x.CicloLectivoId }).IsUnique();
        e.HasIndex(x => new { x.TurnoId, x.Estado });
        // PostgreSQL permite varios NULL en un índice UNIQUE: las reservas impagas no chocan.
        e.HasIndex(x => x.PagoId).IsUnique();
        e.HasIndex(x => x.InscripcionId).IsUnique()
            .HasFilter("\"InscripcionId\" IS NOT NULL");
        e.HasOne(x => x.Inscripcion).WithMany()
            .HasForeignKey(x => x.InscripcionId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Bebe).WithMany()
            .HasForeignKey(x => x.BebeId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.CicloLectivo).WithMany()
            .HasForeignKey(x => x.CicloLectivoId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Turno).WithMany()
            .HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne(x => x.Pago).WithMany()
            .HasForeignKey(x => x.PagoId).IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        e.ToTable(t => t.HasCheckConstraint(
            "CK_ReservasVacantesCiclo_Importe", "\"Importe\" > 0"));
    }
}
