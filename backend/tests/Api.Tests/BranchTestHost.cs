using System.Net;
using System.Net.Http.Json;
using Api.Shared.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

internal static class BranchTestHost
{
    public static async Task CreateDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task DropDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]";
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<HttpClient> LoginAdmin(BranchApiFactory factory, Guid? branchId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", password = "Admin123!" })).StatusCode);
        if (branchId.HasValue) client.DefaultRequestHeaders.Add("X-Sucursal-Id", branchId.Value.ToString());
        return client;
    }

    public static async Task<HttpClient> LoginUser(BranchApiFactory factory, string username, string password,
        Guid? attemptedBranchOverride = null)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new { usuario = username, password })).StatusCode);
        if (attemptedBranchOverride.HasValue)
            client.DefaultRequestHeaders.Add("X-Sucursal-Id", attemptedBranchOverride.Value.ToString());
        return client;
    }

    public static Cliente NewClient(Guid branch, string suffix) => new()
    {
        SucursalId = branch, Nombre = "Cliente", Apellido = suffix, Telefono = "3515555555",
        Localidad = "Córdoba", Provincia = "Córdoba", Tipo = TipoCliente.Particular,
        FechaPrimerContacto = DateOnly.FromDateTime(DateTime.Today)
    };

    public static TipoCesped NewProduct(Guid deposit, string suffix) => new()
    {
        DepositoId = deposit, Nombre = $"Producto {suffix} {Guid.NewGuid():N}", PrecioVentaM2 = 20,
        PrecioContadoM2 = 20, PrecioFinanciadoM2 = 22, CostoM2 = 10, Activo = true,
        ControlPorLotes = true
    };

    public static Venta NewSale(Guid branch, Guid deposit, Guid client, Guid product, Guid tax) => new()
    {
        SucursalId = branch, DepositoId = deposit, ClienteId = client, TipoCespedId = product,
        AlicuotaIvaId = tax, FechaVenta = DateOnly.FromDateTime(DateTime.Today), CantidadM2 = 1,
        PrecioUnitario = 20, PrecioTotal = 20, CostoCompraUnitario = 10, CostoCompraTotal = 10,
        FormaPago = FormaPago.Contado, Estado = EstadoVenta.Confirmada
    };

    public static Cuota NewInstallment(Guid branch, Guid deposit, Guid sale, Guid client, int number = 1) => new()
    {
        SucursalId = branch, DepositoId = deposit, VentaId = sale, ClienteId = client, Numero = number,
        FechaVencimiento = DateOnly.FromDateTime(DateTime.Today), ImportePactado = 100,
        ImportePagado = 0, Estado = EstadoCuota.Pendiente
    };
}

internal sealed class BranchApiFactory(string connectionString) : WebApplicationFactory<Program>
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
