using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProfesorReemplazado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReemplazaAProfesorId",
                table: "SesionesClaseProfesores",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClaseProfesores_ReemplazaAProfesorId",
                table: "SesionesClaseProfesores",
                column: "ReemplazaAProfesorId");

            migrationBuilder.AddForeignKey(
                name: "FK_SesionesClaseProfesores_Profesores_ReemplazaAProfesorId",
                table: "SesionesClaseProfesores",
                column: "ReemplazaAProfesorId",
                principalTable: "Profesores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SesionesClaseProfesores_Profesores_ReemplazaAProfesorId",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropIndex(
                name: "IX_SesionesClaseProfesores_ReemplazaAProfesorId",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "ReemplazaAProfesorId",
                table: "SesionesClaseProfesores");
        }
    }
}
