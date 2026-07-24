USE [CespedVentas];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

CREATE TABLE dbo.Clientes
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Clientes_Id DEFAULT NEWSEQUENTIALID(),
    Nombre NVARCHAR(100) NOT NULL,
    Apellido NVARCHAR(100) NOT NULL,
    Telefono NVARCHAR(50) NOT NULL,
    Correo NVARCHAR(254) NULL,
    Localidad NVARCHAR(100) NOT NULL,
    Provincia NVARCHAR(100) NOT NULL,
    LocalidadId NVARCHAR(20) NULL,
    ProvinciaId NVARCHAR(20) NULL,
    Tipo NVARCHAR(30) NOT NULL,
    FechaPrimerContacto DATE NOT NULL,
    Observaciones NVARCHAR(2000) NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Clientes_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_Clientes PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CK_Clientes_Tipo CHECK (Tipo IN (N'Club', N'Particular', N'Empresa', N'Constructor', N'Revendedor', N'Otro'))
);

CREATE TABLE dbo.TiposCesped
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_TiposCesped_Id DEFAULT NEWSEQUENTIALID(),
    Nombre NVARCHAR(150) NOT NULL,
    Descripcion NVARCHAR(1000) NULL,
    PrecioVentaM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_TiposCesped_PrecioVentaM2 DEFAULT 0,
    CostoM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_TiposCesped_CostoM2 DEFAULT 0,
    Activo BIT NOT NULL CONSTRAINT DF_TiposCesped_Activo DEFAULT 1,
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_TiposCesped_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_TiposCesped PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_TiposCesped_Nombre UNIQUE (Nombre)
);

CREATE TABLE dbo.AlicuotasIva
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AlicuotasIva_Id DEFAULT NEWSEQUENTIALID(),
    Nombre NVARCHAR(100) NOT NULL,
    Porcentaje DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_AlicuotasIva_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_AlicuotasIva PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_AlicuotasIva_Nombre UNIQUE (Nombre),
    CONSTRAINT CK_AlicuotasIva_Porcentaje CHECK (Porcentaje >= 0 AND Porcentaje <= 100)
);

CREATE TABLE dbo.Configuraciones
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Configuraciones_Id DEFAULT NEWSEQUENTIALID(),
    Clave NVARCHAR(100) NOT NULL,
    ValorDecimal DECIMAL(18,2) NOT NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Configuraciones_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_Configuraciones PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Configuraciones_Clave UNIQUE (Clave)
);

CREATE TABLE dbo.Ventas
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Ventas_Id DEFAULT NEWSEQUENTIALID(),
    ClienteId UNIQUEIDENTIFIER NOT NULL,
    TipoCespedId UNIQUEIDENTIFIER NOT NULL,
    AlicuotaIvaId UNIQUEIDENTIFIER NOT NULL,
    FechaVenta DATE NOT NULL,
    FechaEntregaEstimada DATE NULL,
    CantidadM2 DECIMAL(18,2) NOT NULL,
    PrecioUnitario DECIMAL(18,2) NOT NULL,
    PrecioTotal DECIMAL(18,2) NOT NULL,
    MontoEntrega DECIMAL(18,2) NOT NULL,
    CostoCompraUnitario DECIMAL(18,2) NOT NULL,
    CostoCompraTotal DECIMAL(18,2) NOT NULL,
    CostoEnvio DECIMAL(18,2) NOT NULL,
    OtrosCostos DECIMAL(18,2) NOT NULL,
    Iva DECIMAL(18,2) NOT NULL,
    GananciaBruta DECIMAL(18,2) NOT NULL,
    GananciaNeta DECIMAL(18,2) NOT NULL,
    Margen DECIMAL(18,6) NOT NULL,
    FormaPago NVARCHAR(30) NOT NULL,
    CantidadCuotas INT NULL,
    Estado NVARCHAR(30) NOT NULL,
    Observaciones NVARCHAR(2000) NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Ventas_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_Ventas PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Ventas_Clientes FOREIGN KEY (ClienteId) REFERENCES dbo.Clientes(Id),
    CONSTRAINT FK_Ventas_TiposCesped FOREIGN KEY (TipoCespedId) REFERENCES dbo.TiposCesped(Id),
    CONSTRAINT FK_Ventas_AlicuotasIva FOREIGN KEY (AlicuotaIvaId) REFERENCES dbo.AlicuotasIva(Id),
    CONSTRAINT CK_Ventas_CantidadM2 CHECK (CantidadM2 > 0),
    CONSTRAINT CK_Ventas_PrecioUnitario CHECK (PrecioUnitario > 0),
    CONSTRAINT CK_Ventas_MontoEntrega CHECK (MontoEntrega > 0 AND MontoEntrega <= PrecioTotal),
    CONSTRAINT CK_Ventas_Costos CHECK (CostoCompraUnitario >= 0 AND CostoCompraTotal >= 0 AND CostoEnvio >= 0 AND OtrosCostos >= 0),
    CONSTRAINT CK_Ventas_FormaPago CHECK (FormaPago IN (N'Cuotas', N'Contado', N'Transferencia', N'Cheque', N'Otros')),
    CONSTRAINT CK_Ventas_Estado CHECK (Estado IN (N'Confirmada', N'Futura', N'Entregada', N'Cancelada')),
    CONSTRAINT CK_Ventas_Cuotas CHECK ((FormaPago = N'Cuotas' AND CantidadCuotas BETWEEN 1 AND 60) OR (FormaPago <> N'Cuotas' AND CantidadCuotas IS NULL)),
    CONSTRAINT CK_Ventas_FechaFutura CHECK (Estado <> N'Futura' OR FechaEntregaEstimada IS NOT NULL)
);

