using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TurneraAsistenciaRecuperos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recuperaciones_Asistencias_AsistenciaOrigenId",
                table: "Recuperaciones");

            migrationBuilder.AddColumn<DateOnly>(
                name: "VigenteDesde",
                table: "Turnos",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(2026, 1, 1));

            migrationBuilder.AddColumn<DateOnly>(
                name: "VigenteHasta",
                table: "Turnos",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DiaNoLaborableId",
                table: "SesionesClase",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "FechaVencimiento",
                table: "Recuperaciones",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<Guid>(
                name: "AsistenciaOrigenId",
                table: "Recuperaciones",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Recuperaciones",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "DiasNoLaborables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TodoElDia = table.Column<bool>(type: "boolean", nullable: false),
                    HoraDesde = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    HoraHasta = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    GeneraRecupero = table.Column<bool>(type: "boolean", nullable: false),
                    RecuperoSinVencimiento = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiasNoLaborables", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_DiaSemana_HoraInicio",
                table: "Turnos",
                columns: new[] { "DiaSemana", "HoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Turnos_Estado_VigenteDesde",
                table: "Turnos",
                columns: new[] { "Estado", "VigenteDesde" });

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClase_DiaNoLaborableId",
                table: "SesionesClase",
                column: "DiaNoLaborableId");

            migrationBuilder.CreateIndex(
                name: "IX_SesionesClase_Fecha_HoraInicio",
                table: "SesionesClase",
                columns: new[] { "Fecha", "HoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_Recuperaciones_BebeId_SesionOrigenId",
                table: "Recuperaciones",
                columns: new[] { "BebeId", "SesionOrigenId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiasNoLaborables_Fecha_Activo",
                table: "DiasNoLaborables",
                columns: new[] { "Fecha", "Activo" });

            migrationBuilder.AddForeignKey(
                name: "FK_Recuperaciones_Asistencias_AsistenciaOrigenId",
                table: "Recuperaciones",
                column: "AsistenciaOrigenId",
                principalTable: "Asistencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SesionesClase_DiasNoLaborables_DiaNoLaborableId",
                table: "SesionesClase",
                column: "DiaNoLaborableId",
                principalTable: "DiasNoLaborables",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Recuperaciones_Asistencias_AsistenciaOrigenId",
                table: "Recuperaciones");

            migrationBuilder.DropForeignKey(
                name: "FK_SesionesClase_DiasNoLaborables_DiaNoLaborableId",
                table: "SesionesClase");

            migrationBuilder.DropTable(
                name: "DiasNoLaborables");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_DiaSemana_HoraInicio",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_Turnos_Estado_VigenteDesde",
                table: "Turnos");

            migrationBuilder.DropIndex(
                name: "IX_SesionesClase_DiaNoLaborableId",
                table: "SesionesClase");

            migrationBuilder.DropIndex(
                name: "IX_SesionesClase_Fecha_HoraInicio",
                table: "SesionesClase");

            migrationBuilder.DropIndex(
                name: "IX_Recuperaciones_BebeId_SesionOrigenId",
                table: "Recuperaciones");

            migrationBuilder.DropColumn(
                name: "VigenteDesde",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "VigenteHasta",
                table: "Turnos");

            migrationBuilder.DropColumn(
                name: "DiaNoLaborableId",
                table: "SesionesClase");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Recuperaciones");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "FechaVencimiento",
                table: "Recuperaciones",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AsistenciaOrigenId",
                table: "Recuperaciones",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Recuperaciones_Asistencias_AsistenciaOrigenId",
                table: "Recuperaciones",
                column: "AsistenciaOrigenId",
                principalTable: "Asistencias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
