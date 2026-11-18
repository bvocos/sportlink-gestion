using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Auth;
using Api.Features.Remitos;
using Api.Features.Ventas;
using Api.Shared.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

public sealed class RemitoEndpointIntegrationTests
{
    [Fact]
    public async Task Remitos_AreNumberedScopedPriceFreeAndBuiltFromTheSale()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkRemitos_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(master) { Database = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            await using (var schema = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
                await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var data = await Seed(factory.Services);
            using var adminA = await LoginAdmin(factory, data.BranchA);
            using var adminB = await LoginAdmin(factory, data.BranchB);
            using var userA = await LoginUser(factory, "remito-user", "Test123!");

            var draftResponse = await adminA.GetAsync($"/api/ventas/{data.SaleA}/remito-borrador");
            Assert.Equal(HttpStatusCode.OK, draftResponse.StatusCode);
            var draftText = await draftResponse.Content.ReadAsStringAsync();
            AssertPriceFree(draftText);
            Assert.Contains(data.ProductAName, draftText);
            Assert.Contains("Empresa Fiscal A", draftText);
            Assert.Contains("20-12345678-6", draftText);
            Assert.Contains("Responsable inscripto", draftText);
            Assert.DoesNotContain("nota interna", draftText, StringComparison.OrdinalIgnoreCase);

            var a1 = await CreateRemito(adminA, data.SaleA, "Recibe A", maliciousLines: true);
            var a2 = await CreateRemito(adminA, data.SaleA, "Recibe A2");
            var b1 = await CreateRemito(adminB, data.SaleB, "Recibe B");
            Assert.Equal(1, a1.Numero);
            Assert.Equal(2, a2.Numero);
            Assert.Equal(1, b1.Numero);
            Assert.Equal("00012-00000001", a1.NumeroTexto);

            var listResponse = await adminA.GetAsync($"/api/ventas/{data.SaleA}/remitos");
            Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
            var listText = await listResponse.Content.ReadAsStringAsync();
            AssertPriceFree(listText);
            Assert.Equal(2, JsonDocument.Parse(listText).RootElement.GetArrayLength());

            Assert.Equal(HttpStatusCode.NotFound,
                (await adminA.GetAsync($"/api/ventas/{data.SaleB}/remitos")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await adminA.GetAsync($"/api/ventas/{data.SaleB}/remito-borrador")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await adminA.GetAsync($"/api/remitos/{b1.Id}/pdf")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,
                (await userA.GetAsync($"/api/remitos/{b1.Id}/pdf")).StatusCode);

            var cancelled = await adminA.PostAsJsonAsync($"/api/ventas/{data.CancelledSaleA}/remitos", Request("Cancelada"));
            Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
            Assert.Contains("cancelada", await cancelled.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

            var missingName = await adminA.PostAsJsonAsync($"/api/ventas/{data.SaleA}/remitos", Request(""));
            Assert.Equal(HttpStatusCode.BadRequest, missingName.StatusCode);
            Assert.Contains("receptorNombre", await missingName.Content.ReadAsStringAsync());

            var userCreated = await CreateRemito(userA, data.UserSaleA, "Usuario sin montos");
            var userPdf = await userA.GetAsync($"/api/remitos/{userCreated.Id}/pdf");
            Assert.Equal(HttpStatusCode.OK, userPdf.StatusCode);
            Assert.Equal("application/pdf", userPdf.Content.Headers.ContentType?.MediaType);
            var dniRemito = await adminA.PostAsJsonAsync($"/api/ventas/{data.SaleA}/remitos", new
            {
                receptorNombre = "Cliente DNI", receptorCuit = "12.345.678"
            });
            Assert.Equal(HttpStatusCode.Created, dniRemito.StatusCode);

            await using var verify = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
            var changedClient = await verify.Clientes.IgnoreQueryFilters().SingleAsync(x => x.Id == data.ClientA);
            changedClient.RazonSocial = "Razón social modificada";
            changedClient.Domicilio = "Otro domicilio";
            await verify.SaveChangesAsync();
            verify.ChangeTracker.Clear();
            var stored = await verify.Remitos.IgnoreQueryFilters().SingleAsync(x => x.Id == a1.Id);
            Assert.Equal("Recibe A", stored.ReceptorNombre);
            Assert.Equal("Calle 1", stored.ReceptorDomicilio);
            var lines = RemitoEndpoints.DeserializeLines(stored.LineasJson);
            var onlyLine = Assert.Single(lines);
            Assert.Equal(data.ProductAName, onlyLine.Producto);
            Assert.Equal(25.5m, onlyLine.CantidadM2);
            Assert.DoesNotContain("Producto falso", stored.LineasJson);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    [Fact]
    public async Task EightConcurrentCreates_ProduceOneThroughEightWithoutDuplicates()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkRemitosConcurrent_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(master) { Database = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            await using (var schema = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
                await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            var data = await Seed(factory.Services);
            using var adminA = await LoginAdmin(factory, data.BranchA);

            var responses = await Task.WhenAll(Enumerable.Range(1, 8).Select(index =>
                adminA.PostAsJsonAsync($"/api/ventas/{data.SaleA}/remitos", Request($"Receptor {index}"))));
            Assert.All(responses, x => Assert.Equal(HttpStatusCode.Created, x.StatusCode));
            var created = await Task.WhenAll(responses.Select(x => x.Content.ReadFromJsonAsync<CreatedRemito>()));
            Assert.Equal(Enumerable.Range(1, 8), created.Select(x => x!.Numero).Order());
            Assert.Equal(8, created.Select(x => x!.Numero).Distinct().Count());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static async Task<CreatedRemito> CreateRemito(HttpClient client, Guid saleId, string receiver,
        bool maliciousLines = false)
    {
        object body = maliciousLines
            ? new
            {
                receptorNombre = receiver, receptorDomicilio = "Calle 1", receptorLocalidad = "San Francisco",
                receptorProvincia = "Córdoba", receptorTelefono = "123", receptorCuit = "20-12345678-6",
                receptorCondicionIva = "Consumidor final", observaciones = "Prueba",
                lineas = new[] { new { producto = "Producto falso", color = "Rojo", cantidadM2 = 999m, precio = 1m } }
            }
            : Request(receiver);
        var response = await client.PostAsJsonAsync($"/api/ventas/{saleId}/remitos", body);
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"POST remito returned {(int)response.StatusCode}: {text}");
        AssertPriceFree(text);
        return JsonSerializer.Deserialize<CreatedRemito>(text, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static object Request(string receiver) => new
    {
        receptorNombre = receiver,
        receptorDomicilio = "Calle 1",
        receptorLocalidad = "San Francisco",
        receptorProvincia = "Córdoba",
        receptorTelefono = "3564-555555",
        receptorCuit = "20-12345678-6",
        receptorCondicionIva = "Consumidor final",
        observaciones = "Prueba de remito"
    };

    private static void AssertPriceFree(string json)
    {
        Assert.DoesNotContain("precio", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("costo", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("importe", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("total", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("formaPago", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cuota", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<RemitoSeed> Seed(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var branchA = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
        branchA.PuntoVentaAfip = 12;
        var depositB = new Deposito { Nombre = $"Remitos B {Guid.NewGuid():N}", Activo = true };
        db.Add(depositB);
        await db.SaveChangesAsync();
        var branchB = new Sucursal
        {
            Nombre = $"Remitos Branch B {Guid.NewGuid():N}", DepositoPropioId = depositB.Id,
            PuntoVentaAfip = 24, Activo = true, RazonSocial = "Sucursal B SA"
        };
        db.Add(branchB);
        await db.SaveChangesAsync();
        depositB.SucursalId = branchB.Id;
        var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin");
        admin.DebeCambiarPassword = false;
        var permissions = PermisosMatriz.Todos();
        permissions.Montos["ventas"] = false;
        var user = new Usuario
        {
            Nombre = "Usuario Remitos", NombreUsuario = "remito-user", Rol = "Usuario", Activo = true,
            DebeCambiarPassword = false, SucursalId = branchA.Id,
            PermisosJson = JsonSerializer.Serialize(permissions)
        };
        user.PasswordHash = hasher.HashPassword(user, "Test123!");
        var clientA = NewClient(branchA.Id, "Remito A");
        clientA.RazonSocial = "Empresa Fiscal A";
        clientA.TipoDocumento = TipoDocumentoCliente.Cuit;
        clientA.NumeroDocumento = "20123456786";
        clientA.CondicionIva = CondicionIvaCliente.ResponsableInscripto;
        clientA.Domicilio = "Calle Fiscal 123";
        var clientB = NewClient(branchB.Id, "Remito B");
        var productA = NewProduct(branchA.DepositoPropioId, "Remito A"); productA.ControlPorLotes = false;
        var productB = NewProduct(depositB.Id, "Remito B"); productB.ControlPorLotes = false;
        db.AddRange(user, clientA, clientB, productA, productB);
        await db.SaveChangesAsync();
        var tax = await db.AlicuotasIva.FirstAsync();
        var saleA = Sale(branchA.Id, branchA.DepositoPropioId, clientA.Id, productA, tax.Id, EstadoVenta.Confirmada, 25.5m);
        saleA.Observaciones = "nota interna que no debe imprimirse";
        var userSaleA = Sale(branchA.Id, branchA.DepositoPropioId, clientA.Id, productA, tax.Id, EstadoVenta.Entregada, 4m);
        var cancelledSaleA = Sale(branchA.Id, branchA.DepositoPropioId, clientA.Id, productA, tax.Id, EstadoVenta.Cancelada, 2m);
        var saleB = Sale(branchB.Id, depositB.Id, clientB.Id, productB, tax.Id, EstadoVenta.Confirmada, 5m);
        db.AddRange(saleA, userSaleA, cancelledSaleA, saleB);
        await db.SaveChangesAsync();
        return new(branchA.Id, branchB.Id, clientA.Id, saleA.Id, userSaleA.Id, cancelledSaleA.Id, saleB.Id,
            productA.Nombre);
    }

    private static Venta Sale(Guid branch, Guid deposit, Guid client, TipoCesped product, Guid tax,
        EstadoVenta status, decimal quantity)
    {
        var line = new VentaLineaCommand(product.Id, "Verde", quantity, 10, 20, quantity * 20);
        return new Venta
        {
            SucursalId = branch, DepositoId = deposit, ClienteId = client, TipoCespedId = product.Id,
            AlicuotaIvaId = tax, Color = "Verde", FechaVenta = DateOnly.FromDateTime(DateTime.Today),
            CantidadM2 = quantity, PrecioUnitario = 20, PrecioTotal = quantity * 20,
            CostoCompraUnitario = 10, CostoCompraTotal = quantity * 10, FormaPago = FormaPago.Contado,
            Estado = status, LineasJson = JsonSerializer.Serialize(new[] { line })
        };
    }

    private sealed record CreatedRemito(Guid Id, int Numero, string NumeroTexto);
    private sealed record RemitoSeed(Guid BranchA, Guid BranchB, Guid ClientA, Guid SaleA, Guid UserSaleA,
        Guid CancelledSaleA, Guid SaleB, string ProductAName);
}
