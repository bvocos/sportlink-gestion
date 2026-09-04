using System.Security.Claims;
using Api.Features.Caja;
using Api.Features.Cuotas;
using Api.Features.Dashboard;
using Api.Features.Rentabilidad;
using Api.Features.Ventas;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class SucursalIsolationTests
{
    private static readonly Guid SanFrancisco = Guid.Parse("7b100000-0000-0000-0000-000000000001");
    private static readonly Guid BuenosAires = Guid.Parse("7b100000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData("Ventas")]
    [InlineData("Rentabilidad")]
    [InlineData("Dashboard")]
    public void SaleBasedEndpoints_IsolateBothBranches_AndAdministratorSeesAll(string endpoint)
    {
        var rows = new[] { new Venta { SucursalId = SanFrancisco }, new Venta { SucursalId = BuenosAires } }.AsQueryable();
        IQueryable<Venta> Filter(ClaimsPrincipal user) => endpoint switch
        {
            "Ventas" => VentaEndpoints.VisibleQuery(rows, user),
            "Rentabilidad" => RentabilidadEndpoints.VisibleQuery(rows, user),
            "Dashboard" => DashboardEndpoints.VisibleQuery(rows, user),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };

        Assert.Equal(SanFrancisco, Assert.Single(Filter(BranchUser(SanFrancisco))).SucursalId);
        Assert.Equal(BuenosAires, Assert.Single(Filter(BranchUser(BuenosAires))).SucursalId);
        Assert.Equal(2, Filter(Administrator()).Count());
        var selected = endpoint switch
        {
            "Ventas" => VentaEndpoints.VisibleQuery(rows, Administrator(), BuenosAires),
            "Rentabilidad" => RentabilidadEndpoints.VisibleQuery(rows, Administrator(), BuenosAires),
            "Dashboard" => DashboardEndpoints.VisibleQuery(rows, Administrator(), BuenosAires),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
        Assert.Equal(BuenosAires, Assert.Single(selected).SucursalId);
    }

    [Fact]
    public void Caja_IsolatesBothBranches_AndAdministratorSeesAll()
    {
        var rows = new[] { new MovimientoCaja { SucursalId = SanFrancisco }, new MovimientoCaja { SucursalId = BuenosAires } }.AsQueryable();
        Assert.Equal(SanFrancisco, Assert.Single(CajaEndpoints.VisibleQuery(rows, BranchUser(SanFrancisco))).SucursalId);
        Assert.Equal(BuenosAires, Assert.Single(CajaEndpoints.VisibleQuery(rows, BranchUser(BuenosAires))).SucursalId);
        Assert.Equal(2, CajaEndpoints.VisibleQuery(rows, Administrator()).Count());
        Assert.Equal(BuenosAires, Assert.Single(CajaEndpoints.VisibleQuery(rows, Administrator(), BuenosAires)).SucursalId);
    }

    [Fact]
    public void Cuotas_IsolateBothBranches_AndAdministratorSeesAll()
    {
        var rows = new[] { new Cuota { SucursalId = SanFrancisco }, new Cuota { SucursalId = BuenosAires } }.AsQueryable();
        Assert.Equal(SanFrancisco, Assert.Single(CuotaEndpoints.VisibleQuery(rows, BranchUser(SanFrancisco))).SucursalId);
        Assert.Equal(BuenosAires, Assert.Single(CuotaEndpoints.VisibleQuery(rows, BranchUser(BuenosAires))).SucursalId);
        Assert.Equal(2, CuotaEndpoints.VisibleQuery(rows, Administrator()).Count());
        Assert.Equal(BuenosAires, Assert.Single(CuotaEndpoints.VisibleQuery(rows, Administrator(), BuenosAires)).SucursalId);
    }

    [Fact]
    public void UserWithoutBranchClaim_SeesNoOperationalRows()
    {
        var rows = new[] { new Venta { SucursalId = SanFrancisco }, new Venta { SucursalId = BuenosAires } }.AsQueryable();
        Assert.Empty(VentaEndpoints.VisibleQuery(rows, new ClaimsPrincipal(new ClaimsIdentity(authenticationType: "Test"))));
    }

    [Fact]
    public void AdministratorCanSelectOneBranch_ButRegularUserCannotOverrideItsOwnBranch()
    {
        var rows = new[] { new Venta { SucursalId = SanFrancisco }, new Venta { SucursalId = BuenosAires } }.AsQueryable();

        Assert.Equal(BuenosAires,
            Assert.Single(VentaEndpoints.VisibleQuery(rows, Administrator(), BuenosAires)).SucursalId);
        Assert.Equal(SanFrancisco,
            Assert.Single(VentaEndpoints.VisibleQuery(rows, BranchUser(SanFrancisco), BuenosAires)).SucursalId);
    }

    private static ClaimsPrincipal BranchUser(Guid sucursalId) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "Usuario"), new Claim("sucursal_id", sucursalId.ToString())], "Test"));

    private static ClaimsPrincipal Administrator() => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, "Admin"), new Claim(ClaimTypes.Role, "Administrador")], "Test"));
}
