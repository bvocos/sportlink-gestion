using Api.Shared.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202609040001_AddDepositosSucursales")]
public sealed class AddDepositosSucursales : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.Depositos', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Depositos(
                    Id UNIQUEIDENTIFIER NOT NULL,
                    Nombre NVARCHAR(150) NOT NULL,
                    Activo BIT NOT NULL CONSTRAINT DF_Depositos_Activo DEFAULT 1,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Depositos_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
                    UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_Depositos PRIMARY KEY (Id));
                CREATE UNIQUE INDEX IX_Depositos_Nombre ON dbo.Depositos(Nombre);
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Nombre = N'San Francisco')
                INSERT dbo.Depositos(Id, Nombre, Activo)
                VALUES ('7a100000-0000-0000-0000-000000000001', N'San Francisco', 1);
            IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Nombre = N'Buenos Aires')
                INSERT dbo.Depositos(Id, Nombre, Activo)
                VALUES ('7a100000-0000-0000-0000-000000000002', N'Buenos Aires', 1);

            IF OBJECT_ID(N'dbo.Sucursales', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Sucursales(
                    Id UNIQUEIDENTIFIER NOT NULL,
                    Nombre NVARCHAR(150) NOT NULL,
                    DepositoPropioId UNIQUEIDENTIFIER NOT NULL,
                    PuntoVentaAfip INT NULL,
                    Activo BIT NOT NULL CONSTRAINT DF_Sucursales_Activo DEFAULT 1,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Sucursales_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
                    UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_Sucursales PRIMARY KEY (Id),
                    CONSTRAINT FK_Sucursales_Depositos_DepositoPropioId
                        FOREIGN KEY (DepositoPropioId) REFERENCES dbo.Depositos(Id));
                CREATE UNIQUE INDEX IX_Sucursales_Nombre ON dbo.Sucursales(Nombre);
                CREATE INDEX IX_Sucursales_DepositoPropioId ON dbo.Sucursales(DepositoPropioId);
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Nombre = N'San Francisco')
                INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo)
                SELECT '7b100000-0000-0000-0000-000000000001', N'San Francisco', Id, NULL, 1
                FROM dbo.Depositos WHERE Nombre = N'San Francisco';
            IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Nombre = N'Buenos Aires')
                INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo)
                SELECT '7b100000-0000-0000-0000-000000000002', N'Buenos Aires', Id, NULL, 1
                FROM dbo.Depositos WHERE Nombre = N'Buenos Aires';

            IF COL_LENGTH(N'dbo.Usuarios', N'SucursalId') IS NULL
                ALTER TABLE dbo.Usuarios ADD SucursalId UNIQUEIDENTIFIER NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'IX_Usuarios_SucursalId')
                CREATE INDEX IX_Usuarios_SucursalId ON dbo.Usuarios(SucursalId);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuarios_Sucursales_SucursalId')
                ALTER TABLE dbo.Usuarios ADD CONSTRAINT FK_Usuarios_Sucursales_SucursalId
                    FOREIGN KEY (SucursalId) REFERENCES dbo.Sucursales(Id);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuarios_Sucursales_SucursalId')
                ALTER TABLE dbo.Usuarios DROP CONSTRAINT FK_Usuarios_Sucursales_SucursalId;
            IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'IX_Usuarios_SucursalId')
                DROP INDEX IX_Usuarios_SucursalId ON dbo.Usuarios;
            IF COL_LENGTH(N'dbo.Usuarios', N'SucursalId') IS NOT NULL
                ALTER TABLE dbo.Usuarios DROP COLUMN SucursalId;
            IF OBJECT_ID(N'dbo.Sucursales', N'U') IS NOT NULL DROP TABLE dbo.Sucursales;
            IF OBJECT_ID(N'dbo.Depositos', N'U') IS NOT NULL DROP TABLE dbo.Depositos;
            """);
    }
}
