using Api.Shared.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Api.Tests;

public sealed class BudgetNumberMigrationIntegrationTests
{
    [Fact]
    public async Task Migration_PreservesExistingNumbersAndAllowsDuplicatesAcrossBranches()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkBudgetMigration_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
            await using var db = new AppDbContext(options);
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE [dbo].[__EFMigrationsHistory](
                    [MigrationId] nvarchar(150) NOT NULL,
                    [ProductVersion] nvarchar(32) NOT NULL,
                    CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId]));
                """);

            var migrations = db.Database.GetMigrations().ToList();
            var migration = Assert.Single(migrations, x => x.EndsWith("_AddBudgetNumberPerBranch"));
            foreach (var applied in migrations.Where(x => x != migration))
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES ({applied}, {"9.0.4"})");

            var depositA = new Deposito { Nombre = "Presupuesto migración A" };
            var depositB = new Deposito { Nombre = "Presupuesto migración B" };
            db.AddRange(depositA, depositB); await db.SaveChangesAsync();
            var branchA = new Sucursal { Nombre = "Presupuesto migración A", DepositoPropioId = depositA.Id };
            var branchB = new Sucursal { Nombre = "Presupuesto migración B", DepositoPropioId = depositB.Id };
            db.AddRange(branchA, branchB); await db.SaveChangesAsync();
            depositA.SucursalId = branchA.Id; depositB.SucursalId = branchB.Id;
            var clientA = NewClient(branchA.Id, "A");
            var clientB = NewClient(branchB.Id, "B");
            db.AddRange(clientA, clientB); await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync("""
                DROP INDEX IX_Presupuestos_SucursalId_Numero ON dbo.Presupuestos;
                ALTER TABLE dbo.Presupuestos DROP COLUMN Numero;
                ALTER TABLE dbo.Presupuestos ADD Numero INT IDENTITY(1,1) NOT NULL;
                CREATE UNIQUE INDEX IX_Presupuestos_Numero ON dbo.Presupuestos(Numero);
                """);

            await InsertOldBudget(db, Guid.NewGuid(), 1, branchA.Id, depositA.Id, clientA.Id);
            await InsertOldBudget(db, Guid.NewGuid(), 2, branchA.Id, depositA.Id, clientA.Id);
            await InsertOldBudget(db, Guid.NewGuid(), 5, branchA.Id, depositA.Id, clientA.Id);

            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync();

            Assert.Equal([1, 2, 5], await db.Presupuestos.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.SucursalId == branchA.Id).OrderBy(x => x.Numero).Select(x => x.Numero).ToListAsync());
            Assert.False(await IsIdentity(connection));
            Assert.True(await IndexExists(connection, "IX_Presupuestos_SucursalId_Numero"));
            Assert.False(await IndexExists(connection, "IX_Presupuestos_Numero"));

            db.Presupuestos.Add(NewBudget(1, branchB.Id, depositB.Id, clientB.Id));
            await db.SaveChangesAsync();
            db.Presupuestos.Add(NewBudget(1, branchA.Id, depositA.Id, clientA.Id));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            // Fuerza una segunda ejecución de Up sobre la forma nueva para comprobar sus guardas.
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.__EFMigrationsHistory WHERE MigrationId={migration}");
            await migrator.MigrateAsync();
            Assert.False(await IsIdentity(connection));
            Assert.True(await IndexExists(connection, "IX_Presupuestos_SucursalId_Numero"));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static Presupuesto NewBudget(int number, Guid branch, Guid deposit, Guid client) => new()
    {
        Numero = number, SucursalId = branch, DepositoId = deposit, ClienteId = client,
        Fecha = DateOnly.FromDateTime(DateTime.Today), ValidezHasta = DateOnly.FromDateTime(DateTime.Today.AddDays(1))
    };

    private static Cliente NewClient(Guid branch, string suffix) => new()
    {
        SucursalId = branch, Nombre = "Cliente", Apellido = $"Migración {suffix}",
        Localidad = "Córdoba", Provincia = "Córdoba", Tipo = TipoCliente.Particular
    };

    private static async Task InsertOldBudget(AppDbContext db, Guid id, int number, Guid branch, Guid deposit, Guid client) =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            SET IDENTITY_INSERT dbo.Presupuestos ON;
            INSERT INTO dbo.Presupuestos
                (Id, Numero, SucursalId, DepositoId, ClienteId, Fecha, ValidezHasta, Estado,
                 DescuentoContadoPorcentaje, IvaContadoPorcentaje, IvaFinanciadoPorcentaje,
                 EntregaFinanciada, CreatedAt)
            VALUES
                ({id}, {number}, {branch}, {deposit}, {client}, {DateOnly.FromDateTime(DateTime.Today)},
                 {DateOnly.FromDateTime(DateTime.Today.AddDays(1))}, N'Borrador', 0, 10.5, 21, 0, {DateTimeOffset.UtcNow});
            SET IDENTITY_INSERT dbo.Presupuestos OFF;
            """);

    private static async Task<bool> IsIdentity(string connection)
    {
        await using var sql = new SqlConnection(connection); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = "SELECT CONVERT(int, COLUMNPROPERTY(OBJECT_ID(N'dbo.Presupuestos'), N'Numero', 'IsIdentity'))";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task<bool> IndexExists(string connection, string name)
    {
        await using var sql = new SqlConnection(connection); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Presupuestos') AND name=@name";
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task CreateDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }
}
