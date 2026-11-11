using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegularizacionRecuperosHistoricos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SesionOrigenId",
                table: "Recuperaciones",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaVerificacionHistorica",
                table: "Recuperaciones",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NivelOrigenId",
                table: "Recuperaciones",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroCreditoHistorico",
                table: "Recuperaciones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ObservacionesRegularizacion",
                table: "Recuperaciones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_BebeId_NumeroCreditoHistorico",
                table: "Recuperaciones",
                columns: new[] { "BebeId", "NumeroCreditoHistorico" },
                unique: true,
                filter: "\"NumeroCreditoHistorico\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_NivelOrigenId",
                table: "Recuperaciones",
                column: "NivelOrigenId");

            migrationBuilder.AddForeignKey(
                name: "FK_Recuperaciones_Niveles_NivelOrigenId",
                table: "Recuperaciones",
                column: "NivelOrigenId",
                principalTable: "Niveles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recuperaciones_Niveles_NivelOrigenId",
                table: "Recuperaciones");

            migrationBuilder.DropIndex(
                name: "IX_Recuperaciones_BebeId_NumeroCreditoHistorico",
                table: "Recuperaciones");

            migrationBuilder.DropIndex(
                name: "IX_Recuperaciones_NivelOrigenId",
                table: "Recuperaciones");

            migrationBuilder.DropColumn(
                name: "FechaVerificacionHistorica",
                table: "Recuperaciones");

            migrationBuilder.DropColumn(
                name: "NivelOrigenId",
                table: "Recuperaciones");

            migrationBuilder.DropColumn(
                name: "NumeroCreditoHistorico",
                table: "Recuperaciones");

            migrationBuilder.DropColumn(
                name: "ObservacionesRegularizacion",
                table: "Recuperaciones");

            migrationBuilder.AlterColumn<Guid>(
                name: "SesionOrigenId",
                table: "Recuperaciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
