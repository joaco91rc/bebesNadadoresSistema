# Migración legacy Bebés Nadadores

Migrador MySQL viejo -> PostgreSQL actual.

## Criterio implementado

- **2019-2025:** crea bebés/responsables e **inscripción resumida por año**. No crea asistencias históricas individuales.
- **2026:** crea inscripción, turnos históricos reconstruidos, sesiones y asistencias del `historicopersonaclase`.
- **2026 pagos:** migra `pago` donde `ANIOPAGO=2026`, incluso pagos anticipados realizados en 2025. Reconstruye cuotas usando `pagado + deuda(moroso)` por mes.
- **Caja:** no se migra.
- **Aptos médicos:** no se migran porque el sistema viejo no tiene estructura equivalente fiable.
- **Recuperos/lista de espera:** por seguridad se exportan a CSV y **no se insertan automáticamente**. Los códigos legacy tienen semántica ambigua frente al modelo nuevo. El CSV conserva IDs para resolverlos después sin perder información.
- Sexo no existente en legacy: `Sexo.NoInformado`.
- Códigos de asistencia: `P=Presente`, `A=Ausente`, `R=Presente/recupero`. Otros códigos quedan como `Pendiente` + observación y salen en CSV.

## 1. Restaurar el dump viejo en MySQL

Ejemplo:

```bash
mysql -u root -p -e "CREATE DATABASE bebes_legacy CHARACTER SET latin1;"
mysql -u root -p bebes_legacy < "Backup Base.sql"
```

## 2. Probar primero contra una COPIA de PostgreSQL

```bash
dotnet run --project BebesNadadores.Migrador -- \
  --mysql "Server=localhost;Database=bebes_legacy;User ID=root;Password=CLAVE;AllowUserVariables=true" \
  --postgres "Host=localhost;Database=BebesNadadoresPrueba;Username=postgres;Password=CLAVE" \
  --desde 2019 \
  --anio-completo 2026 \
  --corte 2026-09-25 \
  --dry-run
```

`--dry-run` hace todas las lecturas/escrituras dentro de una transacción y termina con **ROLLBACK**.

Luego ejecutar sin `--dry-run` sobre una copia, validar conteos y recién después sobre la base definitiva.

## 3. Reportes generados

- `advertencias.csv`
- `asistencias_codigos_no_mapeados.csv`
- `lista_espera_recuperos_legacy_2026.csv`
- `nombres_para_revisar.csv`

## Recomendación para el corte definitivo

El backup analizado finaliza el 25/09/2026. Antes de producción sacar un dump MySQL nuevo y correr el mismo migrador con la fecha real de corte.

## Idempotencia

Las entidades importadas usan GUID determinísticos basados en IDs legacy. Repetir la migración no genera duplicados para esas entidades. Para bebés existentes cargados manualmente, primero intenta reutilizar coincidencia exacta de nombre+apellido+fecha de nacimiento.

> Para el dump actual (25/09/2026) use `--corte 2026-09-25`. Para el corte final, use la fecha del nuevo dump. No use una fecha posterior al backup porque el sistema viejo precarga clases futuras.
