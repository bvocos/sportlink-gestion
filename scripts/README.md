# Scripts de SQL Server

Ejecutar desde esta carpeta usando autenticación integrada de Windows:

```powershell
sqlcmd -S DESKTOP-6GHJU6G -E -i create-all.sql
```

También se pueden abrir en SQL Server Management Studio y ejecutar en este orden:

1. `00-create-database.sql`
2. `01-create-tables.sql`
3. `02-seed-master-data.sql`
4. `03-create-views.sql`

`create-all.sql` utiliza comandos de modo SQLCMD (`:r`). Para usarlo en SSMS se debe activar **Query > SQLCMD Mode**.

Los scripts de maestros son idempotentes. El script de tablas está pensado para una base nueva y falla intencionalmente si las tablas ya existen.
