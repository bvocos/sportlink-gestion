using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Features.Presupuestos;

public record PresupuestoLineaRequest(Guid TipoCespedId, string? Color, decimal CantidadM2, decimal PrecioContadoM2, decimal PrecioFinanciadoM2);
public record PresupuestoRequest(Guid ClienteId, DateOnly Fecha, DateOnly ValidezHasta, decimal DescuentoContadoPorcentaje,
    decimal IvaContadoPorcentaje, decimal IvaFinanciadoPorcentaje, decimal EntregaFinanciada,
    string? Observaciones, List<PresupuestoLineaRequest> Lineas);

public static class PresupuestoEndpoints
{
    public static void MapPresupuestoEndpoints(this IEndpointRouteBuilder app)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var g = app.MapGroup("/api/presupuestos").WithTags("Presupuestos").RequireAuthorization("presupuestos");
        g.MapGet("/", List); g.MapGet("/filtros", Filters); g.MapGet("/{id:guid}", Get); g.MapPost("/", Create); g.MapPut("/{id:guid}", Update);
        g.MapDelete("/{id:guid}", Delete); g.MapGet("/{id:guid}/pdf", Pdf);
    }

    private static IQueryable<Presupuesto> Query(AppDbContext db) => db.Presupuestos.AsNoTracking().Include(x => x.Cliente).Include(x => x.Lineas);
    private static object Dto(Presupuesto x)
    {
        var totals = PresupuestoCalculator.Calculate(x);
        return new { x.Id, x.Numero, x.ClienteId, cliente = $"{x.Cliente.Nombre} {x.Cliente.Apellido}".Trim(), x.Fecha, x.ValidezHasta, estado = x.Estado.ToString(),
            x.DescuentoContadoPorcentaje, x.IvaContadoPorcentaje, x.IvaFinanciadoPorcentaje, x.EntregaFinanciada, x.Observaciones,
            totals.SubtotalContado, totals.SubtotalFinanciado, totals.TotalContado, totals.TotalFinanciado, totals.EntregaConIva, totals.SaldoFinanciado,
            lineas = x.Lineas.Select(l => new { l.Id, l.TipoCespedId, l.Producto, l.Descripcion, l.DescripcionPresupuesto, l.EspecificacionesPresupuesto, l.FichaTecnicaUrl, l.Color, l.CantidadM2, l.PrecioContadoM2, l.PrecioFinanciadoM2, l.TotalContado, l.TotalFinanciado }) };
    }
    private static async Task<IResult> List(AppDbContext db, CancellationToken ct) => Results.Ok((await Query(db).OrderByDescending(x => x.Numero).ToListAsync(ct)).Select(Dto));
    private static async Task<IResult> Filters(AppDbContext db, CancellationToken ct) => Results.Ok(new {
        clientes = await db.Clientes.AsNoTracking().OrderBy(x => x.Apellido).ThenBy(x => x.Nombre)
            .Select(x => new { x.Id, nombreCompleto = (x.Nombre + " " + x.Apellido).Trim(), x.Telefono, x.Localidad, x.Provincia }).ToListAsync(ct),
        productos = await db.TiposCesped.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Nombre).Select(x => new { x.Id, x.Nombre, x.Descripcion, x.DescripcionPresupuesto, x.EspecificacionesPresupuesto, x.FichaTecnicaUrl, x.PrecioContadoM2, x.PrecioFinanciadoM2, x.ColoresJson }).ToListAsync(ct)
    });
    private static async Task<IResult> Get(Guid id, AppDbContext db, CancellationToken ct) { var x = await Query(db).SingleOrDefaultAsync(x => x.Id == id, ct); return x is null ? Results.NotFound() : Results.Ok(Dto(x)); }

    private static string? Validate(PresupuestoRequest r)
    {
        if (r.ClienteId == Guid.Empty) return "Seleccioná un cliente.";
        if (r.Lineas.Count == 0 || r.Lineas.Any(x => x.CantidadM2 <= 0 || x.PrecioContadoM2 <= 0 || x.PrecioFinanciadoM2 <= 0)) return "Agregá al menos un producto con metros y precios válidos.";
        if (r.DescuentoContadoPorcentaje is < 0 or > 100 || r.IvaContadoPorcentaje < 0 || r.IvaFinanciadoPorcentaje < 0) return "Revisá descuentos e impuestos.";
        if (r.EntregaFinanciada < 0) return "Revisá la financiación.";
        var totalFinanciado = r.Lineas.Sum(x => x.CantidadM2 * x.PrecioFinanciadoM2) * (1 + r.IvaFinanciadoPorcentaje / 100m);
        if (r.EntregaFinanciada * 1.21m > totalFinanciado) return "La entrega más IVA 21% no puede superar el total financiado.";
        return null;
    }
    private static async Task Apply(Presupuesto p, PresupuestoRequest r, AppDbContext db, CancellationToken ct)
    {
        p.ClienteId = r.ClienteId; p.Fecha = r.Fecha; p.ValidezHasta = r.Fecha.AddDays(1); p.DescuentoContadoPorcentaje = r.DescuentoContadoPorcentaje;
        p.IvaContadoPorcentaje = r.IvaContadoPorcentaje; p.IvaFinanciadoPorcentaje = r.IvaFinanciadoPorcentaje; p.EntregaFinanciada = r.EntregaFinanciada;
        p.Observaciones = r.Observaciones?.Trim();
        var ids = r.Lineas.Select(x => x.TipoCespedId).Distinct().ToArray();
        var products = await db.TiposCesped.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        p.Lineas.Clear();
        foreach (var l in r.Lineas)
        {
            if (!products.TryGetValue(l.TipoCespedId, out var product)) throw new InvalidOperationException("Uno de los productos no existe.");
            p.Lineas.Add(new PresupuestoLinea { TipoCespedId = product.Id, Producto = product.Nombre, Descripcion = product.Descripcion, DescripcionPresupuesto = string.IsNullOrWhiteSpace(product.DescripcionPresupuesto) ? product.Descripcion : product.DescripcionPresupuesto, EspecificacionesPresupuesto = product.EspecificacionesPresupuesto, FichaTecnicaUrl = product.FichaTecnicaUrl,
                Color = l.Color?.Trim(), CantidadM2 = l.CantidadM2, PrecioContadoM2 = l.PrecioContadoM2, PrecioFinanciadoM2 = l.PrecioFinanciadoM2,
                TotalContado = decimal.Round(l.CantidadM2 * l.PrecioContadoM2, 2), TotalFinanciado = decimal.Round(l.CantidadM2 * l.PrecioFinanciadoM2, 2) });
        }
    }
    private static async Task<IResult> Create(PresupuestoRequest r, AppDbContext db, CancellationToken ct)
    {
        var error = Validate(r); if (error is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["presupuesto"] = [error] });
        var p = new Presupuesto(); await Apply(p, r, db, ct); db.Add(p); await db.SaveChangesAsync(ct); return Results.Created($"/api/presupuestos/{p.Id}", new { p.Id, p.Numero });
    }
    private static async Task<IResult> Update(Guid id, PresupuestoRequest r, AppDbContext db, CancellationToken ct)
    {
        var error = Validate(r); if (error is not null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["presupuesto"] = [error] });
        var p = await db.Presupuestos.Include(x => x.Lineas).SingleOrDefaultAsync(x => x.Id == id, ct); if (p is null) return Results.NotFound();
        db.PresupuestoLineas.RemoveRange(p.Lineas); await Apply(p, r, db, ct); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
    private static async Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct) { var p = await db.Presupuestos.FindAsync([id], ct); if (p is null) return Results.NotFound(); db.Remove(p); await db.SaveChangesAsync(ct); return Results.NoContent(); }

    private static async Task<IResult> Pdf(Guid id, AppDbContext db, IWebHostEnvironment env, HttpContext http, CancellationToken ct)
    {
        var p = await Query(db).SingleOrDefaultAsync(x => x.Id == id, ct); if (p is null) return Results.NotFound();
        var bytes = PresupuestoPdf.Generate(p, Path.Combine(env.ContentRootPath, "Assets"));
        http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Results.File(bytes, "application/pdf", $"Sportlink-Presupuesto-{p.Numero:00000}.pdf");
    }
}

