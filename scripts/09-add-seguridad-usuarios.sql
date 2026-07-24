USE [CespedVentas];
GO

IF COL_LENGTH('dbo.Usuarios','DebeCambiarPassword') IS NULL
BEGIN
    ALTER TABLE dbo.Usuarios ADD DebeCambiarPassword BIT NOT NULL CONSTRAINT DF_Usuarios_DebeCambiarPassword DEFAULT 0;
    UPDATE dbo.Usuarios SET DebeCambiarPassword=1 WHERE NombreUsuario=N'admin';
END
GO
IF COL_LENGTH('dbo.Usuarios','IntentosFallidos') IS NULL
    ALTER TABLE dbo.Usuarios ADD IntentosFallidos INT NOT NULL CONSTRAINT DF_Usuarios_IntentosFallidos DEFAULT 0;
GO
IF COL_LENGTH('dbo.Usuarios','BloqueadoHasta') IS NULL
    ALTER TABLE dbo.Usuarios ADD BloqueadoHasta DATETIMEOFFSET(7) NULL;
GO
