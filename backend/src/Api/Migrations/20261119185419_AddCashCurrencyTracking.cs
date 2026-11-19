using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCashCurrencyTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CotizacionEntrega",
                table: "Ventas",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MonedaEntrega",
                table: "Ventas",
                type: "text",
                nullable: false,
                defaultValue: "USD");

            migrationBuilder.AddColumn<decimal>(
                name: "CotizacionAplicada",
                table: "MovimientosCaja",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ImporteMonedaCobro",
                table: "MovimientosCaja",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "MonedaCobro",
                table: "MovimientosCaja",
                type: "text",
                nullable: false,
                defaultValue: "USD");

            // Todo el historial anterior fue registrado como equivalente en dólares.
            migrationBuilder.Sql("UPDATE \"MovimientosCaja\" SET \"ImporteMonedaCobro\" = \"Monto\", \"MonedaCobro\" = 'USD';");
            migrationBuilder.Sql("UPDATE \"Ventas\" SET \"MonedaEntrega\" = 'USD';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CotizacionEntrega",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "MonedaEntrega",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CotizacionAplicada",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "ImporteMonedaCobro",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "MonedaCobro",
                table: "MovimientosCaja");
        }
    }
}
