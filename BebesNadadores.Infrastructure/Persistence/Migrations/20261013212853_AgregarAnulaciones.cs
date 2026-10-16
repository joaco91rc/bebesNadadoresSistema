using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAnulaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AnuladoPorUsuarioId",
                table: "PagosProveedores",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnuladoUtc",
                table: "PagosProveedores",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "PagosProveedores",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnuladoPorUsuarioId",
                table: "Pagos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnuladoUtc",
                table: "Pagos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Pagos",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnulaMovimientoId",
                table: "MovimientosCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnuladoPorUsuarioId",
                table: "MovimientosCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnuladoUtc",
                table: "MovimientosCaja",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "MovimientosCaja",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnulaMovimientoId",
                table: "MovimientosBancarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnuladoPorUsuarioId",
                table: "MovimientosBancarios",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnuladoUtc",
                table: "MovimientosBancarios",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "MovimientosBancarios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AnuladaPorUsuarioId",
                table: "Cuotas",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnuladaUtc",
                table: "Cuotas",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MotivoAnulacion",
                table: "Cuotas",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_AnulaMovimientoId",
                table: "MovimientosCaja",
                column: "AnulaMovimientoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosBancarios_AnulaMovimientoId",
                table: "MovimientosBancarios",
                column: "AnulaMovimientoId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosBancarios_MovimientosBancarios_AnulaMovimientoId",
                table: "MovimientosBancarios",
                column: "AnulaMovimientoId",
                principalTable: "MovimientosBancarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_MovimientosCaja_AnulaMovimientoId",
                table: "MovimientosCaja",
                column: "AnulaMovimientoId",
                principalTable: "MovimientosCaja",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosBancarios_MovimientosBancarios_AnulaMovimientoId",
                table: "MovimientosBancarios");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_MovimientosCaja_AnulaMovimientoId",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_AnulaMovimientoId",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosBancarios_AnulaMovimientoId",
                table: "MovimientosBancarios");

            migrationBuilder.DropColumn(
                name: "AnuladoPorUsuarioId",
                table: "PagosProveedores");

            migrationBuilder.DropColumn(
                name: "AnuladoUtc",
                table: "PagosProveedores");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "PagosProveedores");

            migrationBuilder.DropColumn(
                name: "AnuladoPorUsuarioId",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "AnuladoUtc",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Pagos");

            migrationBuilder.DropColumn(
                name: "AnulaMovimientoId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "AnuladoPorUsuarioId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "AnuladoUtc",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "AnulaMovimientoId",
                table: "MovimientosBancarios");

            migrationBuilder.DropColumn(
                name: "AnuladoPorUsuarioId",
                table: "MovimientosBancarios");

            migrationBuilder.DropColumn(
                name: "AnuladoUtc",
                table: "MovimientosBancarios");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "MovimientosBancarios");

            migrationBuilder.DropColumn(
                name: "AnuladaPorUsuarioId",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "AnuladaUtc",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "MotivoAnulacion",
                table: "Cuotas");
        }
    }
}
