IF COL_LENGTH(N'dbo.TiposCesped', N'DescripcionPresupuesto') IS NULL
    ALTER TABLE dbo.TiposCesped ADD DescripcionPresupuesto NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.TiposCesped', N'EspecificacionesPresupuesto') IS NULL
    ALTER TABLE dbo.TiposCesped ADD EspecificacionesPresupuesto NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.TiposCesped', N'FichaTecnicaUrl') IS NULL
    ALTER TABLE dbo.TiposCesped ADD FichaTecnicaUrl NVARCHAR(1000) NULL;

IF COL_LENGTH(N'dbo.PresupuestoLineas', N'DescripcionPresupuesto') IS NULL
    ALTER TABLE dbo.PresupuestoLineas ADD DescripcionPresupuesto NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.PresupuestoLineas', N'EspecificacionesPresupuesto') IS NULL
    ALTER TABLE dbo.PresupuestoLineas ADD EspecificacionesPresupuesto NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'dbo.PresupuestoLineas', N'FichaTecnicaUrl') IS NULL
    ALTER TABLE dbo.PresupuestoLineas ADD FichaTecnicaUrl NVARCHAR(1000) NULL;
GO
