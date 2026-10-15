using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGastoSucursalIsolation : Migration
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

            migrationBuilder.DropIndex(
                name: "IX_Gastos_Fecha",
                table: "Gastos");

            migrationBuilder.AddColumn<Guid?>(
                name: "SucursalId",
                table: "Gastos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE dbo.Gastos SET SucursalId = '7B100000-0000-0000-0000-FFFFFFFFFFFF' WHERE SucursalId IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SucursalId",
                table: "Gastos",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_SucursalId_Fecha",
                table: "Gastos",
                columns: new[] { "SucursalId", "Fecha" });

            migrationBuilder.AddForeignKey(
                name: "FK_Gastos_Sucursales_SucursalId",
                table: "Gastos",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Gastos_Sucursales_SucursalId",
                table: "Gastos");

            migrationBuilder.DropIndex(
                name: "IX_Gastos_SucursalId_Fecha",
                table: "Gastos");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Gastos");

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_Fecha",
                table: "Gastos",
                column: "Fecha");
        }
    }
}
