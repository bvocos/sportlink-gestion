USE [CespedVentas];
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.AlicuotasIva WHERE Nombre = N'IVA 21%')
    INSERT dbo.AlicuotasIva (Id, Nombre, Porcentaje, CreatedAt) VALUES (NEWID(), N'IVA 21%', 21.00, SYSDATETIMEOFFSET());
IF NOT EXISTS (SELECT 1 FROM dbo.AlicuotasIva WHERE Nombre = N'IVA 10,5%')
    INSERT dbo.AlicuotasIva (Id, Nombre, Porcentaje, CreatedAt) VALUES (NEWID(), N'IVA 10,5%', 10.50, SYSDATETIMEOFFSET());
IF NOT EXISTS (SELECT 1 FROM dbo.AlicuotasIva WHERE Nombre = N'IVA 5%')
    INSERT dbo.AlicuotasIva (Id, Nombre, Porcentaje, CreatedAt) VALUES (NEWID(), N'IVA 5%', 5.00, SYSDATETIMEOFFSET());
IF NOT EXISTS (SELECT 1 FROM dbo.AlicuotasIva WHERE Nombre = N'Exento')
    INSERT dbo.AlicuotasIva (Id, Nombre, Porcentaje, CreatedAt) VALUES (NEWID(), N'Exento', 0.00, SYSDATETIMEOFFSET());

IF NOT EXISTS (SELECT 1 FROM dbo.TiposCesped WHERE Nombre = N'Decorativo 20 mm')
    INSERT dbo.TiposCesped (Id, Nombre, Activo, CreatedAt) VALUES (NEWID(), N'Decorativo 20 mm', 1, SYSDATETIMEOFFSET());
IF NOT EXISTS (SELECT 1 FROM dbo.TiposCesped WHERE Nombre = N'Premium 35 mm')
    INSERT dbo.TiposCesped (Id, Nombre, Activo, CreatedAt) VALUES (NEWID(), N'Premium 35 mm', 1, SYSDATETIMEOFFSET());
IF NOT EXISTS (SELECT 1 FROM dbo.TiposCesped WHERE Nombre = N'Deportivo 50 mm')
    INSERT dbo.TiposCesped (Id, Nombre, Activo, CreatedAt) VALUES (NEWID(), N'Deportivo 50 mm', 1, SYSDATETIMEOFFSET());

IF NOT EXISTS (SELECT 1 FROM dbo.Configuraciones WHERE Clave = N'UmbralMuyRentable')
    INSERT dbo.Configuraciones (Id, Clave, ValorDecimal, CreatedAt) VALUES (NEWID(), N'UmbralMuyRentable', 0.30, SYSDATETIMEOFFSET());

COMMIT TRANSACTION;
GO
