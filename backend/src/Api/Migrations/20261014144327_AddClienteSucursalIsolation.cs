using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddClienteSucursalIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Clientes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Apellido",
                table: "Clientes",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<Guid>(
                name: "SucursalId",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE clientes
                SET SucursalId = sucursales.Id
                FROM dbo.Clientes clientes
                CROSS JOIN dbo.Sucursales sucursales
                WHERE sucursales.Nombre = N'San Francisco';
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "SucursalId",
                table: "Clientes",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_SucursalId_Apellido_Nombre",
                table: "Clientes",
                columns: new[] { "SucursalId", "Apellido", "Nombre" });

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Sucursales_SucursalId",
                table: "Clientes",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Clientes_Sucursales_SucursalId",
                table: "Clientes");

            migrationBuilder.DropIndex(
                name: "IX_Clientes_SucursalId_Apellido_Nombre",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "SucursalId",
                table: "Clientes");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<string>(
                name: "Apellido",
                table: "Clientes",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
