# Migración Bebés Nadadores — histórico independiente + operatoria 2026

## Alcance

- **2019–2025:** `HistorialAlumnosLegacy` y `HistorialAlumnosAniosLegacy`. Una fila por alumno y otra por año con actividad. No se crean bebés operativos por el solo hecho de haber asistido antes de 2026. Se conserva fecha de primera inscripción cuando existe y años, primeras/últimas clases, nivel(es) y frecuencia estimada.
- **2026:** se cargan entidades operativas (`Bebes`, `Responsables`, `Inscripciones`, `Turnos`, `SesionesClase`, `Asistencias`, `Cuotas`, `Pagos`, `ImputacionesPago`) utilizando la lógica del migrador. 
- **Vínculos:** archivo histórico `BebeId` opcional, enlazado al legajo cuando hay alumno de 2026 o coincidencia única de nombre+fecha de nacimiento. No se genera una ficha operativa para quienes solo fueron alumnos antes de 2026.
- **Consultas:** `/clientes/historial` y panel `Trayectoria histórica` en `/clientes/{id}`.
- **Recuperos/lista de espera 2026:** se exportan al CSV `lista_espera_recuperos_legacy_2026.csv`, no se cargan automáticamente porque sus códigos necesitan homologación con el personal del natatorio. No suponer que ya están migrados.
- **Caja y aptos médicos antiguos:** no se importan.

## Archivos usados

`BebesNadadoresBase.zip` incluye el MySQL completo al **2 de noviembre de 2026**, no al 7 de noviembre. La importación usa **una base MySQL restaurada** a partir de ese dump; no lee ZIP o SQL directamente. Si siguieron usando el sistema viejo después de esa fecha, sacá otro dump al hacer el corte final.

## Ejecución segura

1. Crear **una copia de prueba** de PostgreSQL, nunca empezar por producción.
2. Restaurar MySQL: `mysql -u root -p -e "CREATE DATABASE bbn2 CHARACTER SET utf8 COLLATE utf8_general_ci"` y `mysql -u root -p bbn2 < BebesNadadoresBase.sql` (ajustar nombre si ya existe, y no sobrescribir una base de trabajo).
3. Confirmar que PostgreSQL de prueba esté al día con las migraciones: `dotnet ef database update --project BebesNadadores.Infrastructure --startup-project BebesNadadores.Web --connection "..."` o iniciar la Web **solo con conexión a la base de prueba**, ya que `Program.cs` aplica migraciones al iniciar.
4. Ejecutar el migrador desde la raíz de la solución:

```bash
dotnet run --project BebesNadadores.Migrador --   --mysql "Server=localhost;Database=bbn2;User ID=root;Password=CLAVE"   --postgres "Host=localhost;Database=BebesPrueba;Username=postgres;Password=CLAVE"   --desde 2019 --anio-completo 2026 --corte 2026-11-02 --dry-run
```

5. Revisar el informe CSV en `migration-report` y comparar conteos y montos del origen/destino, incluyendo **anticipos imputados a 2026**, deuda y pagos parciales. No ejecutar sin `--dry-run` hasta que los responsables validen estas conciliaciones y los códigos de asistencia.
6. Una vez verificado, quitar `--dry-run` **solo contra la base de prueba**. Repetir validaciones y crear backups/snapshot de producción antes de planificar el corte.

## Precauciones

- `--dry-run` inicia una transacción y hace rollback, pero requiere todas las tablas creadas y permisos de escritura en la base de prueba.
- Los IDs derivados del sistema anterior son deterministas y el importador intenta reutilizar registros existentes. Sin embargo, no garantiza evitar todas las duplicaciones cuando hay fichas o pagos creados manualmente sin referencia legacy. Revisar el CSV y comparar antes de confirmar.
- `P` = presente, `A` = ausente; los demás códigos no inequívocos se informan en CSV. `R` se interpreta como recupero presente en el migrador heredado y requiere revisión funcional antes de ejecutar contra producción.
- El historial anual es **actividad registrada**, no prueba documental de inscripción continua durante todos los meses del año.
- El proyecto adjunto contiene la versión del código que se compartió previamente para el migrador, no cambios posteriores de un repositorio GitHub. Fusionar con cuidado en la rama actual.
- El paquete no incluye credenciales, bases de datos ni el dump MySQL; conservalos en un sitio seguro.

### Asistencias posteriores al último archivo

El backup puede contener clases recientes en `clase` / `presenteausente` que todavía no estén en `historicopersonaclase`. Este paquete **no garantiza importar el tramo pendiente**, porque `PRESENTE='A'` se precarga antes de la clase. Se emite advertencia `ASISTENCIA_2026/TRAMO_SIN_ARCHIVAR` para control manual; antes del pase definitivo hay que completar la conciliación de ese tramo con un criterio confirmado por el natatorio.
