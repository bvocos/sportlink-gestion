using System.Security.Claims;
using System.Text.Json;
using Api.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Api.Features.Ventas;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class PermissionMatrixTests
{
    [Fact]
    public void ListResponse_Permisos_SerializesWithoutError()
    {
        var matrix = PermisosMatriz.DesdeJson("[\"ventas\",\"clientes\"]");
        var json = JsonSerializer.Serialize(new { permisos = PermisosMatriz.ToDictionary(matrix) });
        Assert.Contains("\"ventas\"", json, StringComparison.Ordinal);
        Assert.Contains("ventas.verMontos", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DesdeJson_InvalidPayload_ReturnsEmptyMatrix()
    {
        var matrix = PermisosMatriz.DesdeJson("{\"ventas\":true}");
        Assert.False(matrix.Puede("ventas", "ver"));
    }

    [Fact]
    public void LegacyVentas_GetsEverySalesActionAndAmounts()
    {
        var matrix = PermisosMatriz.DesdeJson("[\"ventas\"]");

        Assert.True(matrix.Puede("ventas", "ver"));
        Assert.True(matrix.Puede("ventas", "crear"));
        Assert.True(matrix.Puede("ventas", "editar"));
        Assert.True(matrix.Puede("ventas", "eliminar"));
        Assert.True(matrix.PuedeVerMontos("ventas"));
    }

    [Fact]
    public async Task Administrator_IsAlwaysAuthorized()
    {
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "Administrador")], "test");
        var requirement = new PermisoRequirement("ventas", "eliminar");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermisoHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task ViewOnlyUser_CannotDeleteSales()
    {
        var identity = new ClaimsIdentity([new Claim("permiso", "ventas.ver")], "test");
        var requirement = new PermisoRequirement("ventas", "eliminar");
        var context = new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), null);

        await new PermisoHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public void SalesAmounts_AreNullWithoutAmountPermission_AndVisibleWithIt()
    {
        var venta = new Venta
        {
            PrecioTotal = 1250m, PrecioUnitario = 25m, MontoEntrega = 500m,
            Cliente = new Cliente { Nombre = "Cliente", Apellido = "Prueba" },
            TipoCesped = new TipoCesped { Nombre = "Producto" }
        };

        Assert.Null(VentaEndpoints.ToVisibleDto(venta, Principal("ventas.ver")).PrecioTotal);
        Assert.Equal(1250m, VentaEndpoints.ToVisibleDto(venta, Principal("ventas.ver", "ventas.verMontos")).PrecioTotal);
    }

    private static ClaimsPrincipal Principal(params string[] permissions) => new(new ClaimsIdentity(
        permissions.Select(x => new Claim("permiso", x)), "Test"));
}
