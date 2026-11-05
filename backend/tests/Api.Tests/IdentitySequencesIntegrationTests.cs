using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

/// <summary>
/// Datos copiados o restaurados con Ids explícitos: al arrancar, la app adelanta los contadores
/// automáticos para que los nuevos registros de auditoría no choquen con Ids existentes.
/// </summary>
public sealed class IdentitySequencesIntegrationTests
{
    [Fact]
    public async Task Startup_AlignsAuditSequenceWithCopiedRows()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;
        var databaseName = $"SportlinkSequenceTests_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(masterConnection) { Database = databaseName }.ConnectionString;
        await CreateDatabase(masterConnection, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
            await using (var db = new AppDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                // Igual que la copia desde Azure: filas con Id explícito, contador sin avanzar.
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO "RegistrosAuditoria" ("Id","FechaHora","Usuario","Modulo","Accion","Entidad","EntidadId","DetalleJson")
                    VALUES (500, now(), 'admin', 'Ventas', 'Creación', 'Venta', 'x', '{{}}');
                    """);
            }

            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient(); // Inicia Program y SeedData, que alinea los contadores.

            await using var check = new AppDbContext(options);
            var next = await check.Database
                .SqlQueryRaw<long>("""SELECT nextval(pg_get_serial_sequence('"RegistrosAuditoria"', 'Id')) AS "Value" """)
                .SingleAsync();
            Assert.True(next > 500, $"El próximo Id de auditoría ({next}) debería ser mayor que 500.");

            // Repetir el arranque no retrocede el contador.
            await IdentitySequences.AlignAsync(check);
            var again = await check.Database
                .SqlQueryRaw<long>("""SELECT nextval(pg_get_serial_sequence('"RegistrosAuditoria"', 'Id')) AS "Value" """)
                .SingleAsync();
            Assert.True(again > next);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(masterConnection, databaseName);
        }
    }
}
