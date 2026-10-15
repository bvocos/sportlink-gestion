using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Auth;
using Api.Shared.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

public sealed class BranchReportingIsolationIntegrationTests
{
    [Fact]
    public async Task ReportingAndListingEndpoints_RespectTheActiveBranchOverHttp()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkBranchReporting_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(master) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var data = await Seed(factory.Services);
            using var userA = await LoginUser(factory, "branch-a", "Test123!", data.BranchB);
            using var adminA = await LoginAdmin(factory, data.BranchA);
            using var adminB = await LoginAdmin(factory, data.BranchB);
            using var adminNoBranch = await LoginAdmin(factory, null);

            await AssertDashboard(userA, data, isBranchA: true,
                $"?sucursalId={data.BranchB}");
            await AssertDashboard(adminA, data, isBranchA: true);
            await AssertDashboard(adminB, data, isBranchA: false);
            var emptyDashboard = await GetJson(adminNoBranch, "/api/dashboard");
            Assert.Equal(0, Decimal(emptyDashboard, "total", "facturacion"));
            Assert.Empty(emptyDashboard.GetProperty("ventas").EnumerateArray());
            Assert.Empty(emptyDashboard.GetProperty("filtros").GetProperty("clientes").EnumerateArray());
            Assert.Empty(emptyDashboard.GetProperty("filtros").GetProperty("productos").EnumerateArray());

            var foreignClientDashboard = await GetJson(userA,
                $"/api/dashboard?clienteId={data.ClientB}&estadoFinanciero=Rentable&sucursalId={data.BranchB}");
            Assert.Equal(0, foreignClientDashboard.GetProperty("total").GetProperty("cantidad").GetInt32());
            Assert.Empty(foreignClientDashboard.GetProperty("ventas").EnumerateArray());

            await AssertProfitability(userA, 2, 1500, 600, "ClienteA", "ClienteB",
                $"?sucursalId={data.BranchB}");
            await AssertProfitability(adminA, 2, 1500, 600, "ClienteA", "ClienteB");
            await AssertProfitability(adminB, 1, 7000, 3000, "ClienteB", "ClienteA");
            await AssertProfitability(adminNoBranch, 0, 0, 0, "", "ClienteA");
            var searchedForeignProfit = await GetJson(userA, "/api/rentabilidad?buscar=ClienteB");
            Assert.Equal(0, searchedForeignProfit.GetProperty("total").GetInt32());
            var csv = await userA.GetStringAsync("/api/rentabilidad/exportar");
            Assert.Contains("ClienteA", csv);
            Assert.DoesNotContain("ClienteB", csv);

