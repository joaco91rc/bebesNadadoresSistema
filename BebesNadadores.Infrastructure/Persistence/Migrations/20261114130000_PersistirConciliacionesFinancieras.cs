using System;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace BebesNadadores.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261114130000_PersistirConciliacionesFinancieras")]
public sealed class PersistirConciliacionesFinancieras : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(
            name: "ConciliacionesFinancierasBebes",
            columns: t => new
            {
                BebeId = t.Column<Guid>(type: "uuid", nullable: false),
                FechaCorte = t.Column<DateOnly>(type: "date", nullable: false),
                MesesVencidosSinConciliar = t.Column<int>(type: "integer", nullable: false),
                CodigoMigracion = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ImportadaUtc = t.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: t =>
            {
                t.PrimaryKey("PK_ConciliacionesFinancierasBebes", x => x.BebeId);
                t.CheckConstraint("CK_ConciliacionesFinancierasBebes_MesesNoNegativos", "\"MesesVencidosSinConciliar\" >= 0");
            });
        m.CreateIndex("IX_ConciliacionesFinancierasBebes_CodigoMigracion",
            "ConciliacionesFinancierasBebes", "CodigoMigracion");
    }

    protected override void Down(MigrationBuilder m) => m.DropTable("ConciliacionesFinancierasBebes");
}
