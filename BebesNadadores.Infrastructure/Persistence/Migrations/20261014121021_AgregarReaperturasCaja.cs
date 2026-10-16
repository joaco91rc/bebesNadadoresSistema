using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReaperturasCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CajasReaperturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CajaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    Motivo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    FechaCierreAnteriorUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsuarioCierreAnteriorId = table.Column<Guid>(type: "uuid", nullable: false),
                    SaldoFinalDeclaradoAnterior = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CajasReaperturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CajasReaperturas_AspNetUsers_UsuarioCierreAnteriorId",
                        column: x => x.UsuarioCierreAnteriorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CajasReaperturas_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CajasReaperturas_Cajas_CajaId",
                        column: x => x.CajaId,
                        principalTable: "Cajas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CajasReaperturas_CajaId_FechaUtc",
                table: "CajasReaperturas",
                columns: new[] { "CajaId", "FechaUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CajasReaperturas_UsuarioCierreAnteriorId",
                table: "CajasReaperturas",
                column: "UsuarioCierreAnteriorId");

            migrationBuilder.CreateIndex(
                name: "IX_CajasReaperturas_UsuarioId",
                table: "CajasReaperturas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CajasReaperturas");
        }
    }
}
