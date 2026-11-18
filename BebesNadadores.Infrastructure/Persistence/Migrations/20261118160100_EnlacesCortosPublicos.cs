using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using BebesNadadores.Infrastructure.Persistence;
namespace BebesNadadores.Infrastructure.Persistence.Migrations;
[DbContext(typeof(AppDbContext))]
[Migration("20261118160100_EnlacesCortosPublicos")]
public sealed class EnlacesCortosPublicos : Migration
{
    protected override void Up(MigrationBuilder m) => m.Sql("""
        CREATE TABLE IF NOT EXISTS "EnlacesCortosPublicos" (
            "Slug" varchar(160) PRIMARY KEY,
            "Destino" text NOT NULL,
            "VenceUtc" timestamptz NOT NULL
        );
        CREATE INDEX IF NOT EXISTS "IX_EnlacesCortosPublicos_VenceUtc" ON "EnlacesCortosPublicos" ("VenceUtc");
        """);
    protected override void Down(MigrationBuilder m) => m.Sql("DROP TABLE IF EXISTS \"EnlacesCortosPublicos\";");
}
