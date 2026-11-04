using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

/// <summary>
/// SQL Server (collation CI_AS) no distinguía mayúsculas. En PostgreSQL se mantiene con citext y,
/// para texto concatenado (Nombre + " " + Apellido), comparando en minúsculas.
/// </summary>
public sealed class CaseInsensitiveTextIntegrationTests
{
    [Fact]
    public async Task LoginAndSearches_IgnoreCase_LikeSqlServer()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;
        var databaseName = $"SportlinkCaseTests_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(masterConnection) { Database = databaseName }.ConnectionString;
        await CreateDatabase(masterConnection, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient(); // Inicia Program y SeedData.
            var branch = SeedData.SanFranciscoSucursalId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin")).DebeCambiarPassword = false;
                var cliente = NewClient(branch, "Pérez Muñoz");
                cliente.Nombre = "José";
                db.Clientes.Add(cliente);
                db.Clientes.Add(NewClient(branch, "Gómez 100%_raro"));
                await db.SaveChangesAsync();
            }

            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            Assert.Equal(HttpStatusCode.OK,
                (await client.PostAsJsonAsync("/api/auth/login", new { usuario = "ADMIN", password = "Admin123!" })).StatusCode);
            client.DefaultRequestHeaders.Add("X-Sucursal-Id", branch.ToString());

            Assert.Equal(1, await CountClients(client, "josé pérez"));
            Assert.Equal(1, await CountClients(client, "PÉREZ MU"));
            Assert.Equal(1, await CountClients(client, "100%_"));
            Assert.Equal(0, await CountClients(client, "%zz")); // los comodines se buscan literalmente
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(masterConnection, databaseName);
        }
    }

    private static async Task<int> CountClients(HttpClient client, string term)
    {
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/clientes?buscar={Uri.EscapeDataString(term)}");
        return page.GetProperty("totalCount").GetInt32();
    }
}
