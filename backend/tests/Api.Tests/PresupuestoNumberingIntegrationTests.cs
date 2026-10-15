using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

public sealed class PresupuestoNumberingIntegrationTests
{
    [Fact]
    public async Task Numbers_AreIndependentAndStablePerBranch_OverHttp()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkBudgetHttp_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var data = await Seed(factory.Services);
            using var branchA = await AdminClient(factory, data.BranchA);
            using var branchB = await AdminClient(factory, data.BranchB);

            var a1 = await CreateBudget(branchA, data.ClientA, data.ProductA, data.DepositA);
            var a2 = await CreateBudget(branchA, data.ClientA, data.ProductA, data.DepositA);
            var b1 = await CreateBudget(branchB, data.ClientB, data.ProductB, data.DepositB);
            Assert.Equal(1, a1.Numero);
            Assert.Equal(2, a2.Numero);
            Assert.Equal(1, b1.Numero);

            Assert.Equal(HttpStatusCode.NoContent, (await branchA.DeleteAsync($"/api/presupuestos/{a1.Id}")).StatusCode);
            var a3 = await CreateBudget(branchA, data.ClientA, data.ProductA, data.DepositA);
            Assert.Equal(3, a3.Numero);

            var listA = await branchA.GetFromJsonAsync<JsonElement>("/api/presupuestos");
            Assert.Equal([3, 2], listA.EnumerateArray().Select(x => x.GetProperty("numero").GetInt32()).ToArray());
            var listB = await branchB.GetFromJsonAsync<JsonElement>("/api/presupuestos");
            Assert.Equal([1], listB.EnumerateArray().Select(x => x.GetProperty("numero").GetInt32()).ToArray());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    [Fact]
    public async Task EightConcurrentCreates_ProduceExactlyOneThroughEight()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkBudgetConcurrency_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var data = await Seed(factory.Services);
            using var client = await AdminClient(factory, data.BranchA);

            var creates = Enumerable.Range(0, 8)
                .Select(_ => CreateBudget(client, data.ClientA, data.ProductA, data.DepositA));
            var results = await Task.WhenAll(creates);

            Assert.Equal(Enumerable.Range(1, 8), results.Select(x => x.Numero).Order());
            Assert.Equal(8, results.Select(x => x.Numero).Distinct().Count());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static async Task<HttpClient> AdminClient(BranchApiFactory factory, Guid branchId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", password = "Admin123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        client.DefaultRequestHeaders.Add("X-Sucursal-Id", branchId.ToString());
        return client;
    }

    private static async Task<CreatedBudget> CreateBudget(HttpClient client, Guid clientId, Guid productId, Guid depositId)
    {
        var response = await client.PostAsJsonAsync("/api/presupuestos", new
        {
            clienteId = clientId,
            fecha = DateOnly.FromDateTime(DateTime.Today),
            validezHasta = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            descuentoContadoPorcentaje = 0m,
            ivaContadoPorcentaje = 10.5m,
            ivaFinanciadoPorcentaje = 21m,
            entregaFinanciada = 0m,
            observaciones = (string?)null,
            depositoId = depositId,
            lineas = new[] { new { tipoCespedId = productId, color = (string?)null, cantidadM2 = 1m, precioContadoM2 = 20m, precioFinanciadoM2 = 22m } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreatedBudget>())!;
    }

    private static async Task<BudgetSeed> Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var branchA = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
        var depositB = new Deposito { Nombre = $"Budget B {Guid.NewGuid():N}", Activo = true };
        db.Add(depositB); await db.SaveChangesAsync();
        var branchB = new Sucursal { Nombre = $"Budget Branch B {Guid.NewGuid():N}", DepositoPropioId = depositB.Id, Activo = true };
        db.Add(branchB); await db.SaveChangesAsync();
        depositB.SucursalId = branchB.Id;
        var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin");
        admin.DebeCambiarPassword = false;
        var clientA = NewClient(branchA.Id, "A");
        var clientB = NewClient(branchB.Id, "B");
        var productA = NewProduct(branchA.DepositoPropioId, "A");
        var productB = NewProduct(depositB.Id, "B");
        db.AddRange(clientA, clientB, productA, productB);
        await db.SaveChangesAsync();
        return new(branchA.Id, branchB.Id, branchA.DepositoPropioId, depositB.Id, clientA.Id, clientB.Id, productA.Id, productB.Id);
    }

    private static Cliente NewClient(Guid branch, string suffix) => new()
    {
        SucursalId = branch, Nombre = "Cliente", Apellido = $"Presupuesto {suffix}",
        Telefono = "3515555555", Localidad = "Córdoba", Provincia = "Córdoba",
        Tipo = TipoCliente.Particular, FechaPrimerContacto = DateOnly.FromDateTime(DateTime.Today)
    };

    private static TipoCesped NewProduct(Guid deposit, string suffix) => new()
    {
        DepositoId = deposit, Nombre = $"Producto presupuesto {suffix} {Guid.NewGuid():N}",
        PrecioVentaM2 = 20, PrecioContadoM2 = 20, PrecioFinanciadoM2 = 22,
        CostoM2 = 10, Activo = true
    };

    private static async Task CreateDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    private sealed class BranchApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> { ["SkipDatabaseMigrations"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
            });
        }
    }

    private sealed record BudgetSeed(Guid BranchA, Guid BranchB, Guid DepositA, Guid DepositB,
        Guid ClientA, Guid ClientB, Guid ProductA, Guid ProductB);
    private sealed record CreatedBudget(Guid Id, int Numero);
}
