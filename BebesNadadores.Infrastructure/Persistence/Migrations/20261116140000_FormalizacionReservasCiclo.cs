using System;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace BebesNadadores.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261116140000_FormalizacionReservasCiclo")]
public sealed class FormalizacionReservasCiclo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "InscripcionId", table: "ReservasVacantesCiclo",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "FechaFormalizacionUtc", table: "ReservasVacantesCiclo",
            type: "timestamp with time zone", nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_ReservasVacantesCiclo_InscripcionId",
            table: "ReservasVacantesCiclo", column: "InscripcionId",
            unique: true, filter: "\"InscripcionId\" IS NOT NULL");
        migrationBuilder.AddForeignKey(
            name: "FK_ReservasVacantesCiclo_Inscripciones_InscripcionId",
            table: "ReservasVacantesCiclo", column: "InscripcionId",
            principalTable: "Inscripciones", principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_ReservasVacantesCiclo_Inscripciones_InscripcionId",
            table: "ReservasVacantesCiclo");
        migrationBuilder.DropIndex(
            name: "IX_ReservasVacantesCiclo_InscripcionId",
            table: "ReservasVacantesCiclo");
        migrationBuilder.DropColumn(name: "FechaFormalizacionUtc", table: "ReservasVacantesCiclo");
        migrationBuilder.DropColumn(name: "InscripcionId", table: "ReservasVacantesCiclo");
    }
}
