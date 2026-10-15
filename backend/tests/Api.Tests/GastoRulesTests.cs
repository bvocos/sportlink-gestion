using Api.Features.Gastos;
using Api.Shared.Database;
using System.Security.Claims;
using Xunit;

namespace Api.Tests;

public sealed class GastoRulesTests
{
    [Fact]
    public void Gasto_valido_no_genera_errores()
    {
        var errors = GastoEndpoints.Validate(new GastoRequest(new DateOnly(2026, 8, 12), "Servicios", "Internet", 15000, null));
        Assert.Empty(errors);
    }

    [Fact]
    public void Gasto_exige_importe_categoria_y_descripcion()
    {
        var errors = GastoEndpoints.Validate(new GastoRequest(new DateOnly(2026, 8, 12), " ", "", 0, null));
        Assert.Contains("importe", errors.Keys);
        Assert.Contains("categoria", errors.Keys);
        Assert.Contains("descripcion", errors.Keys);
    }

    [Fact]
    public void Gastos_se_aislan_por_sucursal_incluso_para_el_administrador()
    {
        var sucursalA = Guid.NewGuid();
        var sucursalB = Guid.NewGuid();
        var gastoA = new Gasto { Id = Guid.NewGuid(), SucursalId = sucursalA };
        var gastoB = new Gasto { Id = Guid.NewGuid(), SucursalId = sucursalB };
        var gastos = new[]
        {
            gastoA,
            gastoB
        }.AsQueryable();

        var usuarioA = User(sucursalA);
        Assert.Equal(sucursalA, Assert.Single(GastoEndpoints.VisibleQuery(gastos, usuarioA)).SucursalId);
        var visiblesUsuario = GastoEndpoints.VisibleQuery(gastos, usuarioA);
        Assert.DoesNotContain(visiblesUsuario, x => x.SucursalId == sucursalB);
        Assert.Empty(GastoEndpoints.VisibleByIdQuery(gastos, usuarioA, gastoB.Id)); // Update y Delete devuelven 404.
        Assert.Same(gastoA, Assert.Single(GastoEndpoints.VisibleByIdQuery(gastos, usuarioA, gastoA.Id)));
        Assert.Equal(sucursalA, Assert.Single(GastoEndpoints.VisibleQuery(gastos, Administrator(sucursalA))).SucursalId);
        Assert.Equal(sucursalB, Assert.Single(GastoEndpoints.VisibleQuery(gastos, Administrator(sucursalB))).SucursalId);
    }

    private static ClaimsPrincipal User(Guid sucursalId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "Usuario"), new Claim("sucursal_id", sucursalId.ToString())], "Test"));

    private static ClaimsPrincipal Administrator(Guid sucursalId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "Admin"), new Claim(ClaimTypes.Role, "Administrador"), new Claim("sucursal_id", sucursalId.ToString())], "Test"));
}
