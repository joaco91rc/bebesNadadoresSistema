using System;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261124180000_AgregarBonificacionesCuota")]
public partial class AgregarBonificacionesCuota : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "BonificacionesCuota",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                Importe = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Motivo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                FechaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                AnuladaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                AnuladaPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                MotivoAnulacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_BonificacionesCuota", x => x.Id);
                table.ForeignKey(
                    name: "FK_BonificacionesCuota_Cuotas_CuotaId",
                    column: x => x.CuotaId,
                    principalTable: "Cuotas",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_BonificacionesCuota_Importe", "\"Importe\" > 0");
            });
        migrationBuilder.CreateIndex(
            name: "IX_BonificacionesCuota_CuotaId_FechaUtc",
            table: "BonificacionesCuota", columns: new[] { "CuotaId", "FechaUtc" });
        migrationBuilder.CreateIndex(
            name: "IX_BonificacionesCuota_UsuarioId",
            table: "BonificacionesCuota", column: "UsuarioId");
    }
    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "BonificacionesCuota");
}
