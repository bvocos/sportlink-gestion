using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static Api.Tests.BranchTestHost;

namespace Api.Tests;

/// <summary>
/// Un administrador cambia contraseñas desde Usuarios:
/// la suya queda definitiva; la de otro usuario es provisional y además lo desbloquea.
/// </summary>
public sealed class AdminPasswordIntegrationTests
{
    [Fact]
    public async Task AdminChangesOwnAndOthersPasswords()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;
        var databaseName = $"SportlinkPasswordTests_{Guid.NewGuid():N}";
        var connection = new NpgsqlConnectionStringBuilder(masterConnection) { Database = databaseName }.ConnectionString;
        await CreateDatabase(masterConnection, databaseName);
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new BranchApiFactory(connection);
            _ = factory.CreateClient(); // Inicia Program y SeedData.
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin")).DebeCambiarPassword = false;
                await db.SaveChangesAsync();
            }

            using var admin = await Login(factory, "admin", "Admin123!");
            var users = await admin.GetFromJsonAsync<JsonElement>("/api/usuarios");
            var adminUser = users.EnumerateArray().Single(x => x.GetProperty("nombreUsuario").GetString() == "admin");
            var permisos = adminUser.GetProperty("permisos");

            // Otro administrador, luego bloqueado por intentos fallidos.
            var created = await admin.PostAsJsonAsync("/api/usuarios", new
            {
                nombre = "Otro", nombreUsuario = "otro", password = "Inicial123!", rol = "Administrador",
                permisos, activo = true, sucursalId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var otroId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var otro = await db.Usuarios.SingleAsync(x => x.Id == otroId);
                otro.BloqueadoHasta = DateTimeOffset.UtcNow.AddMinutes(15);
                otro.IntentosFallidos = 3;
                await db.SaveChangesAsync();
            }

            // 1. Su propia contraseña: queda definitiva.
            var own = await admin.PutAsJsonAsync($"/api/usuarios/{adminUser.GetProperty("id").GetGuid()}", new
            {
                nombre = "Administrador", nombreUsuario = "admin", password = "Propia123!", rol = "Administrador",
                permisos, activo = true, sucursalId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
            using var adminAgain = await Login(factory, "admin", "Propia123!");
            var me = await adminAgain.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.False(me.GetProperty("debeCambiarPassword").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, (await adminAgain.GetAsync("/api/usuarios")).StatusCode);

            // 2. La de otro usuario: provisional y lo desbloquea.
            var other = await adminAgain.PutAsJsonAsync($"/api/usuarios/{otroId}", new
            {
                nombre = "Otro", nombreUsuario = "otro", password = "Provisoria123!", rol = "Administrador",
                permisos, activo = true, sucursalId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
            using var otroClient = await Login(factory, "otro", "Provisoria123!");
            var otroMe = await otroClient.GetFromJsonAsync<JsonElement>("/api/auth/me");
            Assert.True(otroMe.GetProperty("debeCambiarPassword").GetBoolean());

            // 3. Mensaje de contraseña corta, con la ñ correcta.
            var tooShort = await adminAgain.PutAsJsonAsync($"/api/usuarios/{otroId}", new
            {
                nombre = "Otro", nombreUsuario = "otro", password = "corta", rol = "Administrador",
                permisos, activo = true, sucursalId = (Guid?)null
            });
            Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
            Assert.Contains("contraseña", await tooShort.Content.ReadAsStringAsync());
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await DropDatabase(masterConnection, databaseName);
        }
    }

    private static async Task<HttpClient> Login(BranchApiFactory factory, string user, string password)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/login", new { usuario = user, password })).StatusCode);
        return client;
    }
}
