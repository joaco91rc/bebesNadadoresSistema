using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarProfesoresPorSesion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsModificadaManualmente",
                table: "SesionesClase",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "SesionesClaseProfesores",
                columns: table => new
                {
                    SesionClaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfesorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EsPrincipal = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesClaseProfesores", x => new { x.SesionClaseId, x.ProfesorId });
                    table.ForeignKey(
                        name: "FK_SesionesClaseProfesores_Profesores_ProfesorId",
                        column: x => x.ProfesorId,
                        principalTable: "Profesores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SesionesClaseProfesores_SesionesClase_SesionClaseId",
                        column: x => x.SesionClaseId,
                        principalTable: "SesionesClase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClaseProfesores_ProfesorId",
                table: "SesionesClaseProfesores",
                column: "ProfesorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SesionesClaseProfesores");

            migrationBuilder.DropColumn(
                name: "EsModificadaManualmente",
                table: "SesionesClase");
        }
    }
}
