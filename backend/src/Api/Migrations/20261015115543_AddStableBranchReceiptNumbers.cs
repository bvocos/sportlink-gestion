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
                ;WITH RecibosOrdenados AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY SucursalId
                               ORDER BY CreatedAt, Id) AS Numero
                    FROM dbo.MovimientosCaja
                    WHERE CuotaId IS NOT NULL AND Tipo=N'Ingreso'
                )
                UPDATE mc
                SET NumeroRecibo = CONVERT(INT, ordenados.Numero)
                FROM dbo.MovimientosCaja mc
                INNER JOIN RecibosOrdenados ordenados ON ordenados.Id = mc.Id;
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
