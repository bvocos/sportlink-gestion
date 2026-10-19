using Api.Features.Presupuestos;
using Api.Shared.Database;
using Api.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class PresupuestoPdfTests
{
    [Fact]
    public void PdfPaymentTexts_ClarifyCashInUsdBillsAndFinancedPesoConversion()
    {
        Assert.Equal("Pago de contado en dólar billete o pesos en efectivo a cotización del dólar blue.", PresupuestoPdf.CashPaymentText);
        Assert.Contains("Opción contado: pago de contado en dólar billete o pesos en efectivo a cotización del dólar blue.", PresupuestoPdf.PaymentClarificationText);
        Assert.Contains("Opción financiada: valores en dólares", PresupuestoPdf.PaymentClarificationText);
        Assert.Contains("cotización de venta del dólar blue del día del pago", PresupuestoPdf.PaymentClarificationText);
    }

    [Theory]
    [InlineData(null, 1)]
    [InlineData(5, 6)]
    public void NextBudgetNumber_IncrementsTheCurrentBranchMaximum(int? currentMaximum, int expected) =>
        Assert.Equal(expected, PresupuestoEndpoints.NextBudgetNumber(currentMaximum));

    [Fact]
    public void PrepareCreateAttempt_ClearsTrackedEntitiesOnlyOnRetry()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=StateOnly;User ID=sa;Password=NotUsed123!;TrustServerCertificate=True")
            .Options;
        using var db = new AppDbContext(options);
        var attempt = 0;

        PresupuestoEndpoints.PrepareCreateAttempt(db, ref attempt);
        db.Add(new Presupuesto());
        Assert.Single(db.ChangeTracker.Entries());

        PresupuestoEndpoints.PrepareCreateAttempt(db, ref attempt);

        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Equal(2, attempt);
    }

    [Fact]
    public void ReplaceLines_DeletesEveryExistingLineAndAddsAllReplacements()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=StateOnly;User ID=sa;Password=NotUsed123!;TrustServerCertificate=True")
            .Options;
        using var db = new AppDbContext(options);
        var first = new PresupuestoLinea { Producto = "Anterior 1" };
        var second = new PresupuestoLinea { Producto = "Anterior 2" };
        var quote = new Presupuesto { Lineas = [first, second] };
        db.Attach(quote);
        var replacement = new PresupuestoLinea { Producto = "Nuevo" };

        PresupuestoEndpoints.ReplaceLines(quote, [replacement], db);

        Assert.Equal(EntityState.Deleted, db.Entry(first).State);
        Assert.Equal(EntityState.Deleted, db.Entry(second).State);
        Assert.Equal(EntityState.Added, db.Entry(replacement).State);
        Assert.Equal([replacement], quote.Lineas);
    }

    [Fact]
    public void Totals_UseConfiguredPricesWithoutAddingInformationalTaxPercentages()
    {
        var quote = new Presupuesto { DescuentoContadoPorcentaje = 5, IvaContadoPorcentaje = 10.5m, IvaFinanciadoPorcentaje = 21, EntregaFinanciada = 1000,
            Lineas = [new PresupuestoLinea { CantidadM2 = 200, PrecioContadoM2 = 21.5m, PrecioFinanciadoM2 = 23, TotalContado = 4300, TotalFinanciado = 4600 }] };
        var totals = PresupuestoCalculator.Calculate(quote);
        Assert.Equal(4300, totals.SubtotalContado); Assert.Equal(4600, totals.SubtotalFinanciado);
        Assert.Equal(4085, totals.TotalContado); Assert.Equal(4600, totals.TotalFinanciado);
        Assert.Equal(1000, totals.EntregaInicial); Assert.Equal(3600, totals.SaldoFinanciado);
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
        var branding = new SucursalBranding(Guid.NewGuid(), "EMPRESA DE PRUEBA", "Calle 1", "Córdoba", "", "test@example.com", "", null, null);
        var pdf = PresupuestoPdf.Generate(quote, assets, branding);
        Assert.True(pdf.Length > 50_000); Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        var pdfStructure = System.Text.Encoding.ASCII.GetString(pdf);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(pdfStructure, @"/Type\s*/Page(?!s)"));
        Assert.Matches(@"/Subtype\s*/Link", pdfStructure);
        var output = Environment.GetEnvironmentVariable("SPORTLINK_PDF_SAMPLE");
        if (!string.IsNullOrWhiteSpace(output)) File.WriteAllBytes(output, pdf);
    }
}
