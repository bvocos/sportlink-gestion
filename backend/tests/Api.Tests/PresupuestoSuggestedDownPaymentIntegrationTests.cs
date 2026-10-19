using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

public sealed class PresupuestoSuggestedDownPaymentIntegrationTests
{
    [Fact]
    public async Task Suggestion_UsesOnlyVisibleProductsWithoutExposingTheirUnitCost()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkBudgetSuggestion_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var data = await Seed(factory.Services);
            using var adminA = await LoginAdmin(factory, data.BranchA);
            using var adminWithoutBranch = await LoginAdmin(factory, null);

            var single = await adminA.PostAsJsonAsync("/api/presupuestos/entrega-sugerida", new
            {
                lineas = new[] { new { tipoCespedId = data.ProductA, cantidadM2 = 25.5m } }
            });
            Assert.Equal(HttpStatusCode.OK, single.StatusCode);
            var singleBody = await single.Content.ReadAsStringAsync();
            Assert.DoesNotContain("costo", singleBody, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(255m, JsonDocument.Parse(singleBody).RootElement.GetProperty("entregaSugerida").GetDecimal());

            var multiple = await adminA.PostAsJsonAsync("/api/presupuestos/entrega-sugerida", new
            {
                lineas = new[]
                {
                    new { tipoCespedId = data.ProductA, cantidadM2 = 25.5m },
                    new { tipoCespedId = data.ProductA, cantidadM2 = 4.5m }
                }
            });
            Assert.Equal(HttpStatusCode.OK, multiple.StatusCode);
            Assert.Equal(300m, (await multiple.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("entregaSugerida").GetDecimal());

            var foreign = await adminA.PostAsJsonAsync("/api/presupuestos/entrega-sugerida", new
            {
                lineas = new[] { new { tipoCespedId = data.ProductB, cantidadM2 = 1m } }
            });
            Assert.Contains(foreign.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound });

            var withoutBranch = await adminWithoutBranch.PostAsJsonAsync("/api/presupuestos/entrega-sugerida", new
            {
                lineas = new[] { new { tipoCespedId = data.ProductA, cantidadM2 = 1m } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, withoutBranch.StatusCode);
            var withoutBranchBody = await withoutBranch.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(withoutBranchBody.GetProperty("errors").TryGetProperty("sucursalId", out _));

            var filters = await adminA.GetStringAsync("/api/presupuestos/filtros");
            Assert.DoesNotContain("costoM2", filters, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static async Task<SuggestionSeed> Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var branchA = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
        var depositB = new Deposito { Nombre = $"Suggestion B {Guid.NewGuid():N}", Activo = true };
        db.Add(depositB);
        await db.SaveChangesAsync();
        var branchB = new Sucursal
        {
            Nombre = $"Suggestion Branch B {Guid.NewGuid():N}",
            DepositoPropioId = depositB.Id,
            Activo = true
        };
        db.Add(branchB);
        await db.SaveChangesAsync();
        depositB.SucursalId = branchB.Id;

        var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin");
        admin.DebeCambiarPassword = false;
        var productA = NewProduct(branchA.DepositoPropioId, "Suggestion A");
        productA.CostoM2 = 10m;
        var productB = NewProduct(depositB.Id, "Suggestion B");
        productB.CostoM2 = 50m;
        db.AddRange(productA, productB);
        await db.SaveChangesAsync();
        return new(branchA.Id, productA.Id, productB.Id);
    }

    private sealed record SuggestionSeed(Guid BranchA, Guid ProductA, Guid ProductB);
}
