using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddImportaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EsImportado",
                table: "TiposCesped",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportacionId",
                table: "MovimientosStock",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImportacionId",
                table: "LotesStock",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Importaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Codigo = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    NumeroDespacho = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Observaciones = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Importaciones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Importaciones_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId_ImportacionId",
                table: "MovimientosStock",
                columns: new[] { "DepositoId", "TipoCespedId", "ImportacionId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_ImportacionId",
                table: "MovimientosStock",
                column: "ImportacionId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_ImportacionId",
                table: "LotesStock",
                column: "ImportacionId");

            migrationBuilder.CreateIndex(
                name: "IX_Importaciones_SucursalId_Codigo",
                table: "Importaciones",
                columns: new[] { "SucursalId", "Codigo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LotesStock_Importaciones_ImportacionId",
                table: "LotesStock",
                column: "ImportacionId",
                principalTable: "Importaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MovimientosStock_Importaciones_ImportacionId",
                table: "MovimientosStock",
                column: "ImportacionId",
                principalTable: "Importaciones",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LotesStock_Importaciones_ImportacionId",
                table: "LotesStock");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimientosStock_Importaciones_ImportacionId",
                table: "MovimientosStock");

            migrationBuilder.DropTable(
                name: "Importaciones");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId_ImportacionId",
                table: "MovimientosStock");

            migrationBuilder.DropIndex(
                name: "IX_MovimientosStock_ImportacionId",
                table: "MovimientosStock");

            migrationBuilder.DropIndex(
                name: "IX_LotesStock_ImportacionId",
                table: "LotesStock");

            migrationBuilder.DropColumn(
                name: "EsImportado",
                table: "TiposCesped");

            migrationBuilder.DropColumn(
                name: "ImportacionId",
                table: "MovimientosStock");

            migrationBuilder.DropColumn(
                name: "ImportacionId",
                table: "LotesStock");
        }
    }
}
