using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetNumberPerBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET ANSI_NULLS ON;
                SET ANSI_PADDING ON;
                SET ANSI_WARNINGS ON;
                SET ARITHABORT ON;
                SET CONCAT_NULL_YIELDS_NULL ON;
                SET QUOTED_IDENTIFIER ON;
                SET NUMERIC_ROUNDABORT OFF;

                IF COLUMNPROPERTY(OBJECT_ID(N'dbo.Presupuestos'), N'Numero', 'IsIdentity') = 1
                BEGIN
                    IF COL_LENGTH(N'dbo.Presupuestos', N'NumeroNuevo') IS NULL
                        ALTER TABLE dbo.Presupuestos ADD NumeroNuevo INT NULL;

                    EXEC(N'UPDATE dbo.Presupuestos SET NumeroNuevo = Numero WHERE NumeroNuevo IS NULL;');

                    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Presupuestos') AND name = N'IX_Presupuestos_Numero')
                        DROP INDEX IX_Presupuestos_Numero ON dbo.Presupuestos;

                    ALTER TABLE dbo.Presupuestos DROP COLUMN Numero;
                    EXEC sys.sp_rename N'dbo.Presupuestos.NumeroNuevo', N'Numero', N'COLUMN';
                    ALTER TABLE dbo.Presupuestos ALTER COLUMN Numero INT NOT NULL;
                END;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Presupuestos') AND name = N'IX_Presupuestos_Numero')
                    DROP INDEX IX_Presupuestos_Numero ON dbo.Presupuestos;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Presupuestos') AND name = N'IX_Presupuestos_SucursalId_Numero')
                    CREATE UNIQUE INDEX IX_Presupuestos_SucursalId_Numero ON dbo.Presupuestos(SucursalId, Numero);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQL Server no puede restaurar IDENTITY sin reconstruir la tabla. El rollback conserva
            // Numero como INT NOT NULL y solo vuelve al índice único global cuando los datos lo permiten.
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT Numero
                    FROM dbo.Presupuestos
                    GROUP BY Numero
                    HAVING COUNT(*) > 1)
                    THROW 50001, 'No se puede restaurar el indice global de presupuestos porque hay numeros repetidos entre sucursales.', 1;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Presupuestos') AND name = N'IX_Presupuestos_SucursalId_Numero')
                    DROP INDEX IX_Presupuestos_SucursalId_Numero ON dbo.Presupuestos;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Presupuestos') AND name = N'IX_Presupuestos_Numero')
                    CREATE UNIQUE INDEX IX_Presupuestos_Numero ON dbo.Presupuestos(Numero);
                """);
        }
    }
}
