using System.Security.Claims;
using Api.Features.Stock;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class StockVisibilityTests
{
    private static readonly Guid SanFrancisco = Guid.NewGuid();
    private static readonly Guid BuenosAires = Guid.NewGuid();
    private static readonly Guid DepositoCentral = Guid.NewGuid();
    private static readonly Guid DepositoAuxiliar = Guid.NewGuid();
    private static readonly Guid DepositoBuenosAires = Guid.NewGuid();

    [Fact]
    public void Usuario_ve_todos_los_depositos_de_su_sucursal()
    {
        var deposits = Deposits().AsQueryable();
        var visible = StockEndpoints.VisibleDepositQuery(deposits, BranchUser(), DepositoCentral).ToList();
        Assert.Equal(2, visible.Count);
        Assert.DoesNotContain(visible, x => x.Id == DepositoBuenosAires);
    }

    [Fact]
    public void Usuario_puede_filtrar_movimientos_por_un_deposito_de_su_sucursal()
    {
        var deposits = Deposits().ToDictionary(x => x.Id);
        var rows = deposits.Values.Select(x => new MovimientoStock { DepositoId = x.Id, Deposito = x }).AsQueryable();
        var visible = StockEndpoints.VisibleMovementQuery(rows, BranchUser(), DepositoCentral, DepositoAuxiliar);
        Assert.Equal(DepositoAuxiliar, Assert.Single(visible).DepositoId);
    }

    [Fact]
    public void Usuario_no_puede_ver_lotes_de_otra_sucursal()
    {
        var deposits = Deposits().ToDictionary(x => x.Id);
        var rows = deposits.Values.Select(x => new LoteStock { DepositoId = x.Id, Deposito = x }).AsQueryable();
        Assert.Empty(StockEndpoints.VisibleLotQuery(rows, BranchUser(), DepositoCentral, DepositoBuenosAires));
    }

    [Fact]
    public void Administrador_respeta_la_sucursal_activa()
    {
        var deposits = Deposits().AsQueryable();
        var visible = StockEndpoints.VisibleDepositQuery(deposits, Administrator(), DepositoCentral).ToList();
        Assert.Equal(2, visible.Count);
        Assert.DoesNotContain(visible, x => x.Id == DepositoBuenosAires);
    }

    private static Deposito[] Deposits() =>
    [
        new() { Id = DepositoCentral, SucursalId = SanFrancisco },
        new() { Id = DepositoAuxiliar, SucursalId = SanFrancisco },
        new() { Id = DepositoBuenosAires, SucursalId = BuenosAires }
    ];

    private static ClaimsPrincipal BranchUser() => Principal("Usuario");
    private static ClaimsPrincipal Administrator() => Principal("Administrador");
    private static ClaimsPrincipal Principal(string role) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.Name, role), new Claim(ClaimTypes.Role, role), new Claim("sucursal_id", SanFrancisco.ToString())], "Test"));
}
