using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarAntecedentesSaludBebe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DetalleHospitalizacion",
                table: "Bebes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DetalleTratamiento",
                table: "Bebes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PermanecioHospitalMasDe5Dias",
                table: "Bebes",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RecibeTratamiento",
                table: "Bebes",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetalleHospitalizacion",
                table: "Bebes");

            migrationBuilder.DropColumn(
                name: "DetalleTratamiento",
                table: "Bebes");

            migrationBuilder.DropColumn(
                name: "PermanecioHospitalMasDe5Dias",
                table: "Bebes");

            migrationBuilder.DropColumn(
                name: "RecibeTratamiento",
                table: "Bebes");
        }
    }
}
