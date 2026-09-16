using BebesNadadores.Domain.Entities;
using BebesNadadores.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BebesNadadores.Infrastructure.Persistence.Configurations;

public class CuotaConfig : IEntityTypeConfiguration<Cuota>
{
    public void Configure(EntityTypeBuilder<Cuota> builder)
    {
        builder.ToTable("Cuotas", t =>
        {
            t.HasCheckConstraint("CK_Cuotas_Importe", "\"Importe\" >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Importe).HasPrecision(18, 2);
        builder.Property(x => x.Descripcion).HasMaxLength(500);

        builder.Ignore(x => x.ImportePagado);
        builder.Ignore(x => x.Saldo);

        builder.HasIndex(x => new { x.InscripcionId, x.Periodo }).IsUnique();
        builder.HasIndex(x => new { x.Estado, x.FechaVencimiento });

        builder.HasOne(x => x.Inscripcion)
            .WithMany(x => x.Cuotas)
            .HasForeignKey(x => x.InscripcionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PagoConfig : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> builder)
    {
        builder.ToTable("Pagos", t =>
        {
            t.HasCheckConstraint("CK_Pagos_Importe", "\"Importe\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Importe).HasPrecision(18, 2);
        builder.Property(x => x.ReferenciaExterna).HasMaxLength(200);
        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        builder.HasIndex(x => new { x.BebeId, x.FechaUtc });
        builder.HasIndex(x => x.ReferenciaExterna);

        builder.HasOne(x => x.Bebe)
            .WithMany()
            .HasForeignKey(x => x.BebeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Responsable)
            .WithMany()
            .HasForeignKey(x => x.ResponsableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Caja)
            .WithMany(x => x.Pagos)
            .HasForeignKey(x => x.CajaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ImputacionPagoConfig : IEntityTypeConfiguration<ImputacionPago>
{
    public void Configure(EntityTypeBuilder<ImputacionPago> builder)
    {
        builder.ToTable("ImputacionesPago", t =>
        {
            t.HasCheckConstraint("CK_ImputacionesPago_Importe", "\"Importe\" > 0");
        });

        builder.HasKey(x => new { x.PagoId, x.CuotaId });

        builder.Property(x => x.Importe).HasPrecision(18, 2);

        builder.HasOne(x => x.Pago)
            .WithMany(x => x.Imputaciones)
            .HasForeignKey(x => x.PagoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Cuota)
            .WithMany(x => x.Imputaciones)
            .HasForeignKey(x => x.CuotaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CajaConfig : IEntityTypeConfiguration<Caja>
{
    public void Configure(EntityTypeBuilder<Caja> builder)
    {
        builder.ToTable("Cajas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SaldoInicial).HasPrecision(18, 2);
        builder.Property(x => x.SaldoFinalDeclarado).HasPrecision(18, 2);
        builder.Property(x => x.Observaciones).HasMaxLength(1000);

        builder.HasIndex(x => new { x.Estado, x.FechaAperturaUtc });

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioAperturaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UsuarioCierreId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class MovimientoCajaConfig : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable("MovimientosCaja", t =>
        {
            t.HasCheckConstraint("CK_MovimientosCaja_Importe", "\"Importe\" > 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Importe).HasPrecision(18, 2);
        builder.Property(x => x.Concepto).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Referencia).HasMaxLength(200);

        builder.HasIndex(x => new { x.CajaId, x.FechaUtc });
        builder.HasIndex(x => new { x.Tipo, x.Categoria, x.MedioPago });

        builder.HasOne(x => x.Caja)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(x => x.CajaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Pago)
            .WithMany()
            .HasForeignKey(x => x.PagoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
