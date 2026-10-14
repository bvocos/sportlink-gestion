using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleDepositsPerBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Depositos_Nombre",
                table: "Depositos");

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "Depositos",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE depositos
                SET SucursalId = sucursales.Id
                FROM dbo.Depositos depositos
                INNER JOIN dbo.Sucursales sucursales ON sucursales.DepositoPropioId = depositos.Id;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Depositos_SucursalId_Nombre",
                table: "Depositos",
                columns: new[] { "SucursalId", "Nombre" },
                unique: true,
                filter: "[SucursalId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Depositos_Sucursales_SucursalId",
                table: "Depositos",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Depositos_Sucursales_SucursalId",
                table: "Depositos");

            migrationBuilder.DropIndex(
                name: "IX_Depositos_SucursalId_Nombre",
                table: "Depositos");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Depositos");

            migrationBuilder.CreateIndex(
                name: "IX_Depositos_Nombre",
                table: "Depositos",
                column: "Nombre",
                unique: true);
        }
    }
}
