USE [CespedVentas];
GO

IF COL_LENGTH('dbo.Clientes','ProvinciaId') IS NULL
    ALTER TABLE dbo.Clientes ADD ProvinciaId NVARCHAR(20) NULL;
GO
IF COL_LENGTH('dbo.Clientes','LocalidadId') IS NULL
    ALTER TABLE dbo.Clientes ADD LocalidadId NVARCHAR(20) NULL;
GO
