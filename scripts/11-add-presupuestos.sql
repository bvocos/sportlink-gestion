IF OBJECT_ID(N'dbo.Presupuestos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Presupuestos(
        Id UNIQUEIDENTIFIER NOT NULL, Numero INT IDENTITY(1,1) NOT NULL, ClienteId UNIQUEIDENTIFIER NOT NULL,
        Fecha DATE NOT NULL, ValidezHasta DATE NOT NULL, Estado NVARCHAR(30) NOT NULL,
        DescuentoContadoPorcentaje DECIMAL(18,2) NOT NULL, IvaContadoPorcentaje DECIMAL(18,2) NOT NULL,
        IvaFinanciadoPorcentaje DECIMAL(18,2) NOT NULL, EntregaFinanciada DECIMAL(18,2) NOT NULL,
        Observaciones NVARCHAR(2000) NULL,
        CreatedAt DATETIMEOFFSET(7) NOT NULL, UpdatedAt DATETIMEOFFSET(7) NULL,
        CONSTRAINT PK_Presupuestos PRIMARY KEY(Id), CONSTRAINT FK_Presupuestos_Clientes FOREIGN KEY(ClienteId) REFERENCES dbo.Clientes(Id));
    CREATE UNIQUE INDEX IX_Presupuestos_Numero ON dbo.Presupuestos(Numero); CREATE INDEX IX_Presupuestos_Fecha ON dbo.Presupuestos(Fecha);
    CREATE TABLE dbo.PresupuestoLineas(
        Id UNIQUEIDENTIFIER NOT NULL, PresupuestoId UNIQUEIDENTIFIER NOT NULL, TipoCespedId UNIQUEIDENTIFIER NULL,
        Producto NVARCHAR(200) NOT NULL, Descripcion NVARCHAR(2000) NULL, DescripcionPresupuesto NVARCHAR(MAX) NULL,
        EspecificacionesPresupuesto NVARCHAR(MAX) NULL, FichaTecnicaUrl NVARCHAR(1000) NULL, Color NVARCHAR(100) NULL,
        CantidadM2 DECIMAL(18,2) NOT NULL, PrecioContadoM2 DECIMAL(18,2) NOT NULL, PrecioFinanciadoM2 DECIMAL(18,2) NOT NULL,
        TotalContado DECIMAL(18,2) NOT NULL, TotalFinanciado DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIMEOFFSET(7) NOT NULL, UpdatedAt DATETIMEOFFSET(7) NULL,
        CONSTRAINT PK_PresupuestoLineas PRIMARY KEY(Id), CONSTRAINT FK_PresupuestoLineas_Presupuestos FOREIGN KEY(PresupuestoId) REFERENCES dbo.Presupuestos(Id) ON DELETE CASCADE);
END;