internal sealed record PresupuestoTotales(decimal SubtotalContado, decimal SubtotalFinanciado, decimal TotalContado, decimal TotalFinanciado, decimal EntregaConIva, decimal SaldoFinanciado);
internal static class PresupuestoCalculator
{
    internal const decimal IvaEntregaPorcentaje = 21m;
    internal static PresupuestoTotales Calculate(Presupuesto p)
    {
        var subtotalContado = p.Lineas.Sum(x => x.TotalContado); var subtotalFinanciado = p.Lineas.Sum(x => x.TotalFinanciado);
        var contado = subtotalContado * (1 - p.DescuentoContadoPorcentaje / 100m) * (1 + p.IvaContadoPorcentaje / 100m);
        var financiado = subtotalFinanciado * (1 + p.IvaFinanciadoPorcentaje / 100m);
        var entregaConIva = p.EntregaFinanciada * (1 + IvaEntregaPorcentaje / 100m);
        return new(subtotalContado, subtotalFinanciado, contado, financiado, entregaConIva, financiado - entregaConIva);
    }
}

internal static class PresupuestoPdf
{
    private const string Green = "#173F32"; private const string Lime = "#79C817"; private const string Orange = "#F04A22";
    private static string Usd(decimal value) => $"USD {value:N2}";
    public static byte[] Generate(Presupuesto p, string assets)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var totals = PresupuestoCalculator.Calculate(p); var cash = totals.TotalContado; var financed = totals.TotalFinanciado; var financedBalance = totals.SaldoFinanciado;
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(0); page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(8).FontColor(Colors.Grey.Darken4));
                page.Background().Image(Path.Combine(assets, "presupuesto-cover-padel.png")).FitArea();
                page.Content().Background("#8A081712").Padding(24).ScaleToFit().Column(c =>
                {
                    c.Spacing(7);
                    c.Item().Row(r =>
                    {
                        r.ConstantItem(145).Height(42).Image(Path.Combine(assets, "sportlink-logo.png")).FitArea();
                        r.RelativeItem().AlignRight().Column(x => { x.Item().Text("PRESUPUESTO").FontSize(14).FontColor(Lime).Bold(); x.Item().Text($"N.º {p.Numero:00000}").FontSize(10).FontColor(Colors.White); });
                    });
                    c.Item().Text("Césped sintético premium").FontSize(20).FontColor(Colors.White).Bold();
                    c.Item().Background("#CFFFFFFF").Padding(13).Column(box =>
                    {
                        box.Spacing(4); box.Item().Text($"Cliente: {p.Cliente.Nombre} {p.Cliente.Apellido}").FontSize(12).Bold().FontColor(Green);
                        box.Item().Text($"Fecha: {p.Fecha:dd/MM/yyyy}   ·   Validez: 24 horas desde la emisión").FontSize(8).FontColor(Green);
                        foreach (var l in p.Lineas.Where(x => !string.IsNullOrWhiteSpace(x.DescripcionPresupuesto) || !string.IsNullOrWhiteSpace(x.EspecificacionesPresupuesto) || !string.IsNullOrWhiteSpace(x.FichaTecnicaUrl)))
                        {
                            box.Item().PaddingTop(4).LineHorizontal(1).LineColor("#C5D3C9");
                            box.Item().Text($"Descripción general · {l.Producto}").FontSize(10).Bold().FontColor(Orange);
                            if (!string.IsNullOrWhiteSpace(l.DescripcionPresupuesto)) box.Item().Text(l.DescripcionPresupuesto!).FontSize(7.5f).LineHeight(1.08f);
                            if (!string.IsNullOrWhiteSpace(l.FichaTecnicaUrl)) box.Item().Hyperlink(l.FichaTecnicaUrl!).Text("• Ficha técnica adjunta: VER FICHA TÉCNICA").FontSize(7.5f).FontColor(Colors.Blue.Medium).Underline();
                            if (!string.IsNullOrWhiteSpace(l.EspecificacionesPresupuesto)) { box.Item().Text("Cotización técnica").FontSize(9).Bold().FontColor(Orange); box.Item().Text(l.EspecificacionesPresupuesto!).FontSize(7.5f).LineHeight(1.05f); }
                        }
                        box.Item().PaddingTop(5).Table(t =>
                        {
                            t.ColumnsDefinition(x => { x.RelativeColumn(3); x.RelativeColumn(); });
                            t.Header(h => { foreach (var s in new[] { "Producto", "Metros solicitados" }) h.Cell().Background(Green).Padding(4).Text(s).FontSize(7).FontColor(Colors.White).Bold(); });
                            foreach (var l in p.Lineas) { t.Cell().BorderBottom(1).BorderColor("#DDE5DF").Padding(4).Text(l.Producto).FontSize(7.5f); t.Cell().BorderBottom(1).BorderColor("#DDE5DF").Padding(4).AlignRight().Text($"{l.CantidadM2:N2} m²").FontSize(7.5f).Bold(); }
                        });
                        box.Item().PaddingTop(5).Row(r =>
                        {
                            r.RelativeItem().Border(1).BorderColor(Lime).Padding(9).Column(x => { x.Item().Text("OPCIÓN CONTADO").FontColor(Green).Bold(); x.Item().Text(Usd(cash)).FontSize(16).Bold().FontColor(Green); x.Item().Text($"Descuento {p.DescuentoContadoPorcentaje:N2}% · IVA {p.IvaContadoPorcentaje:N2}%").FontSize(7).FontColor(Colors.Grey.Darken1); });
                            r.ConstantItem(8); r.RelativeItem().Background(Green).Padding(9).Column(x => { x.Item().Text("OPCIÓN FINANCIADA").FontColor(Lime).Bold(); x.Item().Text(Usd(financed)).FontSize(16).Bold().FontColor(Colors.White); x.Item().Text($"Entrega: {Usd(p.EntregaFinanciada)} + IVA 21% = {Usd(totals.EntregaConIva)}").FontSize(7).FontColor(Colors.White); x.Item().Text($"Saldo: {Usd(financedBalance)} · IVA {p.IvaFinanciadoPorcentaje:N2}% incluido").FontSize(7).FontColor(Colors.Grey.Lighten2); });
                        });
                        box.Item().Text("Importes expresados exclusivamente en dólares estadounidenses (USD).").FontSize(7).Bold().FontColor(Orange);
                        box.Item().Background("#A8F5F5F5").Padding(7).Column(x =>
                        {
                            x.Spacing(2);
                            x.Item().Text("Financiación y aclaraciones").FontSize(8).Bold().FontColor(Green);
                            x.Item().Text("El saldo a financiar se puede dividir en hasta 6 cuotas mediante cheque, cuotas mensuales en dólares o pesos con cotización al día del pago, link de pago o tarjeta de crédito.").FontSize(7);
                            x.Item().Text("• Los valores detallados se encuentran expresados en dólares, tomando la cotización de venta del dólar blue del día del pago.").FontSize(7);
                            x.Item().Text("• Entrega en 15 días o en el acto, según disponibilidad de stock.").FontSize(7);
                            x.Item().Text("• Validez de la oferta sujeta a modificaciones según la variación del dólar.").FontSize(7);
                            x.Item().Text("• Garantía del producto: 5 años.").FontSize(7);
                            x.Item().Text("• El cliente deberá aportar ayuda local permanente.").FontSize(7);
                        });
                        if (!string.IsNullOrWhiteSpace(p.Observaciones)) box.Item().Background("#A8F5F5F5").Padding(7).Column(x => { x.Item().Text("Observaciones").FontSize(8).Bold(); x.Item().Text(p.Observaciones!).FontSize(7); });
                    });
                });
                page.Footer().PaddingHorizontal(24).PaddingBottom(12).Text("SPORTLINK · Synthetic Grass · by Empire").FontSize(7).FontColor(Colors.White).Bold();
            });
        }).GeneratePdf();
    }
}
