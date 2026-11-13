#!/usr/bin/env bash
set -euo pipefail

DB_CONTAINER="bebesnadadores-db"
TEST_DATABASE="BebesNadadores_MigracionTest"
ACCOUNT_EMAIL="ADMIN@BEBESNADADORES.COM"

if ! command -v docker >/dev/null 2>&1; then
  echo 'ERROR: Docker no esta disponible.' >&2; exit 1
fi
if ! docker inspect "$DB_CONTAINER" >/dev/null 2>&1; then
  echo 'ERROR: No existe el contenedor PostgreSQL esperado.' >&2; exit 1
fi

# Verificar que el usuario exista exactamente una vez, EXCLUSIVAMENTE en TEST.
count="$(docker exec "$DB_CONTAINER" sh -c 'psql -X -U "$POSTGRES_USER" -d BebesNadadores_MigracionTest -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM \"AspNetUsers\" WHERE \"NormalizedEmail\" = '\''ADMIN@BEBESNADADORES.COM'\'';"')"
if [[ "$count" != "1" ]]; then
  echo "ERROR: se esperaban exactamente 1 administrador en TEST y se encontraron: $count" >&2; exit 1
fi

read -r -s -p 'Nueva contrasena ADMIN de TEST: ' NEW_PASSWORD
printf '\n'
read -r -s -p 'Repetir contrasena: ' CONFIRM_PASSWORD
printf '\n'
if [[ "$NEW_PASSWORD" != "$CONFIRM_PASSWORD" || ${#NEW_PASSWORD} -lt 12 ]]; then
  unset NEW_PASSWORD CONFIRM_PASSWORD
  echo 'ERROR: las contrasenas no coinciden o tienen menos de 12 caracteres.' >&2; exit 1
fi
unset CONFIRM_PASSWORD

TMP="$(mktemp -d /tmp/bbn-reset-test.XXXXXXXX)"
trap 'unset NEW_PASSWORD; rm -rf "$TMP"' EXIT
cat > "$TMP/ResetIdentity.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
  </ItemGroup>
</Project>
EOF
cat > "$TMP/Program.cs" <<'EOF'
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
var pass = Console.ReadLine() ?? throw new InvalidOperationException("No se recibio contrasena");
var hasher = new PasswordHasher<IdentityUser>(Options.Create(new PasswordHasherOptions
{
    CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3,
    IterationCount = 100_000
}));
Console.WriteLine(hasher.HashPassword(new IdentityUser(), pass));
EOF

printf 'Generando hash ASP.NET Core Identity en contenedor SDK temporal...\n'
docker run --rm --network none -v "$TMP:/work" -w /work mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet build ResetIdentity.csproj -c Release --nologo --verbosity quiet >/dev/null

# La contrasena viaja solo por stdin (no va como parametro en la linea de comandos).
HASH="$(printf '%s\n' "$NEW_PASSWORD" | docker run --rm -i --network none -v "$TMP:/work:ro" -w /work mcr.microsoft.com/dotnet/sdk:10.0 dotnet /work/bin/Release/net10.0/ResetIdentity.dll)"
unset NEW_PASSWORD
if [[ ! "$HASH" =~ ^[A-Za-z0-9+/=]+$ || ${#HASH} -lt 50 ]]; then
  echo 'ERROR: el hash generado no es valido.' >&2; exit 1
fi

printf 'Actualizando exclusivamente %s...\n' "$TEST_DATABASE"
# Base64 no puede contener comillas SQL; se usa psql dentro del contenedor db.
printf 'BEGIN;\nUPDATE "AspNetUsers" SET "PasswordHash" = '\''%s'\'', "SecurityStamp" = gen_random_uuid()::text, "ConcurrencyStamp" = gen_random_uuid()::text, "LockoutEnd" = NULL, "AccessFailedCount" = 0 WHERE "NormalizedEmail" = '\''%s'\'';\nCOMMIT;\n' "$HASH" "$ACCOUNT_EMAIL" | \
  docker exec -i "$DB_CONTAINER" sh -c 'psql -X -U "$POSTGRES_USER" -d BebesNadadores_MigracionTest -v ON_ERROR_STOP=1'
unset HASH
printf '\nListo. Solo se modifico el administrador de TEST. No se toco produccion.\n'
