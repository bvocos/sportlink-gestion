using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class RenumberReceiptsPerBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Esta corrección es válida porque todavía no se entregó ningún recibo.
            // Desde que se entregue el primero no se debe volver a ejecutar esta migración
            // ni crear otra similar que renumere comprobantes ya emitidos.
            migrationBuilder.Sql("""
                UPDATE dbo.MovimientosCaja
                SET NumeroRecibo = NULL
                WHERE CuotaId IS NOT NULL AND Tipo=N'Ingreso';

                ;WITH RecibosOrdenados AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY SucursalId
                               ORDER BY CreatedAt, Id) AS Numero
                    FROM dbo.MovimientosCaja
                    WHERE CuotaId IS NOT NULL AND Tipo=N'Ingreso'
                )
                UPDATE mc
                SET NumeroRecibo = CONVERT(INT, ordenados.Numero)
                FROM dbo.MovimientosCaja mc
                INNER JOIN RecibosOrdenados ordenados ON ordenados.Id = mc.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No se revierte: restaurar la numeración global anterior generaría recibos inestables.
        }
    }
}
