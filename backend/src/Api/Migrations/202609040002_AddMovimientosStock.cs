using Api.Shared.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("202609040002_AddMovimientosStock")]
public sealed class AddMovimientosStock : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MovimientosStock(
                    Id UNIQUEIDENTIFIER NOT NULL,
                    DepositoId UNIQUEIDENTIFIER NOT NULL,
                    Tipo NVARCHAR(30) NOT NULL,
                    CantidadM2 DECIMAL(18,2) NOT NULL,
                    Fecha DATETIME2(7) NOT NULL,
                    Usuario NVARCHAR(150) NOT NULL,
                    Observaciones NVARCHAR(500) NULL,
                    VentaId UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_MovimientosStock_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
                    UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_MovimientosStock PRIMARY KEY (Id),
                    CONSTRAINT CK_MovimientosStock_CantidadM2 CHECK (CantidadM2 > 0),
                    CONSTRAINT FK_MovimientosStock_Depositos_DepositoId FOREIGN KEY (DepositoId) REFERENCES dbo.Depositos(Id),
                    CONSTRAINT FK_MovimientosStock_Ventas_VentaId FOREIGN KEY (VentaId) REFERENCES dbo.Ventas(Id));
                CREATE INDEX IX_MovimientosStock_DepositoId_Fecha ON dbo.MovimientosStock(DepositoId, Fecha DESC);
                CREATE INDEX IX_MovimientosStock_VentaId ON dbo.MovimientosStock(VentaId);
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                DROP TABLE dbo.MovimientosStock;
            """);
    }
}
