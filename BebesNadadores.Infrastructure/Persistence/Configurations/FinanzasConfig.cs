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
    public void Configure(
        EntityTypeBuilder<Pago> builder)
    {
        // =====================================================
        // TABLA / RESTRICCIONES
        // =====================================================

        builder.ToTable(
            "Pagos",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_Pagos_Importe",
                    "\"Importe\" > 0");
            });


        builder.HasKey(x =>
            x.Id);


        // =====================================================
        // PROPIEDADES
        // =====================================================

        builder.Property(x =>
                x.FechaUtc)
            .IsRequired();


        builder.Property(x =>
                x.Importe)
            .HasPrecision(
                18,
                2)
            .IsRequired();


        builder.Property(x =>
                x.MedioPago)
            .IsRequired();


        builder.Property(x =>
                x.ReferenciaExterna)
            .HasMaxLength(
                200);


        builder.Property(x =>
                x.Observaciones)
            .HasMaxLength(
                1000);


        // =====================================================
        // ÍNDICES
        // =====================================================

        builder.HasIndex(x =>
            new
            {
                x.BebeId,
                x.FechaUtc
            });


        builder.HasIndex(x =>
            x.ReferenciaExterna);


        builder.HasIndex(x =>
            x.ResponsableId);


        builder.HasIndex(x =>
            x.CajaId);


        // NUEVO
        builder.HasIndex(x =>
            x.CuentaBancariaId);


        // =====================================================
        // BEBÉ
        // =====================================================

        builder.HasOne(x =>
                x.Bebe)

            .WithMany()

            .HasForeignKey(x =>
                x.BebeId)

            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // RESPONSABLE
        // =====================================================

        builder.HasOne(x =>
                x.Responsable)

            .WithMany()

            .HasForeignKey(x =>
                x.ResponsableId)

            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // CAJA
        // =====================================================

        builder.HasOne(x =>
                x.Caja)

            .WithMany(x =>
                x.Pagos)

            .HasForeignKey(x =>
                x.CajaId)

            .OnDelete(
                DeleteBehavior.Restrict);


        // =====================================================
        // CUENTA BANCARIA
        // =====================================================

        builder.HasOne(x =>
                x.CuentaBancaria)

            .WithMany()

            .HasForeignKey(x =>
                x.CuentaBancariaId)

            .OnDelete(
                DeleteBehavior.Restrict);
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

public class CajaConfig
    : IEntityTypeConfiguration<Caja>
{
    public void Configure(
        EntityTypeBuilder<Caja> builder)
    {
         builder.ToTable(
            "Cajas",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_Cajas_SaldoInicial",
                    "\"SaldoInicial\" >= 0");

                t.HasCheckConstraint(
                    "CK_Cajas_SaldoFinalDeclarado",
                    "\"SaldoFinalDeclarado\" IS NULL OR \"SaldoFinalDeclarado\" >= 0");
            });


        builder.HasKey(x =>
            x.Id);


        builder.Property(x =>
                x.FechaOperativa)
            .IsRequired();


        builder.Property(x =>
                x.SaldoInicial)
            .HasPrecision(
                18,
                2);


        builder.Property(x =>
                x.SaldoFinalDeclarado)
            .HasPrecision(
                18,
                2);


        builder.Property(x =>
                x.Observaciones)
            .HasMaxLength(
                1000);

                builder.Property(x =>
        x.FechaOperativa)
    .IsRequired();


builder.HasIndex(x =>
        x.FechaOperativa)
    .IsUnique();


        /*
         * UNA SOLA CAJA POR DÍA.
         */
        builder.HasIndex(x =>
                x.FechaOperativa)
            .IsUnique();


        builder.HasIndex(x =>
            new
            {
                x.Estado,
                x.FechaAperturaUtc
            });


        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x =>
                x.UsuarioAperturaId)
            .OnDelete(
                DeleteBehavior.Restrict);


        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x =>
                x.UsuarioCierreId)
            .OnDelete(
                DeleteBehavior.Restrict);
    }
}

public class MovimientoCajaConfig
    : IEntityTypeConfiguration<MovimientoCaja>
{
    public void Configure(
        EntityTypeBuilder<MovimientoCaja> builder)
    {
        builder.ToTable(
            "MovimientosCaja",
            t =>
            {
                t.HasCheckConstraint(
                    "CK_MovimientosCaja_Importe",
                    "\"Importe\" > 0");
            });


        builder.HasKey(x =>
            x.Id);


        builder.Property(x =>
                x.Importe)
            .HasPrecision(
                18,
                2);


        builder.Property(x =>
                x.Concepto)
            .HasMaxLength(300)
            .IsRequired();


        builder.Property(x =>
                x.Detalle)
            .HasMaxLength(500);


        builder.Property(x =>
                x.Referencia)
            .HasMaxLength(200);


        builder.HasIndex(x =>
            new
            {
                x.CajaId,
                x.FechaUtc
            });


        builder.HasIndex(x =>
            new
            {
                x.Tipo,
                x.ConceptoCajaId
            });


        builder.HasOne(x =>
                x.Caja)

            .WithMany(x =>
                x.Movimientos)

            .HasForeignKey(x =>
                x.CajaId)

            .OnDelete(
                DeleteBehavior.Restrict);


        builder.HasOne(x =>
                x.ConceptoCaja)

            .WithMany()

            .HasForeignKey(x =>
                x.ConceptoCajaId)

            .OnDelete(
                DeleteBehavior.Restrict);


        builder.HasOne(x =>
                x.Pago)

            .WithMany()

            .HasForeignKey(x =>
                x.PagoId)

            .OnDelete(
                DeleteBehavior.Restrict);

                builder.HasOne(x =>
        x.PagoProveedor)
    .WithMany()
    .HasForeignKey(x =>
        x.PagoProveedorId)
    .OnDelete(
        DeleteBehavior.Restrict);


builder.HasIndex(x =>
    x.PagoProveedorId);
    }
}
