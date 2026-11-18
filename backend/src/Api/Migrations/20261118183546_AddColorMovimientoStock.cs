using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddColorMovimientoStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "MovimientosStock",
                type: "citext",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId_ImportacionId_Color",
                table: "MovimientosStock",
                columns: new[] { "DepositoId", "TipoCespedId", "ImportacionId", "Color" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId_ImportacionId_Color",
                table: "MovimientosStock");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "MovimientosStock");
        }
    }
}
