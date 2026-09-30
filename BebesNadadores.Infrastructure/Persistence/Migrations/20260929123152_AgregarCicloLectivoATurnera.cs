using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarCicloLectivoATurnera : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CicloLectivoId",
                table: "Turnos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CiclosLectivos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Anio = table.Column<int>(type: "integer", nullable: false),
                    FechaInicio = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaFin = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<int>(type: "integer", nullable: false),
                    FechaPublicacionUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CiclosLectivos", x => x.Id);
                    table.CheckConstraint("CK_CiclosLectivos_Fechas", "\"FechaFin\" >= \"FechaInicio\"");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_CicloLectivoId",
                table: "Turnos",
                column: "CicloLectivoId");

            migrationBuilder.CreateIndex(
                name: "IX_CiclosLectivos_Anio",
                table: "CiclosLectivos",
                column: "Anio",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Turnos_CiclosLectivos_CicloLectivoId",
                table: "Turnos",
                column: "CicloLectivoId",
                principalTable: "CiclosLectivos",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Turnos_CiclosLectivos_CicloLectivoId",
                table: "Turnos");

            migrationBuilder.DropTable(
                name: "CiclosLectivos");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_CicloLectivoId",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "CicloLectivoId",
                table: "Turnos");
        }
    }
}
