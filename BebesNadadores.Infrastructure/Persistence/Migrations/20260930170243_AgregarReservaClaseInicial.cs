using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarReservaClaseInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReservasSesionesClase",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SesionClaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    InscripcionId = table.Column<Guid>(type: "uuid", nullable: false),
                    BebeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReservasSesionesClase", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReservasSesionesClase_Bebes_BebeId",
                        column: x => x.BebeId,
                        principalTable: "Bebes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasSesionesClase_Inscripciones_InscripcionId",
                        column: x => x.InscripcionId,
                        principalTable: "Inscripciones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReservasSesionesClase_SesionesClase_SesionClaseId",
                        column: x => x.SesionClaseId,
                        principalTable: "SesionesClase",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReservasSesionesClase_BebeId",
                table: "ReservasSesionesClase",
                column: "BebeId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasSesionesClase_InscripcionId",
                table: "ReservasSesionesClase",
                column: "InscripcionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReservasSesionesClase_SesionClaseId_BebeId",
                table: "ReservasSesionesClase",
                columns: new[] { "SesionClaseId", "BebeId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReservasSesionesClase");
        }
    }
}
