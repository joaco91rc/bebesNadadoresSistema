using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFechaOperativaCaja : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "FechaOperativa",
                table: "Cajas",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.CreateIndex(
                name: "IX_Cajas_FechaOperativa",
                table: "Cajas",
                column: "FechaOperativa",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cajas_SaldoFinalDeclarado",
                table: "Cajas",
                sql: "\"SaldoFinalDeclarado\" IS NULL OR \"SaldoFinalDeclarado\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Cajas_SaldoInicial",
                table: "Cajas",
                sql: "\"SaldoInicial\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cajas_FechaOperativa",
                table: "Cajas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cajas_SaldoFinalDeclarado",
                table: "Cajas");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Cajas_SaldoInicial",
                table: "Cajas");

            migrationBuilder.DropColumn(
                name: "FechaOperativa",
                table: "Cajas");
        }
    }
}
