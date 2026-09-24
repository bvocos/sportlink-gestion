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

public sealed class VentaEndpointIntegrationTests
{
    [Fact]
    public async Task PostVenta_PersistsUsingSqlServerRetryingExecutionStrategy()
    {
        var masterConnection = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(masterConnection)) return;

        var databaseName = $"SportlinkTests_{Guid.NewGuid():N}";
        var databaseConnection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = databaseName }.ConnectionString;
        await WaitForSqlServer(masterConnection);

        await using (var master = new SqlConnection(masterConnection))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(databaseConnection, sql => sql.EnableRetryOnFailure())
                .Options;
            await using (var schema = new AppDbContext(options))
                await schema.Database.EnsureCreatedAsync();

            await using var factory = new VentaApiFactory(databaseConnection);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            Guid clienteId;
            Guid tipoCespedId;
            Guid alicuotaIvaId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var cliente = new Cliente
                {
                    Nombre = "Cliente", Apellido = "Integracion", Telefono = "3515555555",
                    Localidad = "Cordoba", Provincia = "Cordoba", Tipo = TipoCliente.Particular
                };
                var tipo = new TipoCesped
                {
                    Nombre = $"Producto {Guid.NewGuid():N}", PrecioVentaM2 = 20m,
                    PrecioContadoM2 = 20m, PrecioFinanciadoM2 = 22m, CostoM2 = 10m, Activo = true
                };
                var alicuota = await db.AlicuotasIva.FirstAsync();
                var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin");
                admin.DebeCambiarPassword = false;
                db.Clientes.Add(cliente);
                db.TiposCesped.Add(tipo);
                await db.SaveChangesAsync();
                clienteId = cliente.Id;
                tipoCespedId = tipo.Id;
                alicuotaIvaId = alicuota.Id;
            }

            var login = await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", password = "Admin123!" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);

            var response = await client.PostAsJsonAsync("/api/ventas", new
            {
                clienteId,
                fechaVenta = DateOnly.FromDateTime(DateTime.Today),
                tipoCespedId,
                cantidadM2 = 10m,
                precioUnitario = 20m,
                precioTotal = 200m,
                montoEntrega = 0m,
                formaPago = "Cuotas",
                cantidadCuotas = 2,
                estado = "Confirmada",
                fechaEntregaEstimada = (DateOnly?)null,
                observaciones = "Prueba de integracion",
                costoCompraUnitario = 10m,
                costoEnvio = 0m,
                otrosCostos = 0m,
                alicuotaIvaId,
                color = (string?)null,
                lineas = new[]
                {
                    new { tipoCespedId, color = (string?)null, cantidadM2 = 10m, precioCompraM2 = 10m, precioVentaM2 = 20m, total = 200m }
                }
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            using var verificationScope = factory.Services.CreateScope();
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await verificationDb.Ventas.CountAsync());
            Assert.Equal(2, await verificationDb.Cuotas.CountAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var master = new SqlConnection(masterConnection);
            await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task WaitForSqlServer(string connectionString)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        throw new InvalidOperationException("SQL Server de pruebas no estuvo disponible.", lastError);
    }

    private sealed class VentaApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseMigrations"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
                    connectionString,
                    sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
            });
        }
    }
}
