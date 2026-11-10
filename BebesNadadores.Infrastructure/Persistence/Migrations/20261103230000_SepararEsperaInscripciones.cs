using System;
using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BebesNadadores.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261103230000_SepararEsperaInscripciones")]
public partial class SepararEsperaInscripciones : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "EsperaInscripciones",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                NombreBebe = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                NombreResponsable = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Telefono = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                NivelId = table.Column<Guid>(type: "uuid", nullable: false),
                TurnoId = table.Column<Guid>(type: "uuid", nullable: true),
                DiaPreferido = table.Column<int>(type: "integer", nullable: true),
                FranjaHoraria = table.Column<int>(type: "integer", nullable: false),
                Estado = table.Column<int>(type: "integer", nullable: false),
                FechaAltaUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FechaNotificacionUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                BebeInscriptoId = table.Column<Guid>(type: "uuid", nullable: true),
                CreadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                ModificadoUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ModificadoPorUsuarioId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_EsperaInscripciones", x => x.Id);
                table.ForeignKey("FK_EsperaInscripciones_Niveles_NivelId", x => x.NivelId, "Niveles", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_EsperaInscripciones_Turnos_TurnoId", x => x.TurnoId, "Turnos", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_EsperaInscripciones_Bebes_BebeInscriptoId", x => x.BebeInscriptoId, "Bebes", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_EsperaInscripciones_Estado_NivelId_DiaPreferido", "EsperaInscripciones", new[] { "Estado", "NivelId", "DiaPreferido" });
        migrationBuilder.CreateIndex("IX_EsperaInscripciones_Telefono", "EsperaInscripciones", "Telefono");
        migrationBuilder.CreateIndex("IX_EsperaInscripciones_NivelId", "EsperaInscripciones", "NivelId");
        migrationBuilder.CreateIndex("IX_EsperaInscripciones_TurnoId", "EsperaInscripciones", "TurnoId");
        migrationBuilder.CreateIndex("IX_EsperaInscripciones_BebeInscriptoId", "EsperaInscripciones", "BebeInscriptoId");
    }
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("EsperaInscripciones");
}
