using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleBalanceCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CotizacionVenta",
                table: "Ventas",
                type: "numeric(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SeguimientoSaldo",
                table: "Ventas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "CobroVentaId",
                table: "MovimientosCaja",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CobrosVenta",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: false),
                    FechaCobro = table.Column<DateOnly>(type: "date", nullable: false),
                    ImporteUsd = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MonedaCobro = table.Column<string>(type: "text", nullable: false),
                    ImporteMonedaCobro = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Cotizacion = table.Column<decimal>(type: "numeric(18,4)", nullable: true),
                    MedioPago = table.Column<string>(type: "citext", maxLength: 100, nullable: false),
                    Observaciones = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    MovimientoCajaId = table.Column<Guid>(type: "uuid", nullable: true),
                    NumeroRecibo = table.Column<int>(type: "integer", nullable: false),
                    Anulado = table.Column<bool>(type: "boolean", nullable: false),
                    AnuladoPor = table.Column<string>(type: "citext", nullable: true),
                    AnuladoAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MotivoAnulacion = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    MovimientoAnulacionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CobrosVenta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CobrosVenta_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CobrosVenta_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_CobroVentaId",
                table: "MovimientosCaja",
                column: "CobroVentaId");

            migrationBuilder.CreateIndex(
                name: "IX_CobrosVenta_SucursalId_NumeroRecibo",
                table: "CobrosVenta",
                columns: new[] { "SucursalId", "NumeroRecibo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CobrosVenta_SucursalId_VentaId",
                table: "CobrosVenta",
                columns: new[] { "SucursalId", "VentaId" });

            migrationBuilder.CreateIndex(
                name: "IX_CobrosVenta_VentaId",
                table: "CobrosVenta",
                column: "VentaId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_CobrosVenta_CobroVentaId",
                table: "MovimientosCaja",
                column: "CobroVentaId",
                principalTable: "CobrosVenta",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_CobrosVenta_CobroVentaId",
                table: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "CobrosVenta");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_CobroVentaId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "CotizacionVenta",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SeguimientoSaldo",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "CobroVentaId",
                table: "MovimientosCaja");
        }
    }
}
