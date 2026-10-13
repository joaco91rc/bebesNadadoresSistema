using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AgregarConfiguracionSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracionSistema",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NombreInstitucion = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WhatsApp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DiasVigenciaRecupero = table.Column<int>(type: "integer", nullable: false),
                    MesesVigenciaApto = table.Column<int>(type: "integer", nullable: false),
                    DiasAlertaApto = table.Column<int>(type: "integer", nullable: false),
                    DiaVencimientoCuota = table.Column<int>(type: "integer", nullable: false),
                    EdadMaximaMeses = table.Column<int>(type: "integer", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionSistema", x => x.Id);
                    table.CheckConstraint("CK_ConfiguracionSistema_DiasAlertaApto", "\"DiasAlertaApto\" >= 0");
                    table.CheckConstraint("CK_ConfiguracionSistema_DiasVigenciaRecupero", "\"DiasVigenciaRecupero\" > 0");
                    table.CheckConstraint("CK_ConfiguracionSistema_DiaVencimientoCuota", "\"DiaVencimientoCuota\" BETWEEN 1 AND 28");
                    table.CheckConstraint("CK_ConfiguracionSistema_EdadMaximaMeses", "\"EdadMaximaMeses\" BETWEEN 1 AND 84");
                    table.CheckConstraint("CK_ConfiguracionSistema_MesesVigenciaApto", "\"MesesVigenciaApto\" > 0");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionSistema");
        }
    }
}
