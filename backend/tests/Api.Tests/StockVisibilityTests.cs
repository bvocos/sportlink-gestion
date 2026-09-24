using System.Security.Claims;
using Api.Features.Stock;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class StockVisibilityTests
{
    private static readonly Guid SanFrancisco = Guid.Parse("7a100000-0000-0000-0000-000000000001");
    private static readonly Guid BuenosAires = Guid.Parse("7a100000-0000-0000-0000-000000000002");

    [Fact]
    public void Usuario_solo_ve_stock_de_su_deposito()
    {
        var deposits = new[] { new Deposito { Id = SanFrancisco }, new Deposito { Id = BuenosAires } }.AsQueryable();

        var visible = StockEndpoints.VisibleDepositQuery(deposits, BranchUser(), SanFrancisco);

        Assert.Equal(SanFrancisco, Assert.Single(visible).Id);
    }

    [Fact]
    public void Usuario_no_puede_forzar_movimientos_del_otro_deposito()
    {
        var rows = new[] { new MovimientoStock { DepositoId = SanFrancisco }, new MovimientoStock { DepositoId = BuenosAires } }.AsQueryable();

        var visible = StockEndpoints.VisibleMovementQuery(rows, BranchUser(), SanFrancisco, BuenosAires);

        Assert.Equal(SanFrancisco, Assert.Single(visible).DepositoId);
    }

    [Fact]
    public void Usuario_no_puede_forzar_lotes_del_otro_deposito()
    {
        var rows = new[] { new LoteStock { DepositoId = SanFrancisco }, new LoteStock { DepositoId = BuenosAires } }.AsQueryable();

        var visible = StockEndpoints.VisibleLotQuery(rows, BranchUser(), SanFrancisco, BuenosAires);

        Assert.Equal(SanFrancisco, Assert.Single(visible).DepositoId);
    }

    [Fact]
    public void Codigo_de_barras_del_otro_deposito_no_es_visible()
    {
        Assert.False(StockEndpoints.CanSeeBarcodeDeposit(BranchUser(), SanFrancisco, BuenosAires));
        Assert.True(StockEndpoints.CanSeeBarcodeDeposit(BranchUser(), SanFrancisco, SanFrancisco));
    }

    [Fact]
    public void Administrador_ve_ambos_depositos_y_puede_filtrar_cualquiera()
    {
        var admin = Administrator();
        var deposits = new[] { new Deposito { Id = SanFrancisco }, new Deposito { Id = BuenosAires } }.AsQueryable();
        var movements = new[] { new MovimientoStock { DepositoId = SanFrancisco }, new MovimientoStock { DepositoId = BuenosAires } }.AsQueryable();
        var lots = new[] { new LoteStock { DepositoId = SanFrancisco }, new LoteStock { DepositoId = BuenosAires } }.AsQueryable();

        Assert.Equal(2, StockEndpoints.VisibleDepositQuery(deposits, admin, null).Count());
        Assert.Equal(BuenosAires, Assert.Single(StockEndpoints.VisibleMovementQuery(movements, admin, null, BuenosAires)).DepositoId);
        Assert.Equal(BuenosAires, Assert.Single(StockEndpoints.VisibleLotQuery(lots, admin, null, BuenosAires)).DepositoId);
        Assert.True(StockEndpoints.CanSeeBarcodeDeposit(admin, null, BuenosAires));
    }

    private static ClaimsPrincipal BranchUser() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Usuario")], "Test"));
    private static ClaimsPrincipal Administrator() => new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Admin"), new Claim(ClaimTypes.Role, "Administrador")], "Test"));
}
