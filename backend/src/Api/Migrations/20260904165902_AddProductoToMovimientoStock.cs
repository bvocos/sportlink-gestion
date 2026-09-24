using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProductoToMovimientoStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // SQL Server compila todo el batch antes de ejecutar IF — separar pasos evita
            // "Invalid column name 'TipoCespedId'" al referenciar la columna recién creada.

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM dbo.TiposCesped WHERE Id = '7C100000-0000-0000-0000-FFFFFFFFFFFF')
                BEGIN
                    IF COL_LENGTH('dbo.TiposCesped', 'PrecioContadoM2') IS NOT NULL
                        INSERT dbo.TiposCesped(Id, Nombre, Descripcion, PrecioVentaM2, PrecioContadoM2, PrecioFinanciadoM2, CostoM2, ColoresJson, ControlPorLotes, Activo, CreatedAt)
                        VALUES ('7C100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (stock histórico)',
                            N'Movimientos anteriores a la gestión de stock por producto.', 0, 0, 0, 0, N'[]', 0, 0, SYSDATETIMEOFFSET());
                    ELSE
                        INSERT dbo.TiposCesped(Id, Nombre, Descripcion, PrecioVentaM2, CostoM2, ColoresJson, Activo, CreatedAt)
                        VALUES ('7C100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (stock histórico)',
                            N'Movimientos anteriores a la gestión de stock por producto.', 0, 0, N'[]', 0, SYSDATETIMEOFFSET());
                END;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NULL
                    ALTER TABLE dbo.MovimientosStock ADD TipoCespedId UNIQUEIDENTIFIER NULL;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NOT NULL
                    UPDATE dbo.MovimientosStock SET TipoCespedId = '7C100000-0000-0000-0000-FFFFFFFFFFFF'
                    WHERE TipoCespedId IS NULL;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NOT NULL
                   AND EXISTS (
                       SELECT 1 FROM sys.columns
                       WHERE object_id = OBJECT_ID(N'dbo.MovimientosStock')
                         AND name = N'TipoCespedId' AND is_nullable = 1)
                    ALTER TABLE dbo.MovimientosStock ALTER COLUMN TipoCespedId UNIQUEIDENTIFIER NOT NULL;
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.MovimientosStock')
                         AND name = N'IX_MovimientosStock_DepositoId_TipoCespedId')
                    CREATE INDEX IX_MovimientosStock_DepositoId_TipoCespedId
                        ON dbo.MovimientosStock(DepositoId, TipoCespedId);
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.indexes
                       WHERE object_id = OBJECT_ID(N'dbo.MovimientosStock')
                         AND name = N'IX_MovimientosStock_TipoCespedId')
                    CREATE INDEX IX_MovimientosStock_TipoCespedId ON dbo.MovimientosStock(TipoCespedId);
                """);

            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NOT NULL
                   AND COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NOT NULL
                   AND NOT EXISTS (
                       SELECT 1 FROM sys.foreign_keys
                       WHERE name = N'FK_MovimientosStock_TiposCesped_TipoCespedId')
                    ALTER TABLE dbo.MovimientosStock ADD CONSTRAINT FK_MovimientosStock_TiposCesped_TipoCespedId
                        FOREIGN KEY (TipoCespedId) REFERENCES dbo.TiposCesped(Id);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MovimientosStock_TiposCesped_TipoCespedId')
                    ALTER TABLE dbo.MovimientosStock DROP CONSTRAINT FK_MovimientosStock_TiposCesped_TipoCespedId;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MovimientosStock') AND name = N'IX_MovimientosStock_DepositoId_TipoCespedId')
                    DROP INDEX IX_MovimientosStock_DepositoId_TipoCespedId ON dbo.MovimientosStock;
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.MovimientosStock') AND name = N'IX_MovimientosStock_TipoCespedId')
                    DROP INDEX IX_MovimientosStock_TipoCespedId ON dbo.MovimientosStock;
                IF COL_LENGTH('dbo.MovimientosStock', 'TipoCespedId') IS NOT NULL
                    ALTER TABLE dbo.MovimientosStock DROP COLUMN TipoCespedId;
                """);
        }
    }
}
