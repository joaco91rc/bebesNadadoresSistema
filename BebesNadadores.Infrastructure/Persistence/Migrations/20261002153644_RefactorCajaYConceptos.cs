using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefactorCajaYConceptos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_Tipo_Categoria_MedioPago",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "AfectaSaldoEfectivo",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "MedioPago",
                table: "MovimientosCaja");

            migrationBuilder.AddColumn<Guid>(
                name: "ConceptoCajaId",
                table: "MovimientosCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Detalle",
                table: "MovimientosCaja",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConceptosCaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    Orden = table.Column<int>(type: "integer", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConceptosCaja", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_ConceptoCajaId",
                table: "MovimientosCaja",
                column: "ConceptoCajaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_Tipo_ConceptoCajaId",
                table: "MovimientosCaja",
                columns: new[] { "Tipo", "ConceptoCajaId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosCaja_Tipo_Activo_Orden",
                table: "ConceptosCaja",
                columns: new[] { "Tipo", "Activo", "Orden" });

            migrationBuilder.CreateIndex(
                name: "IX_ConceptosCaja_Tipo_Nombre",
                table: "ConceptosCaja",
                columns: new[] { "Tipo", "Nombre" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_ConceptosCaja_ConceptoCajaId",
                table: "MovimientosCaja",
                column: "ConceptoCajaId",
                principalTable: "ConceptosCaja",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_ConceptosCaja_ConceptoCajaId",
                table: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "ConceptosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_ConceptoCajaId",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_Tipo_ConceptoCajaId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "ConceptoCajaId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "Detalle",
                table: "MovimientosCaja");

            migrationBuilder.AddColumn<bool>(
                name: "AfectaSaldoEfectivo",
                table: "MovimientosCaja",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "MovimientosCaja",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MedioPago",
                table: "MovimientosCaja",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_Tipo_Categoria_MedioPago",
                table: "MovimientosCaja",
                columns: new[] { "Tipo", "Categoria", "MedioPago" });
        }
    }
}
