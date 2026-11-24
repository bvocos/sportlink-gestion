using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

public sealed class SaleBalanceCollectionIntegrationTests
{
    [Fact]
    public async Task Collections_are_branch_scoped_update_cash_and_never_exceed_the_balance()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkSaleCollections_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(master) { Database = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            await using (var schema = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
                await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var seeded = await Seed(factory.Services);
            using var adminA = await LoginAdmin(factory, seeded.BranchA);
            using var adminB = await LoginAdmin(factory, seeded.BranchB);

            var first = await adminA.PostAsJsonAsync($"/api/ventas/{seeded.SaleA}/cobros", new
            {
                importeUsd = 30m, fechaCobro = DateOnly.FromDateTime(DateTime.Today), monedaCobro = "USD",
                cotizacion = 1200m, medioPago = "Efectivo"
            });
            Assert.True(first.StatusCode == HttpStatusCode.Created,
                $"Expected 201, got {(int)first.StatusCode}: {await first.Content.ReadAsStringAsync()}");
            var firstJson = await first.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, firstJson.GetProperty("numeroRecibo").GetInt32());

            var ars = await adminA.PostAsJsonAsync($"/api/ventas/{seeded.SaleA}/cobros", new
            {
                importeUsd = 10m, fechaCobro = DateOnly.FromDateTime(DateTime.Today), monedaCobro = "ARS",
                cotizacion = 1250m, medioPago = "Transferencia"
            });
            Assert.Equal(HttpStatusCode.Created, ars.StatusCode);
            var detail = await adminA.GetFromJsonAsync<JsonElement>($"/api/ventas/{seeded.SaleA}/cobros");
            Assert.Equal(40m, detail.GetProperty("saldoUsd").GetDecimal());
            Assert.Equal(12_500m, detail.GetProperty("cobros")[1].GetProperty("importeMonedaCobro").GetDecimal());
            Assert.Equal(HttpStatusCode.NotFound,
                (await adminA.GetAsync($"/api/ventas/{seeded.SaleB}/cobros")).StatusCode);
            Assert.Equal(80m, (await adminB.GetFromJsonAsync<JsonElement>($"/api/ventas/{seeded.SaleB}/cobros"))
                .GetProperty("saldoUsd").GetDecimal());

            var tooLarge = await adminA.PostAsJsonAsync($"/api/ventas/{seeded.SaleA}/cobros", new
                { importeUsd = 41m, fechaCobro = DateOnly.FromDateTime(DateTime.Today), monedaCobro = "USD", medioPago = "Efectivo" });
            Assert.Equal(HttpStatusCode.Conflict, tooLarge.StatusCode);

            var chargeId = detail.GetProperty("cobros")[0].GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK, (await adminA.PostAsJsonAsync(
                $"/api/ventas/{seeded.SaleA}/cobros/{chargeId}/anular", new { motivo = "Carga duplicada" })).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, (await adminA.PostAsJsonAsync(
                $"/api/ventas/{seeded.SaleA}/cobros/{chargeId}/anular", new { motivo = "Otra vez" })).StatusCode);
            detail = await adminA.GetFromJsonAsync<JsonElement>($"/api/ventas/{seeded.SaleA}/cobros");
            Assert.Equal(70m, detail.GetProperty("saldoUsd").GetDecimal());
            var account = await adminA.GetFromJsonAsync<JsonElement>($"/api/clientes/{seeded.ClientA}/cuenta-corriente");
            Assert.Equal(74m, account.GetProperty("resumen").GetProperty("deudaTotalUsd").GetDecimal());
            Assert.NotEmpty(account.GetProperty("movimientos").EnumerateArray());

            var parallel = Enumerable.Range(0, 8).Select(_ => adminA.PostAsJsonAsync(
                $"/api/ventas/{seeded.SmallSale}/cobros", new
                { importeUsd = 1m, fechaCobro = DateOnly.FromDateTime(DateTime.Today), monedaCobro = "USD", medioPago = "Efectivo" }));
            var responses = await Task.WhenAll(parallel);
            Assert.Equal(4, responses.Count(x => x.StatusCode == HttpStatusCode.Created));
            var small = await adminA.GetFromJsonAsync<JsonElement>($"/api/ventas/{seeded.SmallSale}/cobros");
            Assert.Equal(0m, small.GetProperty("saldoUsd").GetDecimal());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static async Task<(Guid BranchA, Guid BranchB, Guid ClientA, Guid SaleA, Guid SaleB, Guid SmallSale)> Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tax = await db.AlicuotasIva.IgnoreQueryFilters().FirstAsync();
        var admin = await db.Usuarios.IgnoreQueryFilters().SingleAsync(x => x.NombreUsuario == "admin");
        admin.DebeCambiarPassword = false;
        var da = new Deposito { Nombre = "Principal A", Activo = true };
        var dbb = new Deposito { Nombre = "Principal B", Activo = true };
        var a = new Sucursal { Nombre = $"Cobros A {Guid.NewGuid():N}", DepositoPropioId = da.Id, Activo = true };
        var b = new Sucursal { Nombre = $"Cobros B {Guid.NewGuid():N}", DepositoPropioId = dbb.Id, Activo = true };
        db.AddRange(da, dbb, a, b); await db.SaveChangesAsync();
        da.SucursalId = a.Id; dbb.SucursalId = b.Id; await db.SaveChangesAsync();
        var ca = NewClient(a.Id, "Cobros A"); var cb = NewClient(b.Id, "Cobros B");
        var pa = NewProduct(da.Id, "Cobros A"); var pb = NewProduct(dbb.Id, "Cobros B");
        db.AddRange(ca, cb, pa, pb); await db.SaveChangesAsync();
        var saleA = NewSale(a.Id, da.Id, ca.Id, pa.Id, tax.Id); saleA.PrecioTotal = 100; saleA.MontoEntrega = 20; saleA.SeguimientoSaldo = true;
        var saleB = NewSale(b.Id, dbb.Id, cb.Id, pb.Id, tax.Id); saleB.PrecioTotal = 100; saleB.MontoEntrega = 20; saleB.SeguimientoSaldo = true;
        var small = NewSale(a.Id, da.Id, ca.Id, pa.Id, tax.Id); small.PrecioTotal = 4; small.MontoEntrega = 0; small.SeguimientoSaldo = true;
        db.AddRange(saleA, saleB, small); await db.SaveChangesAsync();
        return (a.Id, b.Id, ca.Id, saleA.Id, saleB.Id, small.Id);
    }
}
