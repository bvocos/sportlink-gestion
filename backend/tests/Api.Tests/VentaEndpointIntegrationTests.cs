using System.Net;
using System.Net.Http.Json;
using Api.Shared.Database;
using Api.Features.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

public sealed class VentaEndpointIntegrationTests
{
    [Fact]
    public async Task SucursalAdministration_CreatesOwnDeposit_ProtectsData_AndDrivesReceiptFiscalData()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;
        var databaseName = $"SportlinkSucursalTests_{Guid.NewGuid():N}";
        var databaseConnection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = databaseName }.ConnectionString;
        await WaitForSqlServer(masterConnection);
        await using (var master = new SqlConnection(masterConnection))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }
        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(databaseConnection).Options;
            await using (var schema = new AppDbContext(options)) await schema.Database.EnsureCreatedAsync();
            await using var factory = new VentaApiFactory(databaseConnection);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                (await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin")).DebeCambiarPassword = false;
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", password = "Admin123!" })).StatusCode);
            var payload = new
            {
                nombre = "Sucursal Fiscal", nombreDeposito = "Depósito Fiscal", depositoPropioId = (Guid?)null,
                razonSocial = "EMPRESA SUCURSAL S.A.", cuit = "20-12345678-6", condicionIva = "Responsable Inscripto",
                domicilioFiscal = "Calle 123", localidad = "Córdoba", telefono = "3510000000",
                email = "fiscal@example.com", inicioActividades = new DateOnly(2025, 1, 1),
                ingresosBrutos = "123", puntoVentaAfip = (int?)null, activo = true
            };
            var created = await client.PostAsJsonAsync("/api/sucursales", payload);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var createdJson = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            var branchId = createdJson.GetProperty("id").GetGuid();
            var depositId = createdJson.GetProperty("depositoPropioId").GetGuid();

            var png = new byte[24];
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
            png[19] = 100; png[23] = 50;
            Assert.Equal(HttpStatusCode.OK, (await PutLogo(client, branchId, png, "logo.png", "image/png")).StatusCode);
            var logoResponse = await client.GetAsync($"/api/sucursales/{branchId}/logo");
            Assert.Equal(HttpStatusCode.OK, logoResponse.StatusCode);
            Assert.Equal("private", logoResponse.Headers.CacheControl?.ToString());
            Assert.NotNull(logoResponse.Headers.ETag);

            byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xC0, 0, 8, 8, 0, 50, 0, 100, 1];
            Assert.Equal(HttpStatusCode.OK, (await PutLogo(client, branchId, jpeg, "logo.jpg", "image/jpeg")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutLogo(client, branchId, "fake"u8.ToArray(), "logo.png", "image/png")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutLogo(client, branchId, "<svg/>"u8.ToArray(), "logo.svg", "image/svg+xml")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await PutLogo(client, branchId,
                new byte[1024 * 1024 + 1], "huge.png", "image/png")).StatusCode);

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var audit = await db.RegistrosAuditoria.Where(x => x.Entidad == nameof(SucursalLogo)).ToListAsync();
                Assert.NotEmpty(audit);
                Assert.All(audit, x =>
                {
                    Assert.DoesNotContain("Contenido", x.DetalleJson);
                    Assert.True(x.DetalleJson.Length < 500);
                });
                var ownBranch = await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco");
                var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>();
                var limited = new Usuario
                {
                    Nombre = "Logo limitado", NombreUsuario = "logo-limited", Rol = "Usuario",
                    SucursalId = ownBranch.Id, Activo = true, DebeCambiarPassword = false,
                    PermisosJson = System.Text.Json.JsonSerializer.Serialize(PermisosMatriz.Todos())
                };
                limited.PasswordHash = hasher.HashPassword(limited, "Test123!");
                db.Add(limited); await db.SaveChangesAsync();
            }
            using (var limitedClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            {
                Assert.Equal(HttpStatusCode.OK, (await limitedClient.PostAsJsonAsync("/api/auth/login",
                    new { usuario = "logo-limited", password = "Test123!" })).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await limitedClient.GetAsync($"/api/sucursales/{branchId}/logo")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await PutLogo(limitedClient, branchId, png, "logo.png", "image/png")).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden, (await limitedClient.DeleteAsync($"/api/sucursales/{branchId}/logo")).StatusCode);
            }

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Assert.Equal(branchId, (await db.Depositos.SingleAsync(x => x.Id == depositId)).SucursalId);
            }

            var duplicate = await client.PostAsJsonAsync("/api/sucursales", new
            {
                nombre = "Sucursal Fiscal Duplicada", nombreDeposito = "Otro", cuit = "20 12345678 6",
                puntoVentaAfip = 12, activo = true
            });
            Assert.Equal(HttpStatusCode.Created, duplicate.StatusCode);
            var duplicatePv = await client.PostAsJsonAsync("/api/sucursales", new
            {
                nombre = "Sucursal Fiscal Duplicada 2", nombreDeposito = "Otro 2", cuit = "20123456786",
                puntoVentaAfip = 12, activo = true
            });
            Assert.Equal(HttpStatusCode.Conflict, duplicatePv.StatusCode);

            Guid foreignDeposit;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                foreignDeposit = (await db.Sucursales.SingleAsync(x => x.Nombre == "San Francisco")).DepositoPropioId;
            }
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/sucursales", new
            {
                nombre = "No roba depósito", depositoPropioId = foreignDeposit, activo = true
            })).StatusCode);

            Guid installmentId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var customer = new Cliente { SucursalId = branchId, Nombre = "Fiscal", Apellido = "Cliente", Localidad = "Córdoba", Provincia = "Córdoba", Tipo = TipoCliente.Particular };
                var product = new TipoCesped { DepositoId = depositId, Nombre = "Producto fiscal", PrecioVentaM2 = 10, PrecioContadoM2 = 10, PrecioFinanciadoM2 = 10, CostoM2 = 5 };
                db.AddRange(customer, product); await db.SaveChangesAsync();
                var tax = await db.AlicuotasIva.FirstAsync();
                var sale = new Venta { SucursalId = branchId, DepositoId = depositId, ClienteId = customer.Id, TipoCespedId = product.Id, AlicuotaIvaId = tax.Id, FechaVenta = DateOnly.FromDateTime(DateTime.Today), CantidadM2 = 1, PrecioUnitario = 10, PrecioTotal = 10, CostoCompraUnitario = 5, CostoCompraTotal = 5, FormaPago = FormaPago.Cuotas, Estado = EstadoVenta.Confirmada };
                db.Add(sale); await db.SaveChangesAsync();
                var installment = new Cuota { SucursalId = branchId, DepositoId = depositId, VentaId = sale.Id, ClienteId = customer.Id, Numero = 1, FechaVencimiento = DateOnly.FromDateTime(DateTime.Today), FechaPago = DateOnly.FromDateTime(DateTime.Today), ImportePactado = 10, ImportePagado = 10, Estado = EstadoCuota.Pagada };
                db.Add(installment); await db.SaveChangesAsync();
                db.Add(new MovimientoCaja { SucursalId = branchId, VentaId = sale.Id, CuotaId = installment.Id, Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow, Monto = 10, Concepto = "Pago", NumeroRecibo = 1 });
                await db.SaveChangesAsync(); installmentId = installment.Id;
            }
            client.DefaultRequestHeaders.Add("X-Sucursal-Id", branchId.ToString());
            var receipt = await client.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/cuotas/{installmentId}/comprobante");
            Assert.Equal("EMPRESA SUCURSAL S.A.", receipt.GetProperty("empresa").GetProperty("razonSocial").GetString());
            Assert.Equal(System.Text.Json.JsonValueKind.Null, receipt.GetProperty("puntoVenta").ValueKind);
            Assert.Equal(HttpStatusCode.Conflict, (await client.DeleteAsync($"/api/sucursales/{branchId}")).StatusCode);
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var master = new SqlConnection(masterConnection);
            await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task PostVenta_PersistsUsingSqlServerRetryingExecutionStrategy()
    {
        var masterConnection = SqlIntegrationEnvironment.ConnectionOrSkip();
        if (masterConnection is null) return;

        var databaseName = $"SportlinkTests_{Guid.NewGuid():N}";
        var databaseConnection = new SqlConnectionStringBuilder(masterConnection) { InitialCatalog = databaseName }.ConnectionString;
        await WaitForSqlServer(masterConnection);

        await using (var master = new SqlConnection(masterConnection))
        {
            await master.OpenAsync();
            await using var create = master.CreateCommand();
            create.CommandText = $"CREATE DATABASE [{databaseName}]";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(databaseConnection, sql => sql.EnableRetryOnFailure())
                .Options;
            await using (var schema = new AppDbContext(options))
                await schema.Database.EnsureCreatedAsync();

            await using var factory = new VentaApiFactory(databaseConnection);
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            Guid clienteId;
            Guid tipoCespedId;
            Guid alicuotaIvaId;
            Guid sucursalId;
            Guid depositoId;
            Guid depositoSecundarioId;
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var sucursal = await db.Sucursales.Include(x => x.DepositoPropio)
                    .SingleAsync(x => x.Nombre == "San Francisco" && x.Activo);
                var cliente = new Cliente
                {
                    SucursalId = sucursal.Id,
                    Nombre = "Cliente", Apellido = "Integracion", Telefono = "3515555555",
                    Localidad = "Cordoba", Provincia = "Cordoba", Tipo = TipoCliente.Particular
                };
                var tipo = new TipoCesped
                {
                    DepositoId = sucursal.DepositoPropioId,
                    Nombre = $"Producto {Guid.NewGuid():N}", PrecioVentaM2 = 20m,
                    PrecioContadoM2 = 20m, PrecioFinanciadoM2 = 22m, CostoM2 = 10m, Activo = true
                };
                var depositoSecundario = new Deposito
                {
                    SucursalId = sucursal.Id, Nombre = $"Secundario {Guid.NewGuid():N}", Activo = true
                };
                var alicuota = await db.AlicuotasIva.FirstAsync();
                var admin = await db.Usuarios.SingleAsync(x => x.NombreUsuario == "admin");
                admin.DebeCambiarPassword = false;
                db.Clientes.Add(cliente);
                db.Depositos.Add(depositoSecundario);
                db.TiposCesped.Add(tipo);
                await db.SaveChangesAsync();
                tipo.Depositos.Add(new TipoCespedDeposito { DepositoId = sucursal.DepositoPropioId });
                tipo.Depositos.Add(new TipoCespedDeposito { DepositoId = depositoSecundario.Id });
                await db.SaveChangesAsync();
                db.MovimientosStock.Add(new MovimientoStock
                {
                    DepositoId = sucursal.DepositoPropioId,
                    TipoCespedId = tipo.Id,
                    Tipo = TipoMovimientoStock.Ingreso,
                    CantidadM2 = 100m,
                    Fecha = DateTime.UtcNow,
                    Usuario = "integracion-test"
                });
                await db.SaveChangesAsync();
                clienteId = cliente.Id;
                tipoCespedId = tipo.Id;
                alicuotaIvaId = alicuota.Id;
                sucursalId = sucursal.Id;
                depositoId = sucursal.DepositoPropioId;
                depositoSecundarioId = depositoSecundario.Id;
            }

            var login = await client.PostAsJsonAsync("/api/auth/login", new { usuario = "admin", password = "Admin123!" });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            client.DefaultRequestHeaders.Add("X-Sucursal-Id", sucursalId.ToString());

            var stock = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/stock");
            var secondaryStock = stock.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == depositoSecundarioId);
            Assert.Contains(secondaryStock.GetProperty("productos").EnumerateArray(),
                x => x.GetProperty("tipoCespedId").GetGuid() == tipoCespedId);

            var saleFilters = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/ventas/filtros");
            var secondarySaleDeposit = saleFilters.GetProperty("depositos").EnumerateArray()
                .Single(x => x.GetProperty("id").GetGuid() == depositoSecundarioId);
            Assert.Contains(secondarySaleDeposit.GetProperty("productos").EnumerateArray(),
                x => x.GetProperty("tipoCespedId").GetGuid() == tipoCespedId);

            var quoteFilters = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/presupuestos/filtros");
            var quoteProduct = quoteFilters.GetProperty("productos").EnumerateArray()
                .Single(x => x.GetProperty("id").GetGuid() == tipoCespedId);
            Assert.Contains(quoteProduct.GetProperty("depositoIds").EnumerateArray(), x => x.GetGuid() == depositoSecundarioId);

            var response = await client.PostAsJsonAsync("/api/ventas", new
            {
                clienteId,
                fechaVenta = DateOnly.FromDateTime(DateTime.Today),
                tipoCespedId,
                cantidadM2 = 10m,
                precioUnitario = 20m,
                precioTotal = 200m,
                montoEntrega = 0m,
                formaPago = "Cuotas",
                cantidadCuotas = 2,
                estado = "Confirmada",
                fechaEntregaEstimada = (DateOnly?)null,
                observaciones = "Prueba de integracion",
                costoCompraUnitario = 10m,
                costoEnvio = 0m,
                otrosCostos = 0m,
                alicuotaIvaId,
                sucursalId,
                depositoId,
                color = (string?)null,
                lineas = new[]
                {
                    new { tipoCespedId, color = (string?)null, cantidadM2 = 10m, precioCompraM2 = 10m, precioVentaM2 = 20m, total = 200m }
                }
            });

            var responseBody = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == HttpStatusCode.Created,
                $"Expected 201 Created but got {(int)response.StatusCode} {response.StatusCode}. Body: {responseBody}");
            using var verificationScope = factory.Services.CreateScope();
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(1, await verificationDb.Ventas.IgnoreQueryFilters().CountAsync());
            Assert.Equal(2, await verificationDb.Cuotas.IgnoreQueryFilters().CountAsync());
        }
        finally
        {
            SqlConnection.ClearAllPools();
            await using var master = new SqlConnection(masterConnection);
            await master.OpenAsync();
            await using var drop = master.CreateCommand();
            drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task WaitForSqlServer(string connectionString)
    {
        Exception? lastError = null;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();
                return;
            }
            catch (Exception exception)
            {
                lastError = exception;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }
        throw new InvalidOperationException("SQL Server de pruebas no estuvo disponible.", lastError);
    }

    private static async Task<HttpResponseMessage> PutLogo(HttpClient client, Guid branchId,
        byte[] content, string fileName, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await client.PutAsync($"/api/sucursales/{branchId}/logo", form);
    }

    private sealed class VentaApiFactory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SkipDatabaseMigrations"] = "true"
                });
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
                    connectionString,
                    sql => sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null)));
            });
        }
    }
}
