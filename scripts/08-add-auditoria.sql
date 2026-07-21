USE [CespedVentas];
GO

IF OBJECT_ID(N'dbo.RegistrosAuditoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RegistrosAuditoria (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_RegistrosAuditoria PRIMARY KEY,
        FechaHora DATETIMEOFFSET NOT NULL,
        UsuarioId UNIQUEIDENTIFIER NULL,
        Usuario NVARCHAR(150) NOT NULL,
        Modulo NVARCHAR(100) NOT NULL,
        Accion NVARCHAR(50) NOT NULL,
        Entidad NVARCHAR(100) NOT NULL,
        EntidadId NVARCHAR(200) NOT NULL,
        DetalleJson NVARCHAR(MAX) NOT NULL
    );
    CREATE INDEX IX_RegistrosAuditoria_FechaHora ON dbo.RegistrosAuditoria(FechaHora DESC);
    CREATE INDEX IX_RegistrosAuditoria_Modulo ON dbo.RegistrosAuditoria(Modulo);
END
GO
