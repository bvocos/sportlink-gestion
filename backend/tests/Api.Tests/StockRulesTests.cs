using System.Security.Claims;
using Api.Features.Stock;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class StockRulesTests
{
    [Fact]
    public void Usuario_de_san_francisco_recibe_403_al_ingresar_en_buenos_aires()
    {
        var sanFrancisco = Guid.Parse("7a100000-0000-0000-0000-000000000001");
        var buenosAires = Guid.Parse("7a100000-0000-0000-0000-000000000002");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "Usuario"),
            new Claim("permiso", "stock")
        ], "Test"));

        var failure = StockEndpoints.ValidateIngresoScope(principal, buenosAires, sanFrancisco);

        Assert.NotNull(failure);
        Assert.Equal(403, failure.StatusCode);
        Assert.Equal("Sólo podés cargar ingresos en el depósito de tu sucursal.", failure.Message);
    }

    [Fact]
    public void Usuario_puede_ingresar_en_el_deposito_de_su_sucursal()
    {
        var deposito = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, "Usuario")], "Test"));

        Assert.Null(StockEndpoints.ValidateIngresoScope(principal, deposito, deposito));
    }

    [Theory]
    [InlineData(TipoMovimientoStock.Ingreso, 100, 100)]
    [InlineData(TipoMovimientoStock.Ajuste, 100, 100)]
    [InlineData(TipoMovimientoStock.SalidaPorVenta, 100, -100)]
    public void Ledger_aplica_el_signo_segun_el_tipo(TipoMovimientoStock tipo, decimal cantidad, decimal expected) =>
        Assert.Equal(expected, StockEndpoints.SignedQuantity(tipo, cantidad));

    [Theory]
    [InlineData(100, 80, 0)]
    [InlineData(100, 100, 0)]
    [InlineData(80, 100, 20)]
    public void Venta_calcula_el_faltante_sin_permitir_stock_negativo(decimal stock, decimal solicitado, decimal faltante) =>
        Assert.Equal(faltante, Api.Features.Ventas.VentaService.StockShortage(stock, solicitado));
}
