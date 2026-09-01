USE [CespedVentas];
GO

IF COL_LENGTH(N'dbo.TiposCesped', N'PrecioContadoM2') IS NULL
BEGIN
    ALTER TABLE dbo.TiposCesped ADD PrecioContadoM2 DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_TiposCesped_PrecioContadoM2 DEFAULT 0 WITH VALUES;
    UPDATE dbo.TiposCesped SET PrecioContadoM2 = PrecioVentaM2;
END;
GO

IF COL_LENGTH(N'dbo.TiposCesped', N'PrecioFinanciadoM2') IS NULL
BEGIN
    ALTER TABLE dbo.TiposCesped ADD PrecioFinanciadoM2 DECIMAL(18,2) NOT NULL
        CONSTRAINT DF_TiposCesped_PrecioFinanciadoM2 DEFAULT 0 WITH VALUES;
    UPDATE dbo.TiposCesped SET PrecioFinanciadoM2 = PrecioVentaM2;
END;
GO
