using Api.Features.Remitos;
using Api.Shared.Common;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class RemitoPdfTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData(7, 8)]
    public void NextRemitoNumber_IncrementsTheBranchMaximum(int? currentMaximum, int expected) =>
        Assert.Equal(expected, RemitoEndpoints.NextRemitoNumber(currentMaximum));

    [Theory]
    [InlineData(12, 34, "00012-00000034")]
    [InlineData(null, 34, "00000034")]
    public void FormatNumber_UsesPointOfSaleWhenAvailable(int? pointOfSale, int number, string expected) =>
        Assert.Equal(expected, RemitoEndpoints.FormatNumber(number, pointOfSale));

    [Fact]
    public void Pdf_GeneratesWithoutLogoWithLogoAndLongDescription()
    {
        var remito = new Remito
        {
            Numero = 3,
            Fecha = new DateOnly(2026, 11, 18),
            ReceptorNombre = "Cliente de prueba",
            ReceptorDomicilio = "Calle 123",
            ReceptorLocalidad = "San Francisco",
            ReceptorProvincia = "Córdoba",
            ReceptorTelefono = "3564-555555",
            ReceptorCuit = "20-12345678-6",
            ReceptorCondicionIva = "Consumidor final",
            Observaciones = "Entregar por acceso lateral."
        };
        var lines = new List<RemitoLineaSnapshot>
        {
            new(string.Join(' ', Enumerable.Repeat("Descripción extensa del producto", 30)), "Verde", 25.5m)
        };
        var branding = new SucursalBranding(Guid.NewGuid(), "EMPRESA DE PRUEBA", "Calle 1",
            "San Francisco", "03564-555555", "", "30-12345678-9", null, null,
            "Responsable inscripto", new DateOnly(2020, 1, 2), "123-456", 4, "San Francisco");

        var withoutLogo = RemitoPdf.Generate(remito, lines, branding);
        var withLogo = RemitoPdf.Generate(remito, lines, branding with { Logo = TinyPng(), LogoContentType = "image/png" });

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(withoutLogo, 0, 4));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(withLogo, 0, 4));
        Assert.True(withoutLogo.Length > 5_000);
        Assert.True(withLogo.Length > 5_000);
    }

    private static byte[] TinyPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
}
