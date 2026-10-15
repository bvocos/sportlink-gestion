using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStableBranchReceiptNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroRecibo",
                table: "MovimientosCaja",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE mc
                SET NumeroRecibo = (
                    SELECT COUNT(*) FROM dbo.MovimientosCaja anteriores
                    WHERE anteriores.CuotaId IS NOT NULL AND anteriores.Tipo=N'Ingreso'
                      AND anteriores.CreatedAt <= mc.CreatedAt)
                FROM dbo.MovimientosCaja mc
                WHERE mc.CuotaId IS NOT NULL AND mc.Tipo=N'Ingreso';

                IF EXISTS (
                    SELECT 1 FROM dbo.MovimientosCaja
                    WHERE NumeroRecibo IS NOT NULL
                    GROUP BY SucursalId, NumeroRecibo HAVING COUNT(*) > 1)
                    THROW 51000, 'No se pudo crear la numeración de recibos: existen números históricos duplicados dentro de una sucursal.', 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_SucursalId_NumeroRecibo",
                table: "MovimientosCaja",
                columns: new[] { "SucursalId", "NumeroRecibo" },
                unique: true,
                filter: "[NumeroRecibo] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_SucursalId_NumeroRecibo",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "NumeroRecibo",
                table: "MovimientosCaja");
        }
    }
}
