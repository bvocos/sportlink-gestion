using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Auth;
using Api.Shared.Database;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

public sealed class SucursalEndpointIsolationIntegrationTests
{
    [Fact]
    public async Task OperationalEndpoints_EnforceActiveBranchOverHttp()
    {
        var masterConnection = Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION");
        if (string.IsNullOrWhiteSpace(masterConnection)) return;

        var databaseName = $"SportlinkBranchTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = databaseName }.ConnectionString;
        await CreateDatabase(masterConnection, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient(); // Inicia Program y SeedData.
            var data = await Seed(factory.Services);

            using var user = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            Assert.Equal(HttpStatusCode.OK, (await user.PostAsJsonAsync("/api/auth/login", new { usuario = "branch-a", password = "Test123!" })).StatusCode);
            user.DefaultRequestHeaders.Add("X-Sucursal-Id", data.BranchB.ToString()); // Debe ignorarse para no administradores.

            var userSales = await user.GetFromJsonAsync<JsonElement>("/api/ventas");
            Assert.Equal(data.SaleA, Assert.Single(userSales.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
            await AssertNotFound(user.GetAsync($"/api/ventas/{data.SaleB}"));
            await AssertNotFound(user.PutAsJsonAsync($"/api/ventas/{data.SaleB}", SaleRequest(data)));
            await AssertNotFound(user.DeleteAsync($"/api/ventas/{data.SaleB}"));
            await AssertNotFound(user.PostAsync($"/api/ventas/{data.SaleB}/entregar", null));
            await AssertNotFound(user.PostAsync($"/api/ventas/{data.SaleB}/confirmar", null));
            await AssertNotFound(user.PostAsync($"/api/ventas/{data.SaleB}/cancelar", null));

            await AssertNotFound(user.PutAsJsonAsync($"/api/cuotas/{data.InstallmentB}/vencimiento", new { fechaVencimiento = DateOnly.FromDateTime(DateTime.Today.AddDays(30)) }));
            await AssertNotFound(user.PostAsJsonAsync($"/api/cuotas/{data.InstallmentB}/pagos", new { importe = 10m, medioPago = "Transferencia", fechaPago = DateOnly.FromDateTime(DateTime.Today) }));
            await AssertNotFound(user.PostAsync($"/api/cuotas/{data.InstallmentB}/anular-pago", null));
            await AssertNotFound(user.GetAsync($"/api/cuotas/{data.InstallmentB}/recibo"));
            await AssertNotFound(user.GetAsync($"/api/cuotas/{data.InstallmentB}/comprobante"));
            await AssertNotFound(user.PutAsJsonAsync($"/api/caja/movimientos/{data.CashMovementB}/observacion", new { observacion = "No permitido" }));

            await AssertNotFound(user.GetAsync($"/api/presupuestos/{data.BudgetB}"));
            await AssertNotFound(user.GetAsync($"/api/presupuestos/{data.BudgetB}/pdf"));
            await AssertNotFound(user.DeleteAsync($"/api/presupuestos/{data.BudgetB}"));
            await AssertNotFound(user.PutAsJsonAsync($"/api/clientes/{data.ClientB}", ClientRequest()));
            await AssertNotFound(user.DeleteAsync($"/api/clientes/{data.ClientB}"));
            await AssertNotFound(user.PutAsJsonAsync($"/api/gastos/{data.ExpenseB}", ExpenseRequest()));
            await AssertNotFound(user.DeleteAsync($"/api/gastos/{data.ExpenseB}"));

            var lots = await user.GetFromJsonAsync<JsonElement>($"/api/stock/lotes?depositoId={data.DepositB}");
            Assert.Empty(lots.EnumerateArray());
            await AssertNotFound(user.GetAsync($"/api/stock/lotes/buscar-por-codigo?codigoBarra={data.BarcodeB}"));

            using var admin = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", password = "Admin123!" })).StatusCode);
            admin.DefaultRequestHeaders.Add("X-Sucursal-Id", data.BranchB.ToString());
            var adminSales = await admin.GetFromJsonAsync<JsonElement>("/api/ventas");
            Assert.Equal(data.SaleB, Assert.Single(adminSales.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await DropDatabase(masterConnection, databaseName);
        }
    }

    private static async Task AssertNotFound(Task<HttpResponseMessage> responseTask) =>
        Assert.Equal(HttpStatusCode.NotFound, (await responseTask).StatusCode);

    private static object ClientRequest() => new { nombre = "Otro", apellido = "Cliente", telefono = "3515555555", correo = (string?)null, localidad = "Córdoba", provincia = "Córdoba", localidadId = (string?)null, provinciaId = (string?)null, tipo = "Particular", fechaPrimerContacto = DateOnly.FromDateTime(DateTime.Today), observaciones = (string?)null };
    private static object ExpenseRequest() => new { fecha = DateOnly.FromDateTime(DateTime.Today), categoria = "Prueba", descripcion = "No modificar", importe = 10m, observaciones = (string?)null };
    private static object SaleRequest(SeededData d) => new { clienteId = d.ClientB, fechaVenta = DateOnly.FromDateTime(DateTime.Today), tipoCespedId = d.ProductB, cantidadM2 = 1m, precioUnitario = 20m, precioTotal = 20m, montoEntrega = 0m, formaPago = "Contado", cantidadCuotas = (int?)null, estado = "Confirmada", fechaEntregaEstimada = (DateOnly?)null, observaciones = "No modificar", costoCompraUnitario = 10m, costoEnvio = 0m, otrosCostos = 0m, alicuotaIvaId = d.TaxId, depositoId = d.DepositB, lineas = new[] { new { tipoCespedId = d.ProductB, color = (string?)null, cantidadM2 = 1m, precioCompraM2 = 10m, precioVentaM2 = 20m, total = 20m } } };

    private static async Task<SeededData> Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var branchA = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
        var depositB = new Deposito { Nombre = $"Dep B {Guid.NewGuid():N}", Activo = true };
        db.Depositos.Add(depositB); await db.SaveChangesAsync();
        var branchB = new Sucursal { Nombre = $"Sucursal B {Guid.NewGuid():N}", DepositoPropioId = depositB.Id, Activo = true };
        db.Sucursales.Add(branchB); await db.SaveChangesAsync();
        depositB.SucursalId = branchB.Id;

        var permissions = PermisosMatriz.Todos();
        var user = new Usuario { Nombre = "Branch A", NombreUsuario = "branch-a", Rol = "Usuario", PermisosJson = JsonSerializer.Serialize(permissions), Activo = true, DebeCambiarPassword = false, SucursalId = branchA.Id };
        user.PasswordHash = hasher.HashPassword(user, "Test123!");
        var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin"); admin.DebeCambiarPassword = false;
        var clientA = NewClient(branchA.Id, "A"); var clientB = NewClient(branchB.Id, "B");
        var productA = NewProduct(branchA.DepositoPropioId, "A"); var productB = NewProduct(depositB.Id, "B");
        db.AddRange(user, clientA, clientB, productA, productB); await db.SaveChangesAsync();
        var tax = await db.AlicuotasIva.FirstAsync();
        var saleA = NewSale(branchA.Id, branchA.DepositoPropioId, clientA.Id, productA.Id, tax.Id);
        var saleB = NewSale(branchB.Id, depositB.Id, clientB.Id, productB.Id, tax.Id);
        db.Ventas.AddRange(saleA, saleB); await db.SaveChangesAsync();
        var installmentB = new Cuota { SucursalId = branchB.Id, DepositoId = depositB.Id, VentaId = saleB.Id, ClienteId = clientB.Id, Numero = 1, FechaVencimiento = DateOnly.FromDateTime(DateTime.Today), FechaPago = DateOnly.FromDateTime(DateTime.Today), ImportePactado = 100, ImportePagado = 100, MedioPago = "Transferencia", Estado = EstadoCuota.Pagada };
        var movementB = new MovimientoCaja { SucursalId = branchB.Id, VentaId = saleB.Id, CuotaId = installmentB.Id, Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow, Monto = 100, Concepto = "Pago", NumeroRecibo = 1 };
        var budgetB = new Presupuesto { SucursalId = branchB.Id, DepositoId = depositB.Id, ClienteId = clientB.Id, Fecha = DateOnly.FromDateTime(DateTime.Today), ValidezHasta = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), Lineas = [new PresupuestoLinea { TipoCespedId = productB.Id, Producto = productB.Nombre, CantidadM2 = 1, PrecioContadoM2 = 20, PrecioFinanciadoM2 = 22, TotalContado = 20, TotalFinanciado = 22 }] };
        var expenseB = new Gasto { SucursalId = branchB.Id, Fecha = DateOnly.FromDateTime(DateTime.Today), Categoria = "Prueba", Descripcion = "Gasto B", Importe = 10 };
        var lotB = new LoteStock { DepositoId = depositB.Id, TipoCespedId = productB.Id, FechaIngreso = DateTime.UtcNow, Usuario = "test", Rollos = [new Rollo { Posicion = 'A', CodigoBarra = $"B-{Guid.NewGuid():N}", CantidadM2 = 1 }, new Rollo { Posicion = 'B', CodigoBarra = $"B-{Guid.NewGuid():N}", CantidadM2 = 1 }, new Rollo { Posicion = 'C', CodigoBarra = $"B-{Guid.NewGuid():N}", CantidadM2 = 1 }] };
        db.AddRange(installmentB, movementB, budgetB, expenseB, lotB); await db.SaveChangesAsync();
        return new(branchB.Id, depositB.Id, saleA.Id, saleB.Id, installmentB.Id, movementB.Id, budgetB.Id, clientB.Id, expenseB.Id, productB.Id, tax.Id, lotB.Rollos[0].CodigoBarra);
    }

    private static Cliente NewClient(Guid branch, string suffix) => new() { SucursalId = branch, Nombre = "Cliente", Apellido = suffix, Telefono = "3515555555", Localidad = "Córdoba", Provincia = "Córdoba", Tipo = TipoCliente.Particular, FechaPrimerContacto = DateOnly.FromDateTime(DateTime.Today) };
    private static TipoCesped NewProduct(Guid deposit, string suffix) => new() { DepositoId = deposit, Nombre = $"Producto {suffix} {Guid.NewGuid():N}", PrecioVentaM2 = 20, PrecioContadoM2 = 20, PrecioFinanciadoM2 = 22, CostoM2 = 10, Activo = true, ControlPorLotes = true };
    private static Venta NewSale(Guid branch, Guid deposit, Guid client, Guid product, Guid tax) => new() { SucursalId = branch, DepositoId = deposit, ClienteId = client, TipoCespedId = product, AlicuotaIvaId = tax, FechaVenta = DateOnly.FromDateTime(DateTime.Today), CantidadM2 = 1, PrecioUnitario = 20, PrecioTotal = 20, CostoCompraUnitario = 10, CostoCompraTotal = 10, FormaPago = FormaPago.Contado, Estado = EstadoVenta.Confirmada };

    private sealed record SeededData(Guid BranchB, Guid DepositB, Guid SaleA, Guid SaleB, Guid InstallmentB, Guid CashMovementB, Guid BudgetB, Guid ClientB, Guid ExpenseB, Guid ProductB, Guid TaxId, string BarcodeB);

    private static async Task CreateDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = $"CREATE DATABASE [{name}]"; await command.ExecuteNonQueryAsync();
    }
    private static async Task DropDatabase(string master, string name)
    {
        await using var connection = new SqlConnection(master); await connection.OpenAsync();
        await using var command = connection.CreateCommand(); command.CommandText = $"ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{name}]"; await command.ExecuteNonQueryAsync();
    }

    private sealed class BranchApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["SkipDatabaseMigrations"] = "true" }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>(); services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
            });
        }
    }
}
