using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReactivacionesCuota : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CuotasReactivaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuotaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AnuladaUtcAnterior = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnuladaPorUsuarioIdAnterior = table.Column<Guid>(type: "uuid", nullable: true),
                    MotivoAnulacionAnterior = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CuotasReactivaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CuotasReactivaciones_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CuotasReactivaciones_Cuotas_CuotaId",
                        column: x => x.CuotaId,
                        principalTable: "Cuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CuotasReactivaciones_CuotaId_FechaUtc",
                table: "CuotasReactivaciones",
                columns: new[] { "CuotaId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CuotasReactivaciones_UsuarioId",
                table: "CuotasReactivaciones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CuotasReactivaciones");
        }
    }
}
