using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoteStockTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ControlPorLotes",
                table: "TiposCesped",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "LotesStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoCespedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Color = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FechaIngreso = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Usuario = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    VentaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesStock_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesStock_TiposCesped_TipoCespedId",
                        column: x => x.TipoCespedId,
                        principalTable: "TiposCesped",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesStock_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rollos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteStockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Posicion = table.Column<string>(type: "char(1)", nullable: false),
                    CodigoBarra = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CantidadM2 = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rollos", x => x.Id);
                    table.CheckConstraint("CK_Rollos_CantidadM2", "[CantidadM2] > 0");
                    table.CheckConstraint("CK_Rollos_Posicion", "[Posicion] IN ('A','B','C')");
                    table.ForeignKey(
                        name: "FK_Rollos_LotesStock_LoteStockId",
                        column: x => x.LoteStockId,
                        principalTable: "LotesStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_DepositoId_TipoCespedId_Estado",
                table: "LotesStock",
                columns: new[] { "DepositoId", "TipoCespedId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_TipoCespedId",
                table: "LotesStock",
                column: "TipoCespedId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_VentaId",
                table: "LotesStock",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Rollos_CodigoBarra",
                table: "Rollos",
                column: "CodigoBarra",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rollos_LoteStockId",
                table: "Rollos",
                column: "LoteStockId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Rollos");

            migrationBuilder.DropTable(
                name: "LotesStock");

            migrationBuilder.DropColumn(
                name: "ControlPorLotes",
                table: "TiposCesped");
        }
    }
}