CREATE TABLE dbo.Cuotas
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Cuotas_Id DEFAULT NEWSEQUENTIALID(),
    VentaId UNIQUEIDENTIFIER NOT NULL,
    ClienteId UNIQUEIDENTIFIER NOT NULL,
    Numero INT NOT NULL,
    FechaVencimiento DATE NOT NULL,
    FechaPago DATE NULL,
    ImportePactado DECIMAL(18,2) NOT NULL,
    ImportePagado DECIMAL(18,2) NOT NULL CONSTRAINT DF_Cuotas_ImportePagado DEFAULT 0,
    MedioPago NVARCHAR(100) NULL,
    Estado NVARCHAR(30) NOT NULL CONSTRAINT DF_Cuotas_Estado DEFAULT N'Pendiente',
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Cuotas_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_Cuotas PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_Cuotas_Ventas FOREIGN KEY (VentaId) REFERENCES dbo.Ventas(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Cuotas_Clientes FOREIGN KEY (ClienteId) REFERENCES dbo.Clientes(Id),
    CONSTRAINT UQ_Cuotas_Venta_Numero UNIQUE (VentaId, Numero),
    CONSTRAINT CK_Cuotas_Numero CHECK (Numero > 0),
    CONSTRAINT CK_Cuotas_Importes CHECK (ImportePactado > 0 AND ImportePagado >= 0),
    CONSTRAINT CK_Cuotas_Estado CHECK (Estado IN (N'Pendiente', N'Pagada', N'PagadaParcial', N'Vencida'))
);

CREATE TABLE dbo.MovimientosCaja
(
    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_MovimientosCaja_Id DEFAULT NEWSEQUENTIALID(),
    Tipo NVARCHAR(20) NOT NULL,
    Fecha DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_MovimientosCaja_Fecha DEFAULT SYSDATETIMEOFFSET(),
    Monto DECIMAL(18,2) NOT NULL,
    Concepto NVARCHAR(500) NOT NULL,
    Usuario NVARCHAR(150) NOT NULL CONSTRAINT DF_MovimientosCaja_Usuario DEFAULT N'sistema',
    VentaId UNIQUEIDENTIFIER NULL,
    CuotaId UNIQUEIDENTIFIER NULL,
    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_MovimientosCaja_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
    UpdatedAt DATETIMEOFFSET(7) NULL,
    CONSTRAINT PK_MovimientosCaja PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_MovimientosCaja_Ventas FOREIGN KEY (VentaId) REFERENCES dbo.Ventas(Id),
    CONSTRAINT FK_MovimientosCaja_Cuotas FOREIGN KEY (CuotaId) REFERENCES dbo.Cuotas(Id),
    CONSTRAINT CK_MovimientosCaja_Tipo CHECK (Tipo IN (N'Ingreso', N'Retiro')),
    CONSTRAINT CK_MovimientosCaja_Monto CHECK (Monto > 0)
);

CREATE INDEX IX_Clientes_Apellido_Nombre ON dbo.Clientes (Apellido, Nombre);
CREATE INDEX IX_Clientes_Telefono ON dbo.Clientes (Telefono);
CREATE INDEX IX_Ventas_FechaVenta ON dbo.Ventas (FechaVenta DESC);
CREATE INDEX IX_Ventas_ClienteId ON dbo.Ventas (ClienteId);
CREATE INDEX IX_Ventas_Estado_FechaEntrega ON dbo.Ventas (Estado, FechaEntregaEstimada);
CREATE INDEX IX_Cuotas_Estado_FechaVencimiento ON dbo.Cuotas (Estado, FechaVencimiento);
CREATE INDEX IX_Cuotas_ClienteId ON dbo.Cuotas (ClienteId);
CREATE INDEX IX_MovimientosCaja_Fecha ON dbo.MovimientosCaja (Fecha DESC);
CREATE INDEX IX_MovimientosCaja_VentaId ON dbo.MovimientosCaja (VentaId) WHERE VentaId IS NOT NULL;
CREATE INDEX IX_MovimientosCaja_CuotaId ON dbo.MovimientosCaja (CuotaId) WHERE CuotaId IS NOT NULL;

COMMIT TRANSACTION;
GO
