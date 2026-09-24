using System.Security.Claims;
using Api.Features.Stock;
using Api.Features.Ventas;
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
        Assert.Equal(faltante, VentaService.StockShortage(stock, solicitado));

    [Fact]
    public void Venta_con_dos_productos_genera_dos_salidas_de_stock_independientes()
    {
        var depositoId = Guid.NewGuid();
        var venta = new Venta { Id = Guid.NewGuid(), DepositoId = depositoId };
        var productoUno = Guid.NewGuid();
        var productoDos = Guid.NewGuid();
        VentaLineaCommand[] lineas =
        [
            new(productoUno, "Verde", 120m, 8m, 12m, 1440m),
            new(productoDos, "Rojo", 35m, 10m, 15m, 525m)
        ];

        var movimientos = VentaService.CreateStockMovements(venta, lineas, "tester");

        Assert.Collection(movimientos,
            movimiento =>
            {
                Assert.Equal(productoUno, movimiento.TipoCespedId);
                Assert.Equal(120m, movimiento.CantidadM2);
                Assert.Equal(TipoMovimientoStock.SalidaPorVenta, movimiento.Tipo);
                Assert.Equal(venta.Id, movimiento.VentaId);
            },
            movimiento =>
            {
                Assert.Equal(productoDos, movimiento.TipoCespedId);
                Assert.Equal(35m, movimiento.CantidadM2);
                Assert.Equal(TipoMovimientoStock.SalidaPorVenta, movimiento.Tipo);
                Assert.Equal(venta.Id, movimiento.VentaId);
            });
        Assert.All(movimientos, movimiento => Assert.Equal(depositoId, movimiento.DepositoId));
    }

    [Fact]
    public void Lote_con_dos_rollos_es_invalido()
    {
        var errors = StockEndpoints.ValidateRolls([
            new('A', "COD-A", 50m),
            new('B', "COD-B", 49.5m)
        ]);

        Assert.Contains("rollos", errors.Keys);
    }

    [Fact]
    public void Lote_con_codigo_usado_en_otro_lote_es_invalido()
    {
        string[] nuevos = ["COD-A", "COD-YA-USADO", "COD-C"];
        string[] existentes = ["COD-ANTERIOR", "cod-ya-usado"];

        Assert.True(StockEndpoints.HasPreviouslyUsedBarcode(nuevos, existentes));
    }

    [Fact]
    public void Lote_valido_crea_tres_rollos_y_movimiento_por_la_suma()
    {
        var depositoId = Guid.NewGuid();
        var productoId = Guid.NewGuid();
        var request = new IngresoLoteRequest(depositoId, productoId, "Azul", [
            new('A', "LOTE-001-A", 49.5m),
            new('B', "LOTE-001-B", 50m),
            new('C', "LOTE-001-C", 50.75m)
        ], "Ingreso de prueba");

        Assert.Empty(StockEndpoints.ValidateRolls(request.Rollos));
        var (lote, movimiento) = StockEndpoints.CreateLotRegistration(request, "tester", DateTime.UtcNow);

        Assert.Equal(3, lote.Rollos.Count);
        Assert.Equal(['A', 'B', 'C'], lote.Rollos.Select(x => x.Posicion).ToArray());
        Assert.Equal(150.25m, movimiento.CantidadM2);
        Assert.Equal(TipoMovimientoStock.Ingreso, movimiento.Tipo);
        Assert.Equal(depositoId, movimiento.DepositoId);
        Assert.Equal(productoId, movimiento.TipoCespedId);
    }

    [Fact]
    public void Venta_rechaza_un_lote_ya_vendido()
    {
        var depositoId = Guid.NewGuid();
        var productoId = Guid.NewGuid();
        var lote = new LoteStock { DepositoId = depositoId, TipoCespedId = productoId, Color = "Azul", Estado = EstadoLoteStock.Vendido, VentaId = Guid.NewGuid() };
        var linea = new VentaLineaCommand(productoId, "Azul", 150m, 10m, 15m, 2250m, lote.Id);

        var exception = Assert.Throws<LoteNoDisponibleException>(() => VentaService.ValidateLotSelection(lote, linea, depositoId, null));

        Assert.Equal("Ese lote ya fue vendido o no existe.", exception.Message);
    }

    [Fact]
    public void Venta_exitosa_marca_el_lote_como_vendido()
    {
        var depositoId = Guid.NewGuid();
        var productoId = Guid.NewGuid();
        var ventaId = Guid.NewGuid();
        var lote = new LoteStock { DepositoId = depositoId, TipoCespedId = productoId, Color = "Rojo", Estado = EstadoLoteStock.Disponible };
        var linea = new VentaLineaCommand(productoId, "Rojo", 150m, 10m, 15m, 2250m, lote.Id);

        VentaService.ValidateLotSelection(lote, linea, depositoId, null);
        VentaService.MarkLotSold(lote, ventaId);

        Assert.Equal(EstadoLoteStock.Vendido, lote.Estado);
        Assert.Equal(ventaId, lote.VentaId);
    }

    [Fact]
    public void Eliminar_venta_devuelve_el_lote_a_disponible()
    {
        var lote = new LoteStock { Estado = EstadoLoteStock.Vendido, VentaId = Guid.NewGuid() };

        VentaService.ReleaseLot(lote);

        Assert.Equal(EstadoLoteStock.Disponible, lote.Estado);
        Assert.Null(lote.VentaId);
    }
}
