using Api.Shared.Common;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class MonedaCajaTests
{
    [Theory]
    [InlineData(10.125, 1250, 12656.25)]
    [InlineData(1.005, 1, 1.01)]
    public void ImporteCobrado_ConvertsAndRoundsAwayFromZero(decimal usd, decimal quote, decimal expected) =>
        Assert.Equal(expected, MonedaCajaService.ImporteCobrado(usd, MonedaCaja.ARS, quote));

    [Fact]
    public void Usd_DoesNotNeedAQuote() =>
        Assert.Equal(10.13m, MonedaCajaService.ImporteCobrado(10.125m, MonedaCaja.USD, null));

    [Fact]
    public void Ars_RejectsInvalidQuote()
    {
        foreach (var quote in new decimal?[] { null, 0m, -1m })
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                MonedaCajaService.ImporteCobrado(10, MonedaCaja.ARS, quote));
    }

    [Fact]
    public void ManualArsAmount_IsConvertedToUsd() =>
        Assert.Equal(1.2m, MonedaCajaService.MontoUsd(1500, MonedaCaja.ARS, 1250));
}
