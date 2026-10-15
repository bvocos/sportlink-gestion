using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditBranchScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "RegistrosAuditoria",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_SucursalId_FechaHora",
                table: "RegistrosAuditoria",
                columns: new[] { "SucursalId", "FechaHora" });

            migrationBuilder.AddForeignKey(
                name: "FK_RegistrosAuditoria_Sucursales_SucursalId",
                table: "RegistrosAuditoria",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RegistrosAuditoria_Sucursales_SucursalId",
                table: "RegistrosAuditoria");

            migrationBuilder.DropIndex(
                name: "IX_RegistrosAuditoria_SucursalId_FechaHora",
                table: "RegistrosAuditoria");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "RegistrosAuditoria");
        }
    }
}
