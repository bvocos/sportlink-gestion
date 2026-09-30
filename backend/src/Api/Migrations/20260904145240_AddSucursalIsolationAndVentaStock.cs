using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSucursalIsolationAndVentaStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Id = '7A100000-0000-0000-0000-FFFFFFFFFFFF')
                    INSERT dbo.Depositos(Id, Nombre, Activo, CreatedAt)
                    VALUES ('7A100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (datos históricos)', 0, SYSDATETIMEOFFSET());
                IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Id = '7B100000-0000-0000-0000-FFFFFFFFFFFF')
                    INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo, CreatedAt)
                    VALUES ('7B100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (datos históricos)',
                        '7A100000-0000-0000-0000-FFFFFFFFFFFF', NULL, 0, SYSDATETIMEOFFSET());
                """);

            migrationBuilder.AddColumn<Guid?>(
                name: "DepositoId",
                table: "Ventas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid?>(
                name: "SucursalId",
                table: "Ventas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid?>(
                name: "SucursalId",
                table: "MovimientosCaja",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid?>(
                name: "SucursalId",
                table: "Cuotas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE dbo.Ventas SET SucursalId = '7B100000-0000-0000-0000-FFFFFFFFFFFF',
                    DepositoId = '7A100000-0000-0000-0000-FFFFFFFFFFFF' WHERE SucursalId IS NULL OR DepositoId IS NULL;
                UPDATE dbo.Cuotas SET SucursalId = '7B100000-0000-0000-0000-FFFFFFFFFFFF' WHERE SucursalId IS NULL;
                UPDATE dbo.MovimientosCaja SET SucursalId = '7B100000-0000-0000-0000-FFFFFFFFFFFF' WHERE SucursalId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(name: "DepositoId", table: "Ventas", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "SucursalId", table: "Ventas", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "SucursalId", table: "MovimientosCaja", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);
            migrationBuilder.AlterColumn<Guid>(name: "SucursalId", table: "Cuotas", type: "uniqueidentifier", nullable: false, oldClrType: typeof(Guid), oldType: "uniqueidentifier", oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_DepositoId",
                table: "Ventas",
                column: "DepositoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_SucursalId_FechaVenta",
                table: "Ventas",
                columns: new[] { "SucursalId", "FechaVenta" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_SucursalId_Fecha",
                table: "MovimientosCaja",
                columns: new[] { "SucursalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_SucursalId_FechaVencimiento",
                table: "Cuotas",
                columns: new[] { "SucursalId", "FechaVencimiento" });

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Sucursales_SucursalId",
                table: "Cuotas",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosCaja_Sucursales_SucursalId",
                table: "MovimientosCaja",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Depositos_DepositoId",
                table: "Ventas",
                column: "DepositoId",
                principalTable: "Depositos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Ventas_Sucursales_SucursalId",
                table: "Ventas",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cuotas_Sucursales_SucursalId",
                table: "Cuotas");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosCaja_Sucursales_SucursalId",
                table: "MovimientosCaja");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Depositos_DepositoId",
                table: "Ventas");

            migrationBuilder.DropForeignKey(
                name: "FK_Ventas_Sucursales_SucursalId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_DepositoId",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_Ventas_SucursalId_FechaVenta",
                table: "Ventas");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosCaja_SucursalId_Fecha",
                table: "MovimientosCaja");

            migrationBuilder.DropIndex(
                name: "IX_Cuotas_SucursalId_FechaVencimiento",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "DepositoId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Ventas");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "MovimientosCaja");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Cuotas");
        }
    }
}
