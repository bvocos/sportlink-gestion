SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.TiposCesped', N'ControlPorLotes') IS NULL
    ALTER TABLE dbo.TiposCesped ADD ControlPorLotes BIT NOT NULL
        CONSTRAINT DF_TiposCesped_ControlPorLotes DEFAULT 0;

IF OBJECT_ID(N'dbo.LotesStock', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.LotesStock(
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_LotesStock PRIMARY KEY,
        TipoCespedId UNIQUEIDENTIFIER NOT NULL,
        DepositoId UNIQUEIDENTIFIER NOT NULL,
        Color NVARCHAR(100) NULL,
        FechaIngreso DATETIME2 NOT NULL,
        Usuario NVARCHAR(150) NOT NULL,
        Observaciones NVARCHAR(500) NULL,
        VentaId UNIQUEIDENTIFIER NULL,
        Estado NVARCHAR(450) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NULL,
        CONSTRAINT FK_LotesStock_TiposCesped_TipoCespedId FOREIGN KEY(TipoCespedId) REFERENCES dbo.TiposCesped(Id),
        CONSTRAINT FK_LotesStock_Depositos_DepositoId FOREIGN KEY(DepositoId) REFERENCES dbo.Depositos(Id),
        CONSTRAINT FK_LotesStock_Ventas_VentaId FOREIGN KEY(VentaId) REFERENCES dbo.Ventas(Id));
    CREATE INDEX IX_LotesStock_DepositoId_TipoCespedId_Estado ON dbo.LotesStock(DepositoId, TipoCespedId, Estado);
    CREATE INDEX IX_LotesStock_TipoCespedId ON dbo.LotesStock(TipoCespedId);
    CREATE INDEX IX_LotesStock_VentaId ON dbo.LotesStock(VentaId);
END;

IF OBJECT_ID(N'dbo.Rollos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Rollos(
        Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Rollos PRIMARY KEY,
        LoteStockId UNIQUEIDENTIFIER NOT NULL,
        Posicion CHAR(1) NOT NULL,
        CodigoBarra NVARCHAR(150) NOT NULL,
        CantidadM2 DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NULL,
        CONSTRAINT FK_Rollos_LotesStock_LoteStockId FOREIGN KEY(LoteStockId) REFERENCES dbo.LotesStock(Id) ON DELETE CASCADE,
        CONSTRAINT CK_Rollos_Posicion CHECK(Posicion IN ('A','B','C')),
        CONSTRAINT CK_Rollos_CantidadM2 CHECK(CantidadM2 > 0));
    CREATE UNIQUE INDEX IX_Rollos_CodigoBarra ON dbo.Rollos(CodigoBarra);
    CREATE INDEX IX_Rollos_LoteStockId ON dbo.Rollos(LoteStockId);
END;

COMMIT TRANSACTION;
GO
