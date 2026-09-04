using System;
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
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM dbo.TiposCesped WHERE Id = '7C100000-0000-0000-0000-FFFFFFFFFFFF')
                    INSERT dbo.TiposCesped(Id, Nombre, Descripcion, PrecioVentaM2, CostoM2, ColoresJson, Activo, CreatedAt)
                    VALUES ('7C100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (stock histórico)',
                        N'Movimientos anteriores a la gestión de stock por producto.', 0, 0, N'[]', 0, SYSDATETIMEOFFSET());
                """);
            migrationBuilder.AddColumn<Guid?>(
                name: "TipoCespedId",
                table: "MovimientosStock",
                type: "uniqueidentifier",
                nullable: true);
            migrationBuilder.Sql("""
                UPDATE dbo.MovimientosStock SET TipoCespedId = '7C100000-0000-0000-0000-FFFFFFFFFFFF'
                WHERE TipoCespedId IS NULL;
                """);
            migrationBuilder.AlterColumn<Guid>(name: "TipoCespedId", table: "MovimientosStock",
                type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid),
                oldType: "uniqueidentifier", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId",
                table: "MovimientosStock",
                columns: new[] { "DepositoId", "TipoCespedId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_TipoCespedId",
                table: "MovimientosStock",
                column: "TipoCespedId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosStock_TiposCesped_TipoCespedId",
                table: "MovimientosStock",
                column: "TipoCespedId",
                principalTable: "TiposCesped",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosStock_TiposCesped_TipoCespedId",
                table: "MovimientosStock");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId",
                table: "MovimientosStock");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosStock_TipoCespedId",
                table: "MovimientosStock");

            migrationBuilder.DropColumn(
                name: "TipoCespedId",
                table: "MovimientosStock");
        }
    }
}
