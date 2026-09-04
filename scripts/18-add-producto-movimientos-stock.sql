SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @ProductoHistorico UNIQUEIDENTIFIER='7C100000-0000-0000-0000-FFFFFFFFFFFF';
IF NOT EXISTS(SELECT 1 FROM dbo.TiposCesped WHERE Id=@ProductoHistorico)
    INSERT dbo.TiposCesped(Id,Nombre,Descripcion,PrecioVentaM2,CostoM2,ColoresJson,Activo,CreatedAt)
    VALUES(@ProductoHistorico,N'Sin asignar (stock histórico)',N'Movimientos anteriores a la gestión de stock por producto.',0,0,N'[]',0,SYSDATETIMEOFFSET());

IF COL_LENGTH('dbo.MovimientosStock','TipoCespedId') IS NULL
    ALTER TABLE dbo.MovimientosStock ADD TipoCespedId UNIQUEIDENTIFIER NULL;
UPDATE dbo.MovimientosStock SET TipoCespedId=@ProductoHistorico WHERE TipoCespedId IS NULL;
ALTER TABLE dbo.MovimientosStock ALTER COLUMN TipoCespedId UNIQUEIDENTIFIER NOT NULL;

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MovimientosStock') AND name=N'IX_MovimientosStock_DepositoId_TipoCespedId')
    CREATE INDEX IX_MovimientosStock_DepositoId_TipoCespedId ON dbo.MovimientosStock(DepositoId,TipoCespedId);
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.MovimientosStock') AND name=N'IX_MovimientosStock_TipoCespedId')
    CREATE INDEX IX_MovimientosStock_TipoCespedId ON dbo.MovimientosStock(TipoCespedId);
IF NOT EXISTS(SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_MovimientosStock_TiposCesped_TipoCespedId')
    ALTER TABLE dbo.MovimientosStock ADD CONSTRAINT FK_MovimientosStock_TiposCesped_TipoCespedId FOREIGN KEY(TipoCespedId) REFERENCES dbo.TiposCesped(Id);

COMMIT TRANSACTION;
GO
