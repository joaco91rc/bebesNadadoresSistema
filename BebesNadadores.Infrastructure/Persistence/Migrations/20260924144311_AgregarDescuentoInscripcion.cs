using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarDescuentoInscripcion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MotivoDescuento",
                table: "Inscripciones",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TipoDescuento",
                table: "Inscripciones",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorDescuento",
                table: "Inscripciones",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MotivoDescuento",
                table: "Inscripciones");

            migrationBuilder.DropColumn(
                name: "TipoDescuento",
                table: "Inscripciones");

            migrationBuilder.DropColumn(
                name: "ValorDescuento",
                table: "Inscripciones");
        }
    }
}
