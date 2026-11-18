using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRemitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Remitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceptorNombre = table.Column<string>(type: "citext", maxLength: 200, nullable: false),
                    ReceptorDomicilio = table.Column<string>(type: "citext", maxLength: 250, nullable: true),
                    ReceptorLocalidad = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    ReceptorProvincia = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    ReceptorTelefono = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    ReceptorCuit = table.Column<string>(type: "citext", maxLength: 13, nullable: true),
                    ReceptorCondicionIva = table.Column<string>(type: "citext", maxLength: 80, nullable: true),
                    Observaciones = table.Column<string>(type: "citext", maxLength: 1000, nullable: true),
                    LineasJson = table.Column<string>(type: "citext", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Remitos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Remitos_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Remitos_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Remitos_SucursalId_Numero",
                table: "Remitos",
                columns: new[] { "SucursalId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Remitos_VentaId",
                table: "Remitos",
                column: "VentaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Remitos");
        }
    }
}
