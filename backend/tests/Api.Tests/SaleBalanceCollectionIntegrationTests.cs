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

            var lowerDelivery = await adminA.PutAsJsonAsync($"/api/ventas/{seeded.FullSale}", new
            {
                clienteId = seeded.ClientA, fechaVenta = DateOnly.FromDateTime(DateTime.Today),
                tipoCespedId = seeded.ProductA, cantidadM2 = 10m, precioUnitario = 10m,
                precioTotal = 100m, montoEntrega = 50m, formaPago = "Contado", cantidadCuotas = (int?)null,
                estado = "Confirmada", fechaEntregaEstimada = (DateOnly?)null, observaciones = "Entrega corregida",
                costoCompraUnitario = 5m, costoEnvio = 0m, otrosCostos = 0m, alicuotaIvaId = seeded.TaxId,
                depositoId = seeded.DepositA, monedaEntrega = "USD"
            });
            Assert.True(lowerDelivery.StatusCode == HttpStatusCode.OK,
                $"Expected 200, got {(int)lowerDelivery.StatusCode}: {await lowerDelivery.Content.ReadAsStringAsync()}");
            var lowered = await adminA.GetFromJsonAsync<JsonElement>($"/api/ventas/{seeded.FullSale}");
            Assert.Equal(50m, lowered.GetProperty("montoEntrega").GetDecimal());
            Assert.True(lowered.GetProperty("seguimientoSaldo").GetBoolean());
            Assert.Equal(50m, lowered.GetProperty("saldoUsd").GetDecimal());
            using (var scope = factory.Services.CreateScope())
            {
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var deliveryMovements = await scopedDb.MovimientosCaja.IgnoreQueryFilters()
                    .Where(x => x.VentaId == seeded.FullSale && x.CuotaId == null && x.CobroVentaId == null).ToListAsync();
                Assert.Single(deliveryMovements);
                Assert.Equal(50m, deliveryMovements[0].Monto);
            }

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
            Assert.Equal(124m, account.GetProperty("resumen").GetProperty("deudaTotalUsd").GetDecimal());
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

    private static async Task<(Guid BranchA, Guid BranchB, Guid ClientA, Guid SaleA, Guid SaleB, Guid SmallSale,
        Guid FullSale, Guid ProductA, Guid DepositA, Guid TaxId)> Seed(IServiceProvider services)
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
        var pa = NewProduct(da.Id, "Cobros A"); pa.ControlPorLotes = false;
        var pb = NewProduct(dbb.Id, "Cobros B");
        db.AddRange(ca, cb, pa, pb); await db.SaveChangesAsync();
        var saleA = NewSale(a.Id, da.Id, ca.Id, pa.Id, tax.Id); saleA.PrecioTotal = 100; saleA.MontoEntrega = 20; saleA.SeguimientoSaldo = true;
        var saleB = NewSale(b.Id, dbb.Id, cb.Id, pb.Id, tax.Id); saleB.PrecioTotal = 100; saleB.MontoEntrega = 20; saleB.SeguimientoSaldo = true;
        var small = NewSale(a.Id, da.Id, ca.Id, pa.Id, tax.Id); small.PrecioTotal = 4; small.MontoEntrega = 0; small.SeguimientoSaldo = true;
        var full = NewSale(a.Id, da.Id, ca.Id, pa.Id, tax.Id); full.PrecioTotal = 100; full.MontoEntrega = 100; full.SeguimientoSaldo = false;
        db.AddRange(saleA, saleB, small, full); await db.SaveChangesAsync();
        db.MovimientosStock.Add(new MovimientoStock { DepositoId = da.Id, TipoCespedId = pa.Id,
            Tipo = TipoMovimientoStock.Ingreso, CantidadM2 = 1_000m, Fecha = DateTime.UtcNow, Usuario = "test" });
        db.MovimientosCaja.Add(new MovimientoCaja { SucursalId = a.Id, VentaId = full.Id, Tipo = TipoMovimiento.Ingreso,
            Monto = 100m, Fecha = DateTimeOffset.UtcNow, Concepto = $"Entrega inicial venta {full.Id}", Usuario = "test" });
        await db.SaveChangesAsync();
        return (a.Id, b.Id, ca.Id, saleA.Id, saleB.Id, small.Id, full.Id, pa.Id, da.Id, tax.Id);
    }
}
