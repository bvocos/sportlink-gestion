using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultipleDepositsPerProduct : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TiposCespedDepositos",
                columns: table => new
                {
                    TipoCespedId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposCespedDepositos", x => new { x.TipoCespedId, x.DepositoId });
                    table.ForeignKey(
                        name: "FK_TiposCespedDepositos_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TiposCespedDepositos_TiposCesped_TipoCespedId",
                        column: x => x.TipoCespedId,
                        principalTable: "TiposCesped",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TiposCespedDepositos_DepositoId",
                table: "TiposCespedDepositos",
                column: "DepositoId");

            migrationBuilder.Sql("""
                INSERT INTO [TiposCespedDepositos] ([TipoCespedId], [DepositoId])
                SELECT [Id], [DepositoId]
                FROM [TiposCesped]
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TiposCespedDepositos");
        }
    }
}
