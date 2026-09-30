using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarControlDocente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmadoPorUsuarioId",
                table: "SesionesClaseProfesores",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConfirmadoUtc",
                table: "SesionesClaseProfesores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ControlConfirmado",
                table: "SesionesClaseProfesores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "DictoClase",
                table: "SesionesClaseProfesores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EsReemplazo",
                table: "SesionesClaseProfesores",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClaseProfesores_ConfirmadoPorUsuarioId",
                table: "SesionesClaseProfesores",
                column: "ConfirmadoPorUsuarioId");

            migrationBuilder.AddForeignKey(
                name: "FK_SesionesClaseProfesores_AspNetUsers_ConfirmadoPorUsuarioId",
                table: "SesionesClaseProfesores",
                column: "ConfirmadoPorUsuarioId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SesionesClaseProfesores_AspNetUsers_ConfirmadoPorUsuarioId",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropIndex(
                name: "IX_SesionesClaseProfesores_ConfirmadoPorUsuarioId",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "ConfirmadoPorUsuarioId",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "ConfirmadoUtc",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "ControlConfirmado",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "DictoClase",
                table: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "EsReemplazo",
                table: "SesionesClaseProfesores");
        }
    }
}
