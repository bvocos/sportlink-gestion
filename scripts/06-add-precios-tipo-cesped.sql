USE [CespedVentas];
GO

IF COL_LENGTH(N'dbo.TiposCesped', N'PrecioVentaM2') IS NULL
    ALTER TABLE dbo.TiposCesped ADD PrecioVentaM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_TiposCesped_PrecioVentaM2 DEFAULT 0 WITH VALUES;
GO
IF COL_LENGTH(N'dbo.TiposCesped', N'CostoM2') IS NULL
    ALTER TABLE dbo.TiposCesped ADD CostoM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_TiposCesped_CostoM2 DEFAULT 0 WITH VALUES;
GO
