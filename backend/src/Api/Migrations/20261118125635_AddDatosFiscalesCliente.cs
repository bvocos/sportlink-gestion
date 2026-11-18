using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDatosFiscalesCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPostal",
                table: "Clientes",
                type: "citext",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CondicionIva",
                table: "Clientes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domicilio",
                table: "Clientes",
                type: "citext",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NumeroDocumento",
                table: "Clientes",
                type: "citext",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RazonSocial",
                table: "Clientes",
                type: "citext",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumento",
                table: "Clientes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_SucursalId_NumeroDocumento",
                table: "Clientes",
                columns: new[] { "SucursalId", "NumeroDocumento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Clientes_SucursalId_NumeroDocumento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CodigoPostal",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "CondicionIva",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "Domicilio",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "NumeroDocumento",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "RazonSocial",
                table: "Clientes");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Clientes");
        }
    }
}
