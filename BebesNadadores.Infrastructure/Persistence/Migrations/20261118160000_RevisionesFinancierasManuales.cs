using BebesNadadores.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BebesNadadores.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261118160000_RevisionesFinancierasManuales")]
public sealed class RevisionesFinancierasManuales : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE TABLE IF NOT EXISTS "RevisionesFinancierasManuales" (
            "Id" uuid PRIMARY KEY,
            "BebeId" uuid NOT NULL REFERENCES "Bebes"("Id"),
            "FechaCorte" date NOT NULL,
            "UsuarioId" uuid NOT NULL,
            "RevisadaUtc" timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP
        );
        CREATE INDEX IF NOT EXISTS "IX_RevisionesFinancierasManuales_BebeId" ON "RevisionesFinancierasManuales"("BebeId");
        """);
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE IF EXISTS \"RevisionesFinancierasManuales\";");
}
