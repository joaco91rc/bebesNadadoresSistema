using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarRecuperoInicioMitadMes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BonificaRecuperoInicio",
                table: "Inscripciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BonificaRecuperoInicio",
                table: "Inscripciones");
        }
    }
}
