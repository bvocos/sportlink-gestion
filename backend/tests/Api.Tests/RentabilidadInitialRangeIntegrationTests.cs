using System.Net.Http.Json;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

/// <summary>
/// Rentabilidad: en San Francisco el filtro "Desde" arranca en su primera venta (no cancelada);
/// en Buenos Aires no hay fecha inicial especial (el frontend usa el mes en curso).
/// </summary>
public sealed class RentabilidadInitialRangeIntegrationTests
{
    [Fact]
    public async Task InitialRange_OnlySanFranciscoStartsAtItsFirstSale()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;
        var databaseName = $"SportlinkRentabilidadTests_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(masterConnection) { Database = databaseName }.ConnectionString;
        await CreateDatabase(masterConnection, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient(); // Inicia Program y SeedData.

            var sf = SeedData.SanFranciscoSucursalId;
            var ba = SeedData.BuenosAiresSucursalId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin")).DebeCambiarPassword = false;
                var tax = (await db.AlicuotasIva.FirstAsync()).Id;
                var sfClient = NewClient(sf, "SF"); var baClient = NewClient(ba, "BA");
                var sfProduct = NewProduct(SeedData.SanFranciscoDepositoId, "SF");
                var baProduct = NewProduct(SeedData.BuenosAiresDepositoId, "BA");
                db.AddRange(sfClient, baClient, sfProduct, baProduct);
                await db.SaveChangesAsync();

                Venta Sale(Guid branch, Guid deposit, Guid client, Guid product, DateOnly date, EstadoVenta estado = EstadoVenta.Confirmada)
                {
                    var sale = NewSale(branch, deposit, client, product, tax);
                    sale.FechaVenta = date; sale.Estado = estado;
                    return sale;
                }
                db.Ventas.AddRange(
                    Sale(sf, SeedData.SanFranciscoDepositoId, sfClient.Id, sfProduct.Id, new DateOnly(2026, 1, 5), EstadoVenta.Cancelada),
                    Sale(sf, SeedData.SanFranciscoDepositoId, sfClient.Id, sfProduct.Id, new DateOnly(2026, 3, 10)),
                    Sale(sf, SeedData.SanFranciscoDepositoId, sfClient.Id, sfProduct.Id, new DateOnly(2026, 6, 1)),
                    Sale(ba, SeedData.BuenosAiresDepositoId, baClient.Id, baProduct.Id, new DateOnly(2025, 12, 1)));
                await db.SaveChangesAsync();
            }

            using var sfAdmin = await LoginAdmin(factory, sf);
            var sfRange = await sfAdmin.GetFromJsonAsync<JsonElement>("/api/rentabilidad/rango-inicial");
            Assert.Equal("2026-03-10", sfRange.GetProperty("desde").GetString());

            using var baAdmin = await LoginAdmin(factory, ba);
            var baRange = await baAdmin.GetFromJsonAsync<JsonElement>("/api/rentabilidad/rango-inicial");
            Assert.Equal(JsonValueKind.Null, baRange.GetProperty("desde").ValueKind);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(masterConnection, databaseName);
        }
    }
}
