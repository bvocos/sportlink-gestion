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
            new(string.Join(' ', Enumerable.Repeat("Descripción extensa del producto", 30)), "Verde", 25.5m,
                [new("IMP-2026-CODIGO-MUY-LARGO-001", "24001IC04000123X", 25.5m)])
        };
        lines.AddRange(Enumerable.Range(1, 45).Select(index => new RemitoLineaSnapshot(
            $"Producto adicional {index} con descripción para probar la paginación", "Verde", index,
            [new($"IMP-2026-{index:000}", index % 2 == 0 ? null : $"DESP-{index:000}", index)])));
        var branding = new SucursalBranding(Guid.NewGuid(), "EMPRESA DE PRUEBA", "Calle 1",
            "San Francisco", "03564-555555", "sucursal@empresa.com", "30-12345678-9", null, null,
            "Responsable inscripto", new DateOnly(2020, 1, 2), "123-456", 4, "San Francisco");

        var issuer = RemitoPdf.IssuerLines(branding);
        Assert.Contains("EMPRESA DE PRUEBA", issuer);
        Assert.Contains("Sucursal: San Francisco", issuer);
        Assert.Contains("Domicilio: Calle 1 - San Francisco", issuer);
        Assert.Contains("CUIT: 30-12345678-9", issuer);
        Assert.Contains("IVA: Responsable inscripto", issuer);
        Assert.Contains("Tel.: 03564-555555", issuer);
        Assert.Contains("Email: sucursal@empresa.com", issuer);
        Assert.Contains("Inicio Act.: 02/01/2020   Ing. Brutos: 123-456", issuer);

        var withoutLogo = RemitoPdf.Generate(remito, lines, branding);
        var withLogo = RemitoPdf.Generate(remito, lines, branding with { Logo = TinyPng(), LogoContentType = "image/png" });

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(withoutLogo, 0, 4));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(withLogo, 0, 4));
        Assert.True(withoutLogo.Length > 5_000);
        Assert.True(withLogo.Length > 5_000);
    }

    [Fact]
    public void Snapshot_DeserializesLegacyJsonAndFormatsImportationDetails()
    {
        const string legacy = "[{\"Producto\":\"Producto viejo\",\"Color\":null,\"CantidadM2\":12.5}]";
        var legacyLine = Assert.Single(RemitoEndpoints.DeserializeLines(legacy));
        Assert.Null(legacyLine.Importaciones);

        Assert.Equal("Imp. IMP-2026-001 · Despacho 24001IC04000123X",
            RemitoPdf.ImportText(new("IMP-2026-001", "24001IC04000123X", 12.5m)));
        Assert.Equal("Imp. IMP-2026-001", RemitoPdf.ImportText(new("IMP-2026-001", null, 12.5m)));
        Assert.Null(RemitoPdf.ImportText(new(" ", "DESPACHO", 12.5m)));
    }

    private static byte[] TinyPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
}
