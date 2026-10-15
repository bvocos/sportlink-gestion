using Api.Features.Sucursales;

namespace Api.Tests;

public class SucursalFiscalTests
{
    [Theory]
    [InlineData("20-12345678-6", "20123456786")]
    [InlineData("20 12345678 6", "20123456786")]
    public void Cuit_IsNormalizedAndValidated(string input, string expected)
    {
        Assert.Equal(expected, SucursalEndpoints.NormalizeCuit(input));
        Assert.True(SucursalEndpoints.IsValidCuit(expected));
    }

    [Theory]
    [InlineData("20123456789")]
    [InlineData("123")]
    public void InvalidCuit_IsRejected(string input) =>
        Assert.False(SucursalEndpoints.IsValidCuit(input));

    [Theory]
    [InlineData(0)]
    [InlineData(100000)]
    public void PuntoVenta_OutsideArcaRange_IsRejected(int puntoVenta)
    {
        var request = new SucursalRequest("Centro", null, puntoVenta, Cuit: "20-12345678-6");
        Assert.Contains("puntoVentaAfip", SucursalEndpoints.Validate(request).Keys);
    }
}
