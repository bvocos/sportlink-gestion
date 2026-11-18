using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Features.Auth;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

public sealed class ClienteFiscalIntegrationTests
{
    [Fact]
    public async Task FiscalData_IsOptionalSearchableEditableAndBranchScoped()
    {
        var master = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (master is null) return;
        var databaseName = $"SportlinkClienteFiscal_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(master) { Database = databaseName }.ConnectionString;
        await CreateDatabase(master, databaseName);
        try
        {
            await using (var schema = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options))
                await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient();
            Guid branchA, branchB;
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                branchA = await db.Sucursales.Where(x => x.Nombre == "San Francisco").Select(x => x.Id).SingleAsync();
                var deposit = new Deposito { Nombre = $"Fiscal B {Guid.NewGuid():N}" };
                db.Add(deposit); await db.SaveChangesAsync();
                var branch = new Sucursal { Nombre = $"Fiscal B {Guid.NewGuid():N}", DepositoPropioId = deposit.Id };
                db.Add(branch); await db.SaveChangesAsync();
                deposit.SucursalId = branch.Id;
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
                var permissions = JsonSerializer.Serialize(PermisosMatriz.Todos());
                var userA = new Usuario { Nombre = "Fiscal A", NombreUsuario = "fiscal-a", Rol = "Usuario", Activo = true, DebeCambiarPassword = false, SucursalId = branchA, PermisosJson = permissions };
                userA.PasswordHash = hasher.HashPassword(userA, "Test123!");
                var userB = new Usuario { Nombre = "Fiscal B", NombreUsuario = "fiscal-b", Rol = "Usuario", Activo = true, DebeCambiarPassword = false, SucursalId = branch.Id, PermisosJson = permissions };
                userB.PasswordHash = hasher.HashPassword(userB, "Test123!");
                db.AddRange(userA, userB);
                await db.SaveChangesAsync();
                branchB = branch.Id;
            }
            using var adminA = await LoginUser(factory, "fiscal-a", "Test123!");
            using var adminB = await LoginUser(factory, "fiscal-b", "Test123!");

            var createdResponse = await adminA.PostAsJsonAsync("/api/clientes", FiscalPayload("Fiscal Uno"));
            Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
            var created = JsonDocument.Parse(await createdResponse.Content.ReadAsStringAsync()).RootElement;
            var id = created.GetProperty("id").GetGuid();
            Assert.Equal("20123456786", created.GetProperty("numeroDocumento").GetString());
            Assert.Equal("20-12345678-6", created.GetProperty("numeroDocumentoFormateado").GetString());

            await AssertSearch(adminA, "20-12345678-6", 1);
            await AssertSearch(adminA, "123456", 1);
            await AssertSearch(adminA, "Fiscal Uno", 1);
            await AssertSearch(adminB, "20123456786", 0);
            Assert.Equal(HttpStatusCode.NotFound,
                (await adminB.PutAsJsonAsync($"/api/clientes/{id}", FiscalPayload("Ajeno"))).StatusCode);

            Assert.Equal(HttpStatusCode.Created,
                (await adminB.PostAsJsonAsync("/api/clientes", FiscalPayload("Mismo CUIT"))).StatusCode);

            var oldPayload = OldPayload("Payload viejo");
            var oldCreated = await adminA.PostAsJsonAsync("/api/clientes", oldPayload);
            Assert.Equal(HttpStatusCode.Created, oldCreated.StatusCode);
            var oldId = (await oldCreated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            Assert.Equal(HttpStatusCode.OK,
                (await adminA.PutAsJsonAsync($"/api/clientes/{oldId}", OldPayload("Payload editado"))).StatusCode);

            var clear = await adminA.PutAsJsonAsync($"/api/clientes/{id}", OldPayload("Fiscal Uno"));
            Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
            var cleared = await clear.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Null, cleared.GetProperty("numeroDocumento").ValueKind);
            Assert.Equal(JsonValueKind.Null, cleared.GetProperty("razonSocial").ValueKind);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(master, databaseName);
        }
    }

    private static async Task AssertSearch(HttpClient client, string term, int expected)
    {
        var response = await client.GetFromJsonAsync<JsonElement>($"/api/clientes?buscar={Uri.EscapeDataString(term)}");
        Assert.Equal(expected, response.GetProperty("totalCount").GetInt32());
    }

    private static object FiscalPayload(string businessName) => new
    {
        nombre = "Ana", apellido = "Pérez", telefono = "3515555555", correo = (string?)null,
        localidad = "Córdoba", provincia = "Córdoba", localidadId = (string?)null, provinciaId = (string?)null,
        tipo = "Empresa", fechaPrimerContacto = "2026-11-18", observaciones = (string?)null,
        razonSocial = businessName, tipoDocumento = "Cuit", numeroDocumento = "20-12345678-6",
        condicionIva = "ResponsableInscripto", domicilio = "Calle 1", codigoPostal = "2400"
    };

    private static object OldPayload(string name) => new
    {
        nombre = name, apellido = "Cliente", telefono = "3515555555", correo = (string?)null,
        localidad = "Córdoba", provincia = "Córdoba", localidadId = (string?)null, provinciaId = (string?)null,
        tipo = "Particular", fechaPrimerContacto = "2026-11-18", observaciones = (string?)null
    };
}
