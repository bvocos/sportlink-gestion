# VerdeControl — ventas de césped sintético

Solución full-stack basada en el documento `agente-cesped-sintetico.md`. Incluye API ASP.NET Core con vertical slices, SQL Server, Vue 3 + TypeScript y PWA mobile-first.

## Ejecutar

Requisitos: .NET SDK 9, Node 22+ y SQL Server en `DESKTOP-6GHJU6G`.

```powershell
cd scripts
sqlcmd -S DESKTOP-6GHJU6G -E -i create-all.sql
cd ..
dotnet run --project backend/src/Api
```

La terminal del backend debe permanecer abierta y mostrar `Now listening on: http://localhost:5114`. Recién entonces se inicia `npm run dev` en otra terminal. Si el backend no llega a ese mensaje, el error anterior en esa terminal indica el problema de SQL Server.

En otra terminal:

```powershell
cd web
npm install
npm run dev
```

Web: `http://localhost:5173`. API: `http://localhost:5114`. OpenAPI: `http://localhost:5114/openapi/v1.json`.

Los scripts de `scripts/` crean la base, todas las tablas, relaciones, índices, vistas y datos maestros. La API se conecta a `DESKTOP-6GHJU6G` mediante autenticación integrada de Windows. Conserva `EnsureCreated` como respaldo para desarrollo; si los scripts ya fueron ejecutados, utiliza el esquema existente.

## Configuración para producción

La conexión local y el origen de Vite viven exclusivamente en `appsettings.Development.json`. En producción, no guardar contraseñas en Git: configurar estos valores mediante variables de entorno o el administrador de secretos del servidor:

```powershell
$env:ConnectionStrings__Default = "Server=SERVIDOR;Database=CespedVentas;User Id=USUARIO;Password=SECRETO;Encrypt=True;TrustServerCertificate=False"
$env:AllowedOrigins__0 = "https://sistema.midominio.com"
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet run --project backend/src/Api
```

El archivo `backend/src/Api/appsettings.Production.example.json` documenta la estructura esperada y no contiene credenciales utilizables.

Si la base ya existía antes de la corrección de precisión del margen, ejecutar una vez:

```powershell
sqlcmd -S DESKTOP-6GHJU6G -E -i scripts/04-fix-rentabilidad.sql
sqlcmd -S DESKTOP-6GHJU6G -E -i scripts/05-add-monto-entrega.sql
sqlcmd -S DESKTOP-6GHJU6G -E -i scripts/06-add-precios-tipo-cesped.sql
sqlcmd -S DESKTOP-6GHJU6G -E -i scripts/07-add-usuarios.sql
sqlcmd -S DESKTOP-6GHJU6G -E -i scripts/08-add-auditoria.sql
sqlcmd -S DESKTOP-6GHJU6G -E -i scripts/09-add-seguridad-usuarios.sql

Primer acceso: usuario `admin`, contraseña `Admin123!`. El sistema exige reemplazarla por una contraseña personal antes de permitir el acceso al panel.
```

## Nota sobre .NET 10

El documento solicita .NET 10, pero el entorno de generación sólo dispone de .NET 9. El código usa APIs compatibles; al instalar SDK 10 basta cambiar `TargetFramework` a `net10.0` y alinear las referencias `Microsoft.*`.
