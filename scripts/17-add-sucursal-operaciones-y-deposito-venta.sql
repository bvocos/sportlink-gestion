SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @DepositoHistorico UNIQUEIDENTIFIER = '7A100000-0000-0000-0000-FFFFFFFFFFFF';
DECLARE @SucursalHistorica UNIQUEIDENTIFIER = '7B100000-0000-0000-0000-FFFFFFFFFFFF';

IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Id = @DepositoHistorico)
    INSERT dbo.Depositos(Id, Nombre, Activo, CreatedAt)
    VALUES (@DepositoHistorico, N'Sin asignar (datos históricos)', 0, SYSDATETIMEOFFSET());

IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Id = @SucursalHistorica)
    INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo, CreatedAt)
    VALUES (@SucursalHistorica, N'Sin asignar (datos históricos)', @DepositoHistorico, NULL, 0, SYSDATETIMEOFFSET());

IF COL_LENGTH(N'dbo.Ventas', N'SucursalId') IS NULL ALTER TABLE dbo.Ventas ADD SucursalId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Ventas', N'DepositoId') IS NULL ALTER TABLE dbo.Ventas ADD DepositoId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Cuotas', N'SucursalId') IS NULL ALTER TABLE dbo.Cuotas ADD SucursalId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.MovimientosCaja', N'SucursalId') IS NULL ALTER TABLE dbo.MovimientosCaja ADD SucursalId UNIQUEIDENTIFIER NULL;

UPDATE dbo.Ventas SET SucursalId = @SucursalHistorica WHERE SucursalId IS NULL;
UPDATE dbo.Ventas SET DepositoId = @DepositoHistorico WHERE DepositoId IS NULL;
UPDATE dbo.Cuotas SET SucursalId = @SucursalHistorica WHERE SucursalId IS NULL;
UPDATE dbo.MovimientosCaja SET SucursalId = @SucursalHistorica WHERE SucursalId IS NULL;

ALTER TABLE dbo.Ventas ALTER COLUMN SucursalId UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.Ventas ALTER COLUMN DepositoId UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.Cuotas ALTER COLUMN SucursalId UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.MovimientosCaja ALTER COLUMN SucursalId UNIQUEIDENTIFIER NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Ventas') AND name=N'IX_Ventas_DepositoId') CREATE INDEX IX_Ventas_DepositoId ON dbo.Ventas(DepositoId);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Ventas') AND name=N'IX_Ventas_SucursalId_FechaVenta') CREATE INDEX IX_Ventas_SucursalId_FechaVenta ON dbo.Ventas(SucursalId, FechaVenta);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Cuotas') AND name=N'IX_Cuotas_SucursalId_FechaVencimiento') CREATE INDEX IX_Cuotas_SucursalId_FechaVencimiento ON dbo.Cuotas(SucursalId, FechaVencimiento);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MovimientosCaja') AND name=N'IX_MovimientosCaja_SucursalId_Fecha') CREATE INDEX IX_MovimientosCaja_SucursalId_Fecha ON dbo.MovimientosCaja(SucursalId, Fecha);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ventas_Sucursales_SucursalId') ALTER TABLE dbo.Ventas ADD CONSTRAINT FK_Ventas_Sucursales_SucursalId FOREIGN KEY(SucursalId) REFERENCES dbo.Sucursales(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Ventas_Depositos_DepositoId') ALTER TABLE dbo.Ventas ADD CONSTRAINT FK_Ventas_Depositos_DepositoId FOREIGN KEY(DepositoId) REFERENCES dbo.Depositos(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Cuotas_Sucursales_SucursalId') ALTER TABLE dbo.Cuotas ADD CONSTRAINT FK_Cuotas_Sucursales_SucursalId FOREIGN KEY(SucursalId) REFERENCES dbo.Sucursales(Id);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_MovimientosCaja_Sucursales_SucursalId') ALTER TABLE dbo.MovimientosCaja ADD CONSTRAINT FK_MovimientosCaja_Sucursales_SucursalId FOREIGN KEY(SucursalId) REFERENCES dbo.Sucursales(Id);

COMMIT TRANSACTION;
GO

PRINT N'Datos operativos aislados por sucursal y ventas vinculadas a depósito.';
GO
