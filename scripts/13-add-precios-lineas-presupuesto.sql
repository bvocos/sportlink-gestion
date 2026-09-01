USE [CespedVentas];
GO
IF COL_LENGTH(N'dbo.PresupuestoLineas', N'PrecioContadoM2') IS NULL
BEGIN
    ALTER TABLE dbo.PresupuestoLineas ADD PrecioContadoM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_PrecioContadoM2 DEFAULT 0 WITH VALUES;
    ALTER TABLE dbo.PresupuestoLineas ADD PrecioFinanciadoM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_PrecioFinanciadoM2 DEFAULT 0 WITH VALUES;
    ALTER TABLE dbo.PresupuestoLineas ADD TotalContado DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_TotalContado DEFAULT 0 WITH VALUES;
    ALTER TABLE dbo.PresupuestoLineas ADD TotalFinanciado DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_TotalFinanciado DEFAULT 0 WITH VALUES;
    IF COL_LENGTH(N'dbo.PresupuestoLineas', N'PrecioVentaM2') IS NOT NULL
        EXEC(N'UPDATE dbo.PresupuestoLineas SET PrecioContadoM2=PrecioVentaM2, PrecioFinanciadoM2=PrecioVentaM2, TotalContado=Total, TotalFinanciado=Total');
END;
GO
IF COL_LENGTH(N'dbo.PresupuestoLineas', N'PrecioVentaM2') IS NOT NULL
    ALTER TABLE dbo.PresupuestoLineas DROP COLUMN PrecioVentaM2;
GO
IF COL_LENGTH(N'dbo.PresupuestoLineas', N'Total') IS NOT NULL
    ALTER TABLE dbo.PresupuestoLineas DROP COLUMN Total;
GO
