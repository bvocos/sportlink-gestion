using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Auth;
using Api.Shared.Database;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

public sealed class ImportacionEndpointIntegrationTests
{
    [Fact]
    public async Task BatchReception_IsAtomicAndUpdatesImportationBalances()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip(); if (master is null) return;
        var name = $"SportlinkReception_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(master) { Database = name }.ConnectionString;
        await CreateDatabase(master, name);
        try
        {
            await using (var schema = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection); _ = factory.CreateClient();
            var seed = await Seed(factory.Services);
            Guid secondProduct;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var product = NewProduct(seed.DepositA, "Importado recepción");
                product.EsImportado = true; product.ControlPorLotes = false;
                product.ColoresJson = JsonSerializer.Serialize(new[] { "Verde", "Arena" });
                db.Add(product); await db.SaveChangesAsync(); secondProduct = product.Id;
            }
            using var user = await LoginUser(factory, "imports-a", "Test123!");
            var response = await user.PostAsJsonAsync("/api/importaciones/recepcion", new
            {
                importacionId = (Guid?)null,
                nueva = ImportRequest("REC-001"), depositoId = seed.DepositA, observaciones = "Contenedor completo",
                lineas = new object[] { new { tipoCespedId = seed.ImportedA, cantidadM2 = 10m },
                    new { tipoCespedId = secondProduct, variantes = new[] { new { color = "verde", cantidadM2 = 15m }, new { color = "Arena", cantidadM2 = 5m } } } }
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(30m, created.GetProperty("totalM2").GetDecimal());
            Assert.Equal(3, created.GetProperty("lineasIngresadas").GetInt32());
            var id = created.GetProperty("importacionId").GetGuid();
            var detail = await user.GetFromJsonAsync<JsonElement>($"/api/importaciones/{id}");
            Assert.Equal(30m, detail.GetProperty("saldos").EnumerateArray().Sum(x => x.GetProperty("ingresadoM2").GetDecimal()));
            Assert.Equal(30m, detail.GetProperty("saldos").EnumerateArray().Sum(x => x.GetProperty("saldoM2").GetDecimal()));
            Assert.Contains(detail.GetProperty("saldos").EnumerateArray(), x => x.GetProperty("color").GetString() == "Verde" && x.GetProperty("saldoM2").GetDecimal() == 15m);

            var invalidColors = await user.PostAsJsonAsync("/api/importaciones/recepcion", new
            {
                importacionId = id, depositoId = seed.DepositA,
                lineas = new[] { new { tipoCespedId = secondProduct, variantes = new[] { new { color = "Verde", cantidadM2 = 1m }, new { color = "verde", cantidadM2 = 2m } } } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidColors.StatusCode);

            var invalid = await user.PostAsJsonAsync("/api/importaciones/recepcion", new
            {
                importacionId = (Guid?)null,
                nueva = ImportRequest("REC-ATOMIC"), depositoId = seed.DepositA,
                lineas = new[] { new { tipoCespedId = seed.ImportedA, cantidadM2 = 5m }, new { tipoCespedId = seed.NormalA, cantidadM2 = 5m } }
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            await using var verifyScope = factory.Services.CreateAsyncScope();
            var verify = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await verify.Importaciones.IgnoreQueryFilters().AnyAsync(x => x.Codigo == "REC-ATOMIC"));
        }
        finally { NpgsqlConnection.ClearAllPools(); await DropDatabase(master, name); }
    }

    [Fact]
    public async Task Importations_AreBranchScopedAndStockMovesBetweenBuckets()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip(); if (master is null) return;
        var name = $"SportlinkImports_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(master) { Database = name }.ConnectionString;
        await CreateDatabase(master, name);
        try
        {
            await using (var schema = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection); _ = factory.CreateClient();
            var seed = await Seed(factory.Services);
            using var userA = await LoginUser(factory, "imports-a", "Test123!");
            using var userB = await LoginUser(factory, "imports-b", "Test123!");

            var a = await CreateImport(userA, "IMP-001");
            Assert.Equal(HttpStatusCode.Conflict, (await userA.PostAsJsonAsync("/api/importaciones", ImportRequest("imp-001"))).StatusCode);
            var b = await CreateImport(userB, "IMP-001");
            Assert.NotEqual(a, b);
            Assert.Equal(HttpStatusCode.NotFound, (await userA.GetAsync($"/api/importaciones/{b}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await userA.PutAsJsonAsync($"/api/importaciones/{b}", ImportRequest("B-EDIT"))).StatusCode);

            var missing = await userA.PostAsJsonAsync("/api/stock/ingresos", new { depositoId = seed.DepositA, tipoCespedId = seed.ImportedA, cantidadM2 = 10, observaciones = "Ingreso" });
            Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
            var foreign = await userA.PostAsJsonAsync("/api/stock/ingresos", new { depositoId = seed.DepositA, tipoCespedId = seed.ImportedA, cantidadM2 = 10, importacionId = b, observaciones = "Ingreso" });
            Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
            var normalWithCode = await userA.PostAsJsonAsync("/api/stock/ingresos", new { depositoId = seed.DepositA, tipoCespedId = seed.NormalA, cantidadM2 = 5, importacionId = a, observaciones = "Ingreso" });
            Assert.Equal(HttpStatusCode.BadRequest, normalWithCode.StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await userA.PostAsJsonAsync("/api/stock/ingresos", new { depositoId = seed.DepositA, tipoCespedId = seed.NormalA, cantidadM2 = 5, observaciones = "Ingreso" })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await userA.PostAsJsonAsync("/api/stock/ingresos", new { depositoId = seed.DepositA, tipoCespedId = seed.ImportedA, cantidadM2 = 20, importacionId = a, observaciones = "Ingreso" })).StatusCode);

            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.MovimientosStock.Add(new MovimientoStock { DepositoId = seed.DepositA, TipoCespedId = seed.ImportedA, Tipo = TipoMovimientoStock.Ingreso, CantidadM2 = 8, Fecha = DateTime.UtcNow, Usuario = "test" });
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.Conflict, (await userA.PostAsJsonAsync("/api/stock/reasignar-importacion", new { depositoId = seed.DepositA, tipoCespedId = seed.ImportedA, desdeImportacionId = (Guid?)null, haciaImportacionId = a, cantidadM2 = 9, observaciones = "Demasiado" })).StatusCode);
            Assert.Equal(HttpStatusCode.Created, (await userA.PostAsJsonAsync("/api/stock/reasignar-importacion", new { depositoId = seed.DepositA, tipoCespedId = seed.ImportedA, desdeImportacionId = (Guid?)null, haciaImportacionId = a, cantidadM2 = 5, observaciones = "Asignar" })).StatusCode);
            var balances = await userA.GetFromJsonAsync<JsonElement>($"/api/importaciones/saldos?depositoId={seed.DepositA}&tipoCespedId={seed.ImportedA}");
            Assert.All(balances.EnumerateArray(), x => Assert.True(x.GetProperty("saldoM2").GetDecimal() > 0));
            Assert.Equal(28, balances.EnumerateArray().Sum(x => x.GetProperty("saldoM2").GetDecimal()));
            Assert.Equal(HttpStatusCode.Conflict, (await userA.DeleteAsync($"/api/importaciones/{a}")).StatusCode);
        }
        finally { NpgsqlConnection.ClearAllPools(); await DropDatabase(master, name); }
    }

    private static async Task<Guid> CreateImport(HttpClient client, string code)
    {
        var response = await client.PostAsJsonAsync("/api/importaciones", ImportRequest(code));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private static object ImportRequest(string code) => new { codigo = code, numeroDespacho = "D-1", fecha = "2026-11-18", observaciones = "Test" };

    private static async Task<(Guid DepositA, Guid ImportedA, Guid NormalA)> Seed(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
        var branchA = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
        var depositB = new Deposito { Nombre = $"Imports B {Guid.NewGuid():N}" }; db.Add(depositB); await db.SaveChangesAsync();
        var branchB = new Sucursal { Nombre = $"Imports B {Guid.NewGuid():N}", DepositoPropioId = depositB.Id }; db.Add(branchB); await db.SaveChangesAsync(); depositB.SucursalId = branchB.Id;
        var imported = NewProduct(branchA.DepositoPropioId, "Importado"); imported.EsImportado = true; imported.ControlPorLotes = false;
        var normal = NewProduct(branchA.DepositoPropioId, "Nacional"); normal.ControlPorLotes = false;
        var permissions = JsonSerializer.Serialize(PermisosMatriz.Todos());
        var userA = new Usuario { Nombre = "Imports A", NombreUsuario = "imports-a", Rol = "Usuario", Activo = true, DebeCambiarPassword = false, SucursalId = branchA.Id, PermisosJson = permissions }; userA.PasswordHash = hasher.HashPassword(userA, "Test123!");
        var userB = new Usuario { Nombre = "Imports B", NombreUsuario = "imports-b", Rol = "Usuario", Activo = true, DebeCambiarPassword = false, SucursalId = branchB.Id, PermisosJson = permissions }; userB.PasswordHash = hasher.HashPassword(userB, "Test123!");
        db.AddRange(imported, normal, userA, userB); await db.SaveChangesAsync();
        return (branchA.DepositoPropioId, imported.Id, normal.Id);
    }
}
