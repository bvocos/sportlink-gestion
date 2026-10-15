using Api.Shared.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Api.Tests;

public sealed class ReceiptRenumberMigrationIntegrationTests
{
    [Fact]
    public async Task Migration_RenumbersInterleavedReceiptsWithoutGapsPerBranch()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkReceiptMigration_{Guid.NewGuid():N}";
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
            Assert.Contains(migrations, x => x.EndsWith("_AddBudgetNumberPerBranch"));
            // Solo queda pendiente la migración bajo prueba; las posteriores, incluida la de
            // numeración de presupuestos, se marcan como aplicadas para aislar este escenario.
            foreach (var migration in migrations.Where(x => !x.EndsWith("_RenumberReceiptsPerBranch")))
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.__EFMigrationsHistory(MigrationId, ProductVersion) VALUES ({migration}, {"9.0.4"})");
            var migrator = db.GetService<IMigrator>();

            var depositA = new Deposito { Nombre = "Migración A" };
            var depositB = new Deposito { Nombre = "Migración B" };
            db.AddRange(depositA, depositB); await db.SaveChangesAsync();
            var branchA = new Sucursal { Nombre = "Migración A", DepositoPropioId = depositA.Id };
            var branchB = new Sucursal { Nombre = "Migración B", DepositoPropioId = depositB.Id };
            db.AddRange(branchA, branchB); await db.SaveChangesAsync();
            depositA.SucursalId = branchA.Id; depositB.SucursalId = branchB.Id;
            var clientA = NewClient(branchA.Id, "A"); var clientB = NewClient(branchB.Id, "B");
            var productA = NewProduct(depositA.Id, "A"); var productB = NewProduct(depositB.Id, "B");
            var tax = new AlicuotaIva { Nombre = "Exento migración", Porcentaje = 0 };
            db.AddRange(clientA, clientB, productA, productB, tax); await db.SaveChangesAsync();

            var saleA = NewSale(branchA.Id, depositA.Id, clientA.Id, productA.Id, tax.Id);
            var saleB = NewSale(branchB.Id, depositB.Id, clientB.Id, productB.Id, tax.Id);
            db.AddRange(saleA, saleB); await db.SaveChangesAsync();
            var installments = new[]
            {
                NewInstallment(branchA.Id, depositA.Id, saleA.Id, clientA.Id, 1),
                NewInstallment(branchB.Id, depositB.Id, saleB.Id, clientB.Id, 1),
                NewInstallment(branchA.Id, depositA.Id, saleA.Id, clientA.Id, 2),
                NewInstallment(branchB.Id, depositB.Id, saleB.Id, clientB.Id, 2)
            };
            db.AddRange(installments); await db.SaveChangesAsync();
            var movements = new[]
            {
                NewMovement(branchA.Id, saleA.Id, installments[0].Id, 1),
                NewMovement(branchB.Id, saleB.Id, installments[1].Id, 2),
                NewMovement(branchA.Id, saleA.Id, installments[2].Id, 3),
                NewMovement(branchB.Id, saleB.Id, installments[3].Id, 4)
            };
            db.AddRange(movements); await db.SaveChangesAsync();

            var sameTime = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            for (var i = 0; i < movements.Length; i++)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE dbo.MovimientosCaja SET CreatedAt={sameTime.AddMinutes(i)}, NumeroRecibo={i + 1} WHERE Id={movements[i].Id}");

            await migrator.MigrateAsync();

            var result = await db.MovimientosCaja.IgnoreQueryFilters().AsNoTracking()
                .Where(x => x.CuotaId != null && x.Tipo == TipoMovimiento.Ingreso)
                .OrderBy(x => x.CreatedAt).ToListAsync();
            Assert.Equal([1, 2], result.Where(x => x.SucursalId == branchA.Id).Select(x => x.NumeroRecibo));
            Assert.Equal([1, 2], result.Where(x => x.SucursalId == branchB.Id).Select(x => x.NumeroRecibo));
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var connectionToMaster = new SqlConnection(master);
            await connectionToMaster.OpenAsync();
            await using var drop = connectionToMaster.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static Cliente NewClient(Guid branch, string suffix) => new()
        { SucursalId = branch, Nombre = "Cliente", Apellido = suffix, Localidad = "Córdoba", Provincia = "Córdoba", Tipo = TipoCliente.Particular };
    private static TipoCesped NewProduct(Guid deposit, string suffix) => new()
        { DepositoId = deposit, Nombre = $"Producto {suffix}", PrecioVentaM2 = 10, PrecioContadoM2 = 10, PrecioFinanciadoM2 = 10, CostoM2 = 5 };
    private static Venta NewSale(Guid branch, Guid deposit, Guid client, Guid product, Guid tax) => new()
        { SucursalId = branch, DepositoId = deposit, ClienteId = client, TipoCespedId = product, AlicuotaIvaId = tax, FechaVenta = DateOnly.FromDateTime(DateTime.Today), CantidadM2 = 1, PrecioUnitario = 10, PrecioTotal = 10, CostoCompraUnitario = 5, CostoCompraTotal = 5, FormaPago = FormaPago.Cuotas, Estado = EstadoVenta.Confirmada };
    private static Cuota NewInstallment(Guid branch, Guid deposit, Guid sale, Guid client, int number) => new()
        { SucursalId = branch, DepositoId = deposit, VentaId = sale, ClienteId = client, Numero = number, FechaVencimiento = DateOnly.FromDateTime(DateTime.Today), ImportePactado = 100, Estado = EstadoCuota.Pagada };
    private static MovimientoCaja NewMovement(Guid branch, Guid sale, Guid installment, int oldNumber) => new()
        { SucursalId = branch, VentaId = sale, CuotaId = installment, Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow, Monto = 100, Concepto = "Pago histórico", NumeroRecibo = oldNumber };
    private static async Task CreateDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = $"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync();
    }
}
