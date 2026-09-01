using Api.Features.Presupuestos;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class PresupuestoPdfTests
{
    [Fact]
    public void Totals_UseEachProductsCashAndFinancedPriceIndependently()
    {
        var quote = new Presupuesto { DescuentoContadoPorcentaje = 5, IvaContadoPorcentaje = 10.5m, IvaFinanciadoPorcentaje = 21, EntregaFinanciada = 1000,
            Lineas = [new PresupuestoLinea { CantidadM2 = 200, PrecioContadoM2 = 21.5m, PrecioFinanciadoM2 = 23, TotalContado = 4300, TotalFinanciado = 4600 }] };
        var totals = PresupuestoCalculator.Calculate(quote);
        Assert.Equal(4300, totals.SubtotalContado); Assert.Equal(4600, totals.SubtotalFinanciado);
        Assert.Equal(4513.925m, totals.TotalContado); Assert.Equal(5566, totals.TotalFinanciado);
        Assert.Equal(1210, totals.EntregaConIva); Assert.Equal(4356, totals.SaldoFinanciado);
    }

    [Fact]
    public void Pdf_IsGeneratedWithCommercialOptions()
    {
        var quote = new Presupuesto
        {
            Numero = 27, Fecha = new DateOnly(2026, 8, 31), ValidezHasta = new DateOnly(2026, 9, 15),
            Cliente = new Cliente { Nombre = "Cliente", Apellido = "de Prueba" },
            DescuentoContadoPorcentaje = 5, IvaContadoPorcentaje = 10.5m, IvaFinanciadoPorcentaje = 21,
            EntregaFinanciada = 3600, Observaciones = "Flete e instalación a coordinar.",
            Lineas = [new PresupuestoLinea { Producto = "Padelgrass", Descripcion = "Césped sintético deportivo premium.", DescripcionPresupuesto = "Es el césped sintético ideal para campos deportivos que requieren alto rendimiento y durabilidad. Desarrollado con hilos monofilamento y fibrilados, ofrece resistencia al desgaste, comodidad para el jugador y rendimiento constante durante todo el partido. Su superficie uniforme garantiza seguridad y una larga vida útil.", EspecificacionesPresupuesto = "Ancho del rollo: 4 m\nLargo del rollo: 25 m\nAltura de hilo: 50 mm\nAltura total: 52 mm\nCalidad: Premium\nColor de hilo: A consultar", FichaTecnicaUrl = "https://example.com/ficha.pdf", Color = "Verde", CantidadM2 = 200, PrecioContadoM2 = 21.5m, PrecioFinanciadoM2 = 23, TotalContado = 4300, TotalFinanciado = 4600 }]
        };
        var assets = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Api/Assets"));
        var pdf = PresupuestoPdf.Generate(quote, assets);
        Assert.True(pdf.Length > 50_000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var pdfStructure = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(pdfStructure, @"/Type\s*/Page(?!s)"));
        Assert.Matches(@"/Subtype\s*/Link", pdfStructure);
        var output = Environment.GetEnvironmentVariable("SPORTLINK_PDF_SAMPLE");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllBytes(output, pdf);
    }
}
