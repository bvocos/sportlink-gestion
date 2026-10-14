using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSucursalDepositoBusinessIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Presupuestos_Fecha",
                table: "Presupuestos");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "TiposCesped",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "DepositoId",
                table: "TiposCesped",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("7a100000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "DepositoId",
                table: "Presupuestos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("7a100000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "Presupuestos",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("7b100000-0000-0000-0000-000000000001"));

            migrationBuilder.AddColumn<Guid>(
                name: "DepositoId",
                table: "Cuotas",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("""
                UPDATE c
                SET DepositoId = v.DepositoId
                FROM dbo.Cuotas c
                INNER JOIN dbo.Ventas v ON v.Id = c.VentaId;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TiposCesped_DepositoId_Nombre",
                table: "TiposCesped",
                columns: new[] { "DepositoId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_DepositoId",
                table: "Presupuestos",
                column: "DepositoId");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_SucursalId_Fecha",
                table: "Presupuestos",
                columns: new[] { "SucursalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_DepositoId",
                table: "Cuotas",
                column: "DepositoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Depositos_DepositoId",
                table: "Cuotas",
                column: "DepositoId",
                principalTable: "Depositos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_Depositos_DepositoId",
                table: "Presupuestos",
                column: "DepositoId",
                principalTable: "Depositos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Presupuestos_Sucursales_SucursalId",
                table: "Presupuestos",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TiposCesped_Depositos_DepositoId",
                table: "TiposCesped",
                column: "DepositoId",
                principalTable: "Depositos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Cuotas_Depositos_DepositoId",
                table: "Cuotas");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_Depositos_DepositoId",
                table: "Presupuestos");

            migrationBuilder.DropForeignKey(
                name: "FK_Presupuestos_Sucursales_SucursalId",
                table: "Presupuestos");

            migrationBuilder.DropForeignKey(
                name: "FK_TiposCesped_Depositos_DepositoId",
                table: "TiposCesped");

            migrationBuilder.DropIndex(
                name: "IX_TiposCesped_DepositoId_Nombre",
                table: "TiposCesped");

            migrationBuilder.DropIndex(
                name: "IX_Presupuestos_DepositoId",
                table: "Presupuestos");

            migrationBuilder.DropIndex(
                name: "IX_Presupuestos_SucursalId_Fecha",
                table: "Presupuestos");

            migrationBuilder.DropIndex(
                name: "IX_Cuotas_DepositoId",
                table: "Cuotas");

            migrationBuilder.DropColumn(
                name: "DepositoId",
                table: "TiposCesped");

            migrationBuilder.DropColumn(
                name: "DepositoId",
                table: "Presupuestos");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Presupuestos");

            migrationBuilder.DropColumn(
                name: "DepositoId",
                table: "Cuotas");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "TiposCesped",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_Fecha",
                table: "Presupuestos",
                column: "Fecha");
        }
    }
}
