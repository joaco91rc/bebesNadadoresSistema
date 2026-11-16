using System;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace BebesNadadores.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261116120000_ReservasVacantesCiclo")]
public sealed class ReservasVacantesCiclo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ReservasVacantesCiclo",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                CicloLectivoId = table.Column<Guid>(type: "uuid", nullable: false),
                TurnoId = table.Column<Guid>(type: "uuid", nullable: false),
                PagoId = table.Column<Guid>(type: "uuid", nullable: false),
                Importe = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                Estado = table.Column<int>(type: "integer", nullable: false),
                FechaReservaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ReservasVacantesCiclo", x => x.Id);
                table.CheckConstraint("CK_ReservasVacantesCiclo_Importe", "\"Importe\" > 0");
                table.ForeignKey("FK_ReservasVacantesCiclo_Bebes_BebeId", x => x.BebeId, "Bebes", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ReservasVacantesCiclo_CiclosLectivos_CicloLectivoId", x => x.CicloLectivoId, "CiclosLectivos", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ReservasVacantesCiclo_Turnos_TurnoId", x => x.TurnoId, "Turnos", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_ReservasVacantesCiclo_Pagos_PagoId", x => x.PagoId, "Pagos", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_ReservasVacantesCiclo_BebeId_CicloLectivoId", "ReservasVacantesCiclo", new[] { "BebeId", "CicloLectivoId" }, unique: true);
        migrationBuilder.CreateIndex("IX_ReservasVacantesCiclo_CicloLectivoId", "ReservasVacantesCiclo", "CicloLectivoId");
        migrationBuilder.CreateIndex("IX_ReservasVacantesCiclo_PagoId", "ReservasVacantesCiclo", "PagoId", unique: true);
        migrationBuilder.CreateIndex("IX_ReservasVacantesCiclo_TurnoId_Estado", "ReservasVacantesCiclo", new[] { "TurnoId", "Estado" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ReservasVacantesCiclo");
    }
}