            await AssertCash(userA, 70, 2, false, $"?sucursalId={data.BranchB}");
            await AssertCash(adminB, 700, 1, true);
            var rejected = await userA.PostAsJsonAsync("/api/caja/movimientos",
                new { tipo = "Retiro", monto = 100m, concepto = "No debe usar saldo B", sucursalId = data.BranchB });
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await userA.PostAsJsonAsync("/api/caja/movimientos",
                new { tipo = "Retiro", monto = 50m, concepto = "Retiro A", sucursalId = data.BranchB })).StatusCode);
            await AssertCash(userA, 20, 3, false);
            await AssertCash(adminB, 700, 1, true);
            Assert.Equal(HttpStatusCode.Created, (await userA.PostAsJsonAsync("/api/caja/movimientos",
                new { tipo = "Ingreso", monto = 10m, concepto = "Ingreso forzado a A", sucursalId = data.BranchB })).StatusCode);
            await AssertCash(userA, 30, 4, false);
            await AssertCash(adminB, 700, 1, true);

            await AssertExpenses(userA, 2, 23, ["Gasto A1", "Gasto A2"]);
            var foreignExpenseSearch = await GetJson(userA, "/api/gastos?buscar=Gasto%20B");
            Assert.Equal(0, foreignExpenseSearch.GetProperty("total").GetInt32());
            await AssertExpenses(adminB, 1, 700, ["Gasto B"]);
            await AssertExpenses(adminNoBranch, 0, 0, []);
            Assert.Equal(HttpStatusCode.Created, (await userA.PostAsJsonAsync("/api/gastos", Expense("Gasto A3", 5))).StatusCode);
            await AssertExpenses(userA, 3, 28, ["Gasto A1", "Gasto A2", "Gasto A3"]);
            await AssertExpenses(adminB, 1, 700, ["Gasto B"]);
            Assert.Equal(HttpStatusCode.BadRequest,
                (await adminNoBranch.PostAsJsonAsync("/api/gastos", Expense("Sin sucursal", 1))).StatusCode);

            var installments = await GetJson(userA, $"/api/cuotas/pendientes?sucursalId={data.BranchB}");
            var onlyInstallment = Assert.Single(installments.EnumerateArray());
            Assert.Equal(300, onlyInstallment.GetProperty("saldoPendiente").GetDecimal());
            Assert.Contains("ClienteA", onlyInstallment.GetProperty("cliente").GetString());
            var installmentSummary = await GetJson(userA, $"/api/cuotas/pendientes/resumen?sucursalId={data.BranchB}");
            Assert.Equal(1, installmentSummary.GetProperty("cantidad").GetInt32());
            Assert.Equal(300, installmentSummary.GetProperty("totalPendiente").GetDecimal());

            var budgets = await GetJson(userA, "/api/presupuestos");
            Assert.Equal(data.BudgetA, Assert.Single(budgets.EnumerateArray()).GetProperty("id").GetGuid());
            var clients = await GetJson(userA, "/api/clientes");
            Assert.Equal(1, clients.GetProperty("totalCount").GetInt32());
            Assert.Equal(data.ClientA, Assert.Single(clients.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static async Task AssertDashboard(HttpClient client, ReportingSeed data, bool isBranchA,
        string suffix = "")
    {
        var json = await GetJson(client, "/api/dashboard" + suffix);
        var expectedFacturation = isBranchA ? 1500m : 7000m;
        var expectedGain = isBranchA ? 600m : 3000m;
        Assert.Equal(isBranchA ? 2 : 1, json.GetProperty("total").GetProperty("cantidad").GetInt32());
        Assert.Equal(expectedFacturation, Decimal(json, "total", "facturacion"));
        Assert.Equal(expectedGain, Decimal(json, "total", "gananciaNeta"));
        Assert.Equal(isBranchA ? 70m : 700m, json.GetProperty("saldo").GetDecimal());
        Assert.Equal(1, json.GetProperty("cuotasPendientes").GetInt32());
        Assert.Equal(isBranchA ? 1 : 0, json.GetProperty("entregasPendientes").GetInt32());
        var text = json.ToString();
        Assert.Contains(isBranchA ? "ClienteA" : "ClienteB", text);
        Assert.DoesNotContain(isBranchA ? "ClienteB" : "ClienteA", text);
        Assert.Contains(isBranchA ? data.ProductNameA : data.ProductNameB, text);
        Assert.DoesNotContain(isBranchA ? data.ProductNameB : data.ProductNameA, text);
        Assert.Equal(isBranchA ? 2 : 1, json.GetProperty("ventas").GetArrayLength());
        Assert.NotEmpty(json.GetProperty("series").EnumerateArray());
    }

    private static async Task AssertProfitability(HttpClient client, int count, decimal revenue, decimal gain,
        string included, string excluded, string suffix = "")
    {
        var json = await GetJson(client, "/api/rentabilidad" + suffix);
        Assert.Equal(count, json.GetProperty("totales").GetProperty("cantidadVentas").GetInt32());
        Assert.Equal(revenue, Decimal(json, "totales", "facturacionTotal"));
        Assert.Equal(gain, Decimal(json, "totales", "gananciaNetaTotal"));
        Assert.Equal(count, json.GetProperty("items").GetArrayLength());
        var text = json.ToString();
        if (included.Length > 0) Assert.Contains(included, text);
        Assert.DoesNotContain(excluded, text);
    }

    private static async Task AssertCash(HttpClient client, decimal balance, int count, bool hasBranches,
        string suffix = "")
    {
        var json = await GetJson(client, "/api/caja" + suffix);
        Assert.Equal(balance, json.GetProperty("saldo").GetDecimal());
        Assert.Equal(count, json.GetProperty("movimientos").GetArrayLength());
        Assert.Equal(hasBranches, json.GetProperty("sucursales").GetArrayLength() > 0);
    }

    private static async Task AssertExpenses(HttpClient client, int count, decimal total, string[] descriptions)
    {
        var json = await GetJson(client, "/api/gastos");
        Assert.Equal(count, json.GetProperty("total").GetInt32());
        Assert.Equal(total, json.GetProperty("totalImporte").GetDecimal());
        Assert.Equal(descriptions.Order(), json.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("descripcion").GetString()!).Order());
    }

    private static object Expense(string description, decimal amount) => new
    {
        fecha = DateOnly.FromDateTime(DateTime.Today), categoria = "Prueba", descripcion = description,
        importe = amount, observaciones = (string?)null
    };

    private static async Task<JsonElement> GetJson(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"GET {path} returned {(int)response.StatusCode} {response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static decimal Decimal(JsonElement json, string parent, string property) =>
        json.GetProperty(parent).GetProperty(property).GetDecimal();

    private static async Task<ReportingSeed> Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var branchA = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
        var depositB = new Deposito { Nombre = $"Reporting B {Guid.NewGuid():N}", Activo = true };
        db.Add(depositB); await db.SaveChangesAsync();
        var branchB = new Sucursal { Nombre = $"Reporting Branch B {Guid.NewGuid():N}", DepositoPropioId = depositB.Id, Activo = true };
        db.Add(branchB); await db.SaveChangesAsync(); depositB.SucursalId = branchB.Id;
        var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin");
        admin.DebeCambiarPassword = false;
        var user = new Usuario
        {
            Nombre = "Branch A", NombreUsuario = "branch-a", Rol = "Usuario", Activo = true,
            DebeCambiarPassword = false, SucursalId = branchA.Id,
            PermisosJson = JsonSerializer.Serialize(PermisosMatriz.Todos())
        };
        user.PasswordHash = hasher.HashPassword(user, "Test123!");
        var clientA = NewClient(branchA.Id, "ClienteA");
        var clientB = NewClient(branchB.Id, "ClienteB");
        var productA = NewProduct(branchA.DepositoPropioId, "ProductoA");
        var productB = NewProduct(depositB.Id, "ProductoB");
        db.AddRange(user, clientA, clientB, productA, productB); await db.SaveChangesAsync();
        var tax = await db.AlicuotasIva.FirstAsync(x => x.Porcentaje == 0);
        var threshold = await db.Configuraciones.SingleAsync(x => x.Clave == "UmbralMuyRentable");
        threshold.ValorDecimal = 0.5m;

        var saleA1 = Sale(branchA.Id, branchA.DepositoPropioId, clientA.Id, productA.Id, tax.Id,
            1000, 600, EstadoVenta.Confirmada);
        var saleA2 = Sale(branchA.Id, branchA.DepositoPropioId, clientA.Id, productA.Id, tax.Id,
            500, 300, EstadoVenta.Futura);
        var saleB = Sale(branchB.Id, depositB.Id, clientB.Id, productB.Id, tax.Id,
            7000, 4000, EstadoVenta.Confirmada);
        db.AddRange(saleA1, saleA2, saleB); await db.SaveChangesAsync();
        db.AddRange(
            Installment(branchA.Id, branchA.DepositoPropioId, saleA1.Id, clientA.Id, 300),
            Installment(branchB.Id, depositB.Id, saleB.Id, clientB.Id, 2000),
            Cash(branchA.Id, TipoMovimiento.Ingreso, 100, "Ingreso A"),
            Cash(branchA.Id, TipoMovimiento.Retiro, 30, "Retiro A"),
            Cash(branchB.Id, TipoMovimiento.Ingreso, 700, "Ingreso B"),
            ExpenseEntity(branchA.Id, "Gasto A1", 11), ExpenseEntity(branchA.Id, "Gasto A2", 12),
            ExpenseEntity(branchB.Id, "Gasto B", 700));
        var budgetA = Budget(1, branchA.Id, branchA.DepositoPropioId, clientA.Id, productA);
        var budgetB = Budget(1, branchB.Id, depositB.Id, clientB.Id, productB);
        db.AddRange(budgetA, budgetB); await db.SaveChangesAsync();
        return new(branchA.Id, branchB.Id, clientA.Id, clientB.Id, budgetA.Id,
            productA.Nombre, productB.Nombre);
    }

    private static Venta Sale(Guid branch, Guid deposit, Guid client, Guid product, Guid tax,
        decimal price, decimal cost, EstadoVenta status) => new()
    {
        SucursalId = branch, DepositoId = deposit, ClienteId = client, TipoCespedId = product,
        AlicuotaIvaId = tax, FechaVenta = DateOnly.FromDateTime(DateTime.Today), CantidadM2 = 1,
        PrecioUnitario = price, PrecioTotal = price, CostoCompraUnitario = cost, CostoCompraTotal = cost,
        GananciaBruta = price - cost, GananciaNeta = price - cost, Margen = (price - cost) / price,
        MontoEntrega = price, FormaPago = FormaPago.Contado, Estado = status
    };

    private static Cuota Installment(Guid branch, Guid deposit, Guid sale, Guid client, decimal amount) => new()
    {
        SucursalId = branch, DepositoId = deposit, VentaId = sale, ClienteId = client, Numero = 1,
        FechaVencimiento = DateOnly.FromDateTime(DateTime.Today.AddDays(10)), ImportePactado = amount,
        ImportePagado = 0, Estado = EstadoCuota.Pendiente
    };

    private static MovimientoCaja Cash(Guid branch, TipoMovimiento type, decimal amount, string concept) => new()
        { SucursalId = branch, Tipo = type, Monto = amount, Concepto = concept, Usuario = "test", Fecha = DateTimeOffset.UtcNow };

    private static Gasto ExpenseEntity(Guid branch, string description, decimal amount) => new()
        { SucursalId = branch, Fecha = DateOnly.FromDateTime(DateTime.Today), Categoria = "Prueba", Descripcion = description, Importe = amount };

    private static Presupuesto Budget(int number, Guid branch, Guid deposit, Guid client, TipoCesped product) => new()
    {
        Numero = number, SucursalId = branch, DepositoId = deposit, ClienteId = client,
        Fecha = DateOnly.FromDateTime(DateTime.Today), ValidezHasta = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
        Lineas = [new PresupuestoLinea { TipoCespedId = product.Id, Producto = product.Nombre,
            CantidadM2 = 1, PrecioContadoM2 = 20, PrecioFinanciadoM2 = 22, TotalContado = 20, TotalFinanciado = 22 }]
    };

    private sealed record ReportingSeed(Guid BranchA, Guid BranchB, Guid ClientA, Guid ClientB,
        Guid BudgetA, string ProductNameA, string ProductNameB);
}
