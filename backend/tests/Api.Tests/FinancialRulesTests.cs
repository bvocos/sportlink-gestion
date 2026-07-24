using Api.Features.Caja;
using Api.Features.Cuotas;
using Api.Features.Ventas;
using Api.Features.Clientes;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class FinancialRulesTests
{
    [Theory]
    [InlineData("bruno", "Bruno")]
    [InlineData("MARÍA DE LA FUENTE", "María de la Fuente")]
    [InlineData("  juan   del   río  ", "Juan del Río")]
    public void ClientNameFormatting_UsesSpanishTitleCase(string input, string expected) =>
        Assert.Equal(expected, CrearClienteHandler.FormatPersonName(input));

    private static RegistrarVentaCommand Sale(
        FormaPago formaPago = FormaPago.Contado, int? cuotas = null, decimal entrega = 100m,
        decimal precioTotal = 1000m) =>
        new(Guid.NewGuid(), new DateOnly(2026, 7, 24), Guid.NewGuid(), 10m, 100m, precioTotal, entrega,
            formaPago, cuotas, EstadoVenta.Confirmada, null, "Prueba", 40m, 50m, 25m, Guid.NewGuid());

    [Fact]
    public void Apply_CalculatesTotalsNetProfitAndMargin()
    {
        var venta = new Venta();
        VentaService.Apply(venta, Sale(), 21m);
        Assert.Equal(1000m, venta.PrecioTotal);
        Assert.Equal(400m, venta.CostoCompraTotal);
        Assert.Equal(525m, venta.GananciaBruta);
        Assert.Equal(315m, venta.GananciaNeta);
        Assert.Equal(0.315m, venta.Margen);
    }

    [Fact]
    public void Apply_UsesEditableFinalAmountForFinancialCalculations()
    {
        var venta = new Venta();
        VentaService.Apply(venta, Sale(precioTotal: 990m), 21m);
        Assert.Equal(990m, venta.PrecioTotal);
        Assert.Equal(207.9m, venta.Iva);
        Assert.Equal(307.1m, venta.GananciaNeta);
    }

    [Fact]
    public void CreateInstallments_FinancesBalanceAndPreservesTotal()
    {
        var command = Sale(FormaPago.Cuotas, 3, 100m);
        var venta = new Venta { PrecioTotal = 1000m, MontoEntrega = 100m };
        VentaService.CreateInstallments(venta, command);
        Assert.Equal(3, venta.Cuotas.Count);
        Assert.Equal(new[] { 300m, 300m, 300m }, venta.Cuotas.Select(x => x.ImportePactado));
        Assert.Equal(900m, venta.Cuotas.Sum(x => x.ImportePactado));
        Assert.Equal(new[] { 1, 2, 3 }, venta.Cuotas.Select(x => x.Numero));
    }

    [Fact]
    public void CreateInstallments_AssignsRoundingDifferenceToLastInstallment()
    {
        var command = Sale(FormaPago.Cuotas, 3, 0m);
        var venta = new Venta { PrecioTotal = 100m, MontoEntrega = 0m };
        VentaService.CreateInstallments(venta, command);
        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, venta.Cuotas.Select(x => x.ImportePactado));
        Assert.Equal(100m, venta.Cuotas.Sum(x => x.ImportePactado));
    }

    [Fact]
    public void Validator_RejectsInstallmentSaleWithoutInstallmentCount()
    {
        var result = new RegistrarVentaValidator().Validate(Sale(FormaPago.Cuotas, null));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(RegistrarVentaCommand.CantidadCuotas));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(-1, 100)]
    [InlineData(101, 100)]
    public void PaymentValidation_RejectsInvalidAmounts(decimal importe, decimal saldo) =>
        Assert.NotNull(CuotaEndpoints.ValidatePayment(importe, saldo));

    [Fact]
    public void PaymentValidation_AcceptsExactRemainingBalance() =>
        Assert.Null(CuotaEndpoints.ValidatePayment(100m, 100m));

    [Fact]
    public void WithdrawalValidation_RejectsNegativeResult() =>
        Assert.NotNull(CajaEndpoints.ValidateWithdrawal(TipoMovimiento.Retiro, 101m, 100m));

    [Fact]
    public void WithdrawalValidation_AcceptsAvailableBalance() =>
        Assert.Null(CajaEndpoints.ValidateWithdrawal(TipoMovimiento.Retiro, 100m, 100m));
}
