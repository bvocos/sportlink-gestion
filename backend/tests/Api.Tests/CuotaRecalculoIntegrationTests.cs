using System.Net;
using System.Net.Http.Json;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

/// <summary>
/// Cobro de cuotas por HTTP: San Francisco recalcula las pendientes al cobrar un importe distinto
/// y al anular; Buenos Aires mantiene el comportamiento anterior.
/// </summary>
public sealed class CuotaRecalculoIntegrationTests
{
    [Fact]
    public async Task SanFranciscoRecalcula_BuenosAiresNo()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;
        var databaseName = $"SportlinkCuotasTests_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(masterConnection) { Database = databaseName }.ConnectionString;
        await CreateDatabase(masterConnection, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient(); // Inicia Program y SeedData.

            var sf = SeedData.SanFranciscoSucursalId; var ba = SeedData.BuenosAiresSucursalId;
            Guid sfVenta, baVenta;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin")).DebeCambiarPassword = false;
                var tax = (await db.AlicuotasIva.FirstAsync()).Id;
                sfVenta = await SeedSale(db, sf, SeedData.SanFranciscoDepositoId, tax);
                baVenta = await SeedSale(db, ba, SeedData.BuenosAiresDepositoId, tax);
            }

            // San Francisco: paga 70 de una cuota de 100 → las otras dos pasan a 115.
            using var sfClient = await LoginAdmin(factory, sf);
            var sfPrimera = await CuotaId(factory, sfVenta, 1);
            Assert.Equal(HttpStatusCode.OK, (await Pay(sfClient, sfPrimera, 70)).StatusCode);
            Assert.Equal(new[] { 70m, 115m, 115m }, await Pactados(factory, sfVenta));
            // Más que el saldo total de la venta: rechazado.
            Assert.Equal(HttpStatusCode.BadRequest, (await Pay(sfClient, await CuotaId(factory, sfVenta, 2), 231)).StatusCode);
            // Anular el cobro: vuelve a 100 / 100 / 100.
            Assert.Equal(HttpStatusCode.OK, (await sfClient.PostAsync($"/api/cuotas/{sfPrimera}/anular-pago", null)).StatusCode);
            Assert.Equal(new[] { 100m, 100m, 100m }, await Pactados(factory, sfVenta));

            // Buenos Aires: paga 70 → la cuota queda parcial y las demás no cambian; no se puede pagar de más.
            using var baClient = await LoginAdmin(factory, ba);
            var baPrimera = await CuotaId(factory, baVenta, 1);
            Assert.Equal(HttpStatusCode.OK, (await Pay(baClient, baPrimera, 70)).StatusCode);
            Assert.Equal(new[] { 100m, 100m, 100m }, await Pactados(factory, baVenta));
            Assert.Equal(HttpStatusCode.BadRequest, (await Pay(baClient, await CuotaId(factory, baVenta, 2), 150)).StatusCode);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(masterConnection, databaseName);
        }
    }

    private static async Task<Guid> SeedSale(AppDbContext db, Guid branch, Guid deposit, Guid tax)
    {
        var client = NewClient(branch, "Cuotas"); var product = NewProduct(deposit, "Cuotas");
        db.AddRange(client, product);
        await db.SaveChangesAsync();
        var sale = NewSale(branch, deposit, client.Id, product.Id, tax);
        sale.FormaPago = FormaPago.Cuotas; sale.CantidadCuotas = 3; sale.PrecioTotal = 300;
        db.Ventas.Add(sale);
        await db.SaveChangesAsync();
        for (var i = 1; i <= 3; i++) db.Cuotas.Add(NewInstallment(branch, deposit, sale.Id, client.Id, i));
        await db.SaveChangesAsync();
        return sale.Id;
    }

    private static Task<HttpResponseMessage> Pay(HttpClient client, Guid cuotaId, decimal importe) =>
        client.PostAsJsonAsync($"/api/cuotas/{cuotaId}/pagos",
            new { importe, medioPago = "Transferencia", fechaPago = DateOnly.FromDateTime(DateTime.Today) });

    private static async Task<Guid> CuotaId(BranchApiFactory factory, Guid venta, int numero)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Cuotas.IgnoreQueryFilters().Where(x => x.VentaId == venta && x.Numero == numero).Select(x => x.Id).SingleAsync();
    }

    private static async Task<decimal[]> Pactados(BranchApiFactory factory, Guid venta)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Cuotas.IgnoreQueryFilters().AsNoTracking().Where(x => x.VentaId == venta)
            .OrderBy(x => x.Numero).Select(x => x.ImportePactado).ToArrayAsync();
    }
}
