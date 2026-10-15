using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSucursalFiscalData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CondicionIva",
                table: "Sucursales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Cuit",
                table: "Sucursales",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DomicilioFiscal",
                table: "Sucursales",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Sucursales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngresosBrutos",
                table: "Sucursales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "InicioActividades",
                table: "Sucursales",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Localidad",
                table: "Sucursales",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "Sucursales",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telefono",
                table: "Sucursales",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_Cuit_PuntoVentaAfip",
                table: "Sucursales",
                columns: new[] { "Cuit", "PuntoVentaAfip" },
                unique: true,
                filter: "[Cuit] IS NOT NULL AND [PuntoVentaAfip] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sucursales_Cuit_PuntoVentaAfip",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "CondicionIva",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Cuit",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "DomicilioFiscal",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "IngresosBrutos",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "InicioActividades",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Localidad",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "Sucursales");

            migrationBuilder.DropColumn(
                name: "Telefono",
                table: "Sucursales");
        }
    }
}
