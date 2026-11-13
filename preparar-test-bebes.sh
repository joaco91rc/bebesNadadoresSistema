#!/usr/bin/env bash
set -euo pipefail
BASE=/opt/bebesnadadores
PROD_REPO="$BASE/app"
TEST_REPO="$BASE/app-test"
COMPOSE="$BASE/compose.test.yml"
cd "$BASE"

command -v docker >/dev/null
command -v python3 >/dev/null
docker inspect bebesnadadores-app >/dev/null
docker network inspect bebesnadadores_bebesnadadores >/dev/null

if [[ ! -d "$TEST_REPO/.git" && ! -f "$TEST_REPO/.git" ]]; then
  if [[ -e "$TEST_REPO" ]]; then echo 'ERROR: app-test existe y no es worktree de Git; no se modifica.' >&2; exit 1; fi
  git -C "$PROD_REPO" worktree add --detach "$TEST_REPO" HEAD
fi

# Modificación SOLO en worktree TEST. No altera app/ ni la imagen productiva.
python3 - "$TEST_REPO/BebesNadadores.Web/Services/Migracion/MigracionEjecucionService.cs" <<'PY'
from pathlib import Path
import sys
p=Path(sys.argv[1])
s=p.read_text(encoding='utf-8')
old='''            if (postgres.Host != "localhost" && postgres.Host != "127.0.0.1")
                throw new InvalidOperationException("La base PostgreSQL de TEST debe estar en localhost.");'''
new='''            var hostLocal = postgres.Host == "localhost" || postgres.Host == "127.0.0.1";
            // Excepción controlada SOLO para la copia TEST dentro de Docker.
            var dockerTest = environment.IsDevelopment() &&
                configuration.GetValue("Migracion:PermitirTestDocker", false) &&
                string.Equals(actual, "BebesNadadores_MigracionTest", StringComparison.Ordinal) &&
                (postgres.Host == "db" || postgres.Host == "bebesnadadores-db");
            if (!hostLocal && !dockerTest)
                throw new InvalidOperationException("Destino TEST no autorizado para este host PostgreSQL.");'''
if old in s:
    p.write_text(s.replace(old,new,1),encoding='utf-8')
elif new not in s:
    raise SystemExit('ERROR: cambió la validación original del migrador. No aplico parche; revisar versión.')
PY

# Genera compose.test.yml SOLO si el destino original es exactamente la base productiva.
# La cadena de conexión NO se muestra; queda en archivo root-only fuera de Git.
python3 - "$COMPOSE" <<'PY'
import json, re, subprocess, sys, os
raw=subprocess.check_output(['docker','inspect','bebesnadadores-app'],text=True)
ins=json.loads(raw)[0]
env=dict(s.split('=',1) for s in ins['Config']['Env'] if '=' in s)
conn=env.get('ConnectionStrings__DefaultConnection')
if not conn: raise SystemExit('ERROR: conexión PostgreSQL no encontrada en contenedor productivo')
pattern=r'(?i)(^|;)(\s*(?:Database|Initial Catalog)\s*=\s*)([^;]*)'
m=re.search(pattern,conn)
if not m or m.group(3).strip().strip('"') != 'bebesnadadores':
    raise SystemExit('ERROR: la base de origen no es bebesnadadores. No se genera TEST.')
h=re.search(r'(?i)(^|;)(\s*Host\s*=\s*)([^;]*)',conn)
if not h or h.group(3).strip() not in ('db','bebesnadadores-db'):
    raise SystemExit('ERROR: host PostgreSQL inesperado. Se requiere db o bebesnadadores-db.')
test=conn[:m.start(3)]+'BebesNadadores_MigracionTest'+conn[m.end(3):]
yaml='''# SOLO TEST. Puerto local, NO publica en Internet.
services:
  app-test:
    image: bebesnadadores-test:local
    container_name: bebesnadadores-app-test
    restart: 'no'
    env_file:
      - .env.production
    environment:
'''
options={
 'ConnectionStrings__DefaultConnection':test,
 'ASPNETCORE_ENVIRONMENT':'Development',
 'ASPNETCORE_URLS':'http://+:8080',
 'POSTGRES_DB':'BebesNadadores_MigracionTest',
 'Migracion__PermitirEjecucionProduccion':'false',
 'Migracion__BaseProduccionPermitida':'NO_PRODUCCION',
 'Migracion__VentanaMantenimiento':'false',
 'Migracion__PermitirEjecucionTest':'true',
 'Migracion__PermitirTestDocker':'true',
 'Migracion__PermitirCargaSql':'true',
 'Migracion__MySqlBaseTemporal':'bbn_migracion_test',
 'Migracion__PgDumpCli':'/usr/lib/postgresql/17/bin/pg_dump',
 'Migracion__CarpetaBackupsPersistente':'/backups/bebes-nadadores',
 'BackgroundServices__GeneracionCuotasHabilitada':'false',
 'BackgroundServices__RecuperosAutomaticosHabilitados':'false'
}
for k,v in options.items(): yaml+=f'      {k}: {json.dumps(v,ensure_ascii=False)}\n'
yaml+='''    ports:
      - "127.0.0.1:8092:8080"
    volumes:
      - ./migration-data-test:/app/App_Data/Migraciones
      - ./migration-backups-test:/backups/bebes-nadadores
      - bbn_test_dataprotection:/root/.aspnet/DataProtection-Keys
    networks:
      - bebesnadadores
volumes:
  bbn_test_dataprotection:
networks:
  bebesnadadores:
    external: true
    name: bebesnadadores_bebesnadadores
'''
path=sys.argv[1]
with open(path,'w',encoding='utf-8') as f: f.write(yaml)
os.chmod(path,0o600)
PY
mkdir -p "$BASE/migration-data-test" "$BASE/migration-backups-test"
chmod 700 "$BASE/migration-data-test" "$BASE/migration-backups-test"

echo 'Construyendo imagen EXCLUSIVA de pruebas (no modifica producción)...'
docker build -t bebesnadadores-test:local "$TEST_REPO"
echo 'Validando Compose TEST...'
docker compose -p bbn-test -f "$COMPOSE" config --quiet
echo 'Configuración TEST generada. Asegurá que bbn_migracion_test exista y esté vacía antes de iniciar.'
echo 'Para iniciar: cd /opt/bebesnadadores && docker compose -p bbn-test -f compose.test.yml up -d app-test'
echo 'Para entrar desde tu PC: ssh -L 8092:127.0.0.1:8092 royaltech-vps (después http://localhost:8092/administracion/migracion)'
