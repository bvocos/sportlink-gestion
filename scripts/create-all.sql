:ON ERROR EXIT
:r .\00-create-database.sql
:r .\01-create-tables.sql
:r .\02-seed-master-data.sql
:r .\03-create-views.sql
:r .\04-fix-rentabilidad.sql
:r .\05-add-monto-entrega.sql
:r .\06-add-precios-tipo-cesped.sql
:r .\07-add-usuarios.sql
:r .\08-add-auditoria.sql
:r .\09-add-seguridad-usuarios.sql
:r .\10-add-geografia-clientes.sql

PRINT N'Base CespedVentas creada correctamente.';
GO
