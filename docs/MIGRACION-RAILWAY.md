# Migración de Azure a Railway (PostgreSQL)

Azure sigue funcionando durante todo el proceso. No se borra nada de Azure hasta que Railway esté validado.

## Qué cambió en el código

- Base de datos: SQL Server → **PostgreSQL** (`Npgsql.EntityFrameworkCore.PostgreSQL`).
- Las 32 migraciones históricas de SQL Server se reemplazaron por una sola: `InicialPostgres`.
- Texto con tipo `citext`: búsquedas, login y nombres únicos siguen sin distinguir mayúsculas, igual que en SQL Server.
- La carga inicial (`SeedData`) ya no usa SQL a mano. Crea sucursales base, "Sin asignar", admin, alícuotas y configuración solo si faltan.
- Las claves de la cookie de sesión se guardan en la tabla `DataProtectionKeys`, así un redeploy no desloguea a nadie.
- `UseForwardedHeaders`: la app reconoce HTTPS detrás del proxy de Railway.
- Conexión: `ConnectionStrings__Default` o, si está vacía, `DATABASE_URL` (formato de Railway).
- El contenedor escucha en `PORT` (Railway lo inyecta; por defecto 8080). Healthcheck: `/api/health` (`railway.json`).
- El CI (`.github/workflows/ci.yml`) prueba contra Postgres y ya no despliega en Azure. El despliegue lo hace Railway.
- `tools/MigrarDatos`: copia los datos de Azure SQL a Postgres y verifica la copia.

## 1. Preparar Railway (una sola vez)

1. Crear el proyecto `sportlink` en Railway, con plan Hobby o Pro.
2. Entornos: `production` (ya existe) y crear `develop`.
3. En **cada** entorno:
   - **+ New → Database → PostgreSQL.**
   - **+ New → GitHub Repo →** `bvocos/sportlink-gestion`.
     - Rama: `main` en production, `develop` en develop.
     - Activar **Wait for CI**.
   - Variables del servicio de la app:
     ```
     DATABASE_URL=${{Postgres.DATABASE_URL}}
     ASPNETCORE_ENVIRONMENT=Production
     ```
   - **Settings → Networking → Generate Domain** (más adelante se puede poner un dominio propio).

Mientras la rama `feature/migracion-railway-postgres` no esté fusionada, se puede apuntar el entorno `develop` de Railway a esa rama para probar.

En el primer arranque, la app aplica la migración `InicialPostgres` sobre la base vacía.

## 2. Copiar los datos

La herramienta **solo lee** de Azure. En Postgres **vacía las tablas destino** y copia todo dentro de una única transacción. Al terminar compara, tabla por tabla:

- la cantidad de filas,
- la suma de cada columna de importes,
- y las referencias entre tablas (claves foráneas).

Necesita:

- **Origen:** la cadena de conexión de Azure SQL (`CespedVentasDev` o `CespedVentas`). La IP desde donde se ejecuta debe estar en el firewall del servidor SQL.
- **Destino:** la variable `DATABASE_PUBLIC_URL` del Postgres de Railway (pestaña *Variables* de la base).

```powershell
cd tools/MigrarDatos
# Simulación: muestra tablas y filas, no escribe nada
dotnet run -- --origen "<cadena Azure SQL>" --destino "<DATABASE_PUBLIC_URL>"
# Copia real + verificación
dotnet run -- --origen "<cadena Azure SQL>" --destino "<DATABASE_PUBLIC_URL>" --confirmar
```

Termina con `Migración completa y verificada.` o con la lista de diferencias que encontró.

Después de copiar, **reiniciar** el servicio de la app en Railway (Redeploy). Eso aplica la normalización de permisos y deja lista la tabla de sesiones.

## 3. Orden recomendado

1. **develop:**
   1. Copiar `CespedVentasDev` a Railway develop.
   2. Probar la app de punta a punta: login, ventas, cuotas, caja, stock, presupuestos con PDF, reportes de rentabilidad.
2. **production:**
   1. Avisar una ventana corta sin uso del sistema.
   2. Copiar `CespedVentas` a Railway production y verificar.
   3. Pasar a los usuarios a la URL de Railway, o mover el DNS.
3. Fusionar `feature/migracion-railway-postgres` en `develop` y después en `main`. Desde ese momento Azure deja de recibir despliegues, pero sigue encendido con su última versión.
4. Dejar Azure unos días como respaldo y después darlo de baja: Container Apps, environment y las bases SQL, incluida `CespedVentasBackupPreDevelop20261015`.

**Volver atrás:** si hay que volver a Azure tras pasar a producción, los datos cargados en Railway después del cambio no estarán en Azure. Por eso la ventana de cambio debe ser sin uso, y la validación hay que hacerla antes.

## Desarrollo local

```powershell
docker compose up -d                  # Postgres local en el puerto 5432
cd backend/src/Api; dotnet run        # usa appsettings.Development.json
```

Tests de integración:

```powershell
$env:TEST_SQL_CONNECTION="Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres_local"
dotnet test backend/tests/Api.Tests
```
