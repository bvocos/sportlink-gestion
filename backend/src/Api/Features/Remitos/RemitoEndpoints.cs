using System.Data;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Features.Auth;
using Api.Features.Ventas;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Features.Remitos;

public sealed record RemitoRequest(string ReceptorNombre, string? ReceptorDomicilio,
    string? ReceptorLocalidad, string? ReceptorProvincia, string? ReceptorTelefono,
    string? ReceptorCuit, string? ReceptorCondicionIva, string? Observaciones);

public sealed record RemitoLineaSnapshot(string Producto, string? Color, decimal CantidadM2);

public static partial class RemitoEndpoints
{
    public static void MapRemitoEndpoints(this IEndpointRouteBuilder app)
    {
        var sales = app.MapGroup("/api/ventas").WithTags("Remitos");
        sales.MapGet("/{id:guid}/remitos", List).RequirePermiso("ventas", "ver");
        sales.MapGet("/{id:guid}/remito-borrador", Draft).RequirePermiso("ventas", "ver");
        sales.MapPost("/{id:guid}/remitos", Create).RequirePermiso("ventas", "crear");
        app.MapGet("/api/remitos/{id:guid}/pdf", Pdf).WithTags("Remitos").RequirePermiso("ventas", "ver");
    }

    internal static int NextRemitoNumber(int? currentMaximum) => (currentMaximum ?? 0) + 1;

    internal static string FormatNumber(int number, int? pointOfSale) =>
        pointOfSale.HasValue ? $"{pointOfSale:00000}-{number:00000000}" : $"{number:00000000}";

    internal static IQueryable<Remito> VisibleQuery(IQueryable<Remito> query, ClaimsPrincipal user) =>
        query.WhereVisibleParaUsuario(user);

    private static async Task<IResult> List(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var sale = await db.Ventas.AsNoTracking().WhereVisibleParaUsuario(user)
            .Where(x => x.Id == id).Select(x => new { x.Id, x.SucursalId }).SingleOrDefaultAsync(ct);
        if (sale is null) return Results.NotFound();
        var pointOfSale = await db.Sucursales.AsNoTracking().Where(x => x.Id == sale.SucursalId)
            .Select(x => x.PuntoVentaAfip).SingleAsync(ct);
        var rows = await VisibleQuery(db.Remitos.AsNoTracking(), user).Where(x => x.VentaId == id)
            .OrderByDescending(x => x.Numero).Select(x => new
            {
                x.Id, x.Numero, x.Fecha, x.ReceptorNombre
            }).ToListAsync(ct);
        return Results.Ok(rows.Select(x => new
        {
            x.Id, x.Numero, numeroTexto = FormatNumber(x.Numero, pointOfSale), x.Fecha, x.ReceptorNombre
        }));
    }

    private static async Task<IResult> Draft(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var sale = await db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped)
            .WhereVisibleParaUsuario(user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sale is null) return Results.NotFound();
        var lines = await BuildLines(sale, db, ct);
        return Results.Ok(new
        {
            receptorNombre = sale.Cliente.RazonSocial ?? $"{sale.Cliente.Nombre} {sale.Cliente.Apellido}".Trim(),
            receptorDomicilio = sale.Cliente.Domicilio ?? "",
            receptorLocalidad = sale.Cliente.Localidad,
            receptorProvincia = sale.Cliente.Provincia,
            receptorTelefono = sale.Cliente.Telefono,
            receptorCuit = sale.Cliente.TipoDocumento.HasValue && sale.Cliente.NumeroDocumento is not null
                ? DocumentoFiscal.Formatear(sale.Cliente.TipoDocumento.Value, sale.Cliente.NumeroDocumento) : "",
            receptorCondicionIva = sale.Cliente.CondicionIva.HasValue
                ? ArcaCodigos.Etiqueta(sale.Cliente.CondicionIva.Value) : "",
            observaciones = "",
            lineas = lines.Select(x => new { x.Producto, x.Color, x.CantidadM2 })
        });
    }

    private static async Task<IResult> Create(Guid id, RemitoRequest request, ClaimsPrincipal user,
        AppDbContext db, SucursalBrandingService brandingService, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var sale = await db.Ventas.AsNoTracking().Include(x => x.TipoCesped)
            .WhereVisibleParaUsuario(user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sale is null) return Results.NotFound();
        if (sale.Estado == EstadoVenta.Cancelada)
            return Results.Conflict(new { message = "No se puede generar un remito para una venta cancelada." });

        var lines = await BuildLines(sale, db, ct);
        var lineSnapshot = JsonSerializer.Serialize(lines);
        Remito? created = null;
        var attempt = 0;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Sucursales\" WHERE \"Id\" = {sale.SucursalId} FOR UPDATE", ct);
            var currentMaximum = await db.Remitos.IgnoreQueryFilters()
                .Where(x => x.SucursalId == sale.SucursalId).MaxAsync(x => (int?)x.Numero, ct);
            var remito = new Remito
            {
                SucursalId = sale.SucursalId,
                VentaId = sale.Id,
                Numero = NextRemitoNumber(currentMaximum),
                Fecha = FechaArgentina.Hoy(),
                ReceptorNombre = request.ReceptorNombre.Trim(),
                ReceptorDomicilio = Clean(request.ReceptorDomicilio),
                ReceptorLocalidad = Clean(request.ReceptorLocalidad),
                ReceptorProvincia = Clean(request.ReceptorProvincia),
                ReceptorTelefono = Clean(request.ReceptorTelefono),
                ReceptorCuit = Clean(request.ReceptorCuit),
                ReceptorCondicionIva = Clean(request.ReceptorCondicionIva),
                Observaciones = Clean(request.Observaciones),
                LineasJson = lineSnapshot
            };
            db.Remitos.Add(remito);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            created = remito;
        });
        var branding = await brandingService.GetAsync(sale.SucursalId, ct);
        return Results.Created($"/api/remitos/{created!.Id}", new
        {
            created.Id, created.Numero,
            numeroTexto = FormatNumber(created.Numero, branding?.PuntoVentaAfip)
        });
    }

    private static async Task<IResult> Pdf(Guid id, ClaimsPrincipal user, AppDbContext db,
        SucursalBrandingService brandingService, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var remito = await VisibleQuery(db.Remitos.AsNoTracking(), user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (remito is null) return Results.NotFound();
        var branding = await brandingService.GetAsync(remito.SucursalId, ct);
        if (branding is null) return Results.NotFound();
        var lines = DeserializeLines(remito.LineasJson);
        byte[] bytes;
        try { bytes = RemitoPdf.Generate(remito, lines, branding); }
        catch (Exception ex) when (branding.Logo is not null)
        {
            loggerFactory.CreateLogger("RemitoPdf").LogWarning(ex,
                "No se pudo decodificar el logo de la sucursal {SucursalId}; se generará el remito con texto.", remito.SucursalId);
            bytes = RemitoPdf.Generate(remito, lines, branding with { Logo = null, LogoContentType = null });
        }
        var number = FormatNumber(remito.Numero, branding.PuntoVentaAfip);
        return Results.File(bytes, "application/pdf", $"Remito-{number}.pdf");
    }

    private static Dictionary<string, string[]> Validate(RemitoRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.ReceptorNombre))
            errors["receptorNombre"] = ["Ingresá el nombre de quien recibe."];
        else if (request.ReceptorNombre.Trim().Length > 200)
            errors["receptorNombre"] = ["El nombre no puede superar 200 caracteres."];
        ValidateLength(errors, "receptorDomicilio", request.ReceptorDomicilio, 250);
        ValidateLength(errors, "receptorLocalidad", request.ReceptorLocalidad, 150);
        ValidateLength(errors, "receptorProvincia", request.ReceptorProvincia, 150);
        ValidateLength(errors, "receptorTelefono", request.ReceptorTelefono, 50);
        ValidateLength(errors, "receptorCondicionIva", request.ReceptorCondicionIva, 80);
        ValidateLength(errors, "observaciones", request.Observaciones, 1000);
        var document = Clean(request.ReceptorCuit);
        if (document is not null && (document.Length is < 7 or > 13 || !DocumentRegex().IsMatch(document)))
            errors["receptorCuit"] = ["El documento debe tener entre 7 y 13 caracteres y contener solo números, guiones y puntos."];
        return errors;
    }

    private static void ValidateLength(Dictionary<string, string[]> errors, string key, string? value, int max)
    {
        if (value?.Trim().Length > max) errors[key] = [$"El campo no puede superar {max} caracteres."];
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<List<RemitoLineaSnapshot>> BuildLines(Venta sale, AppDbContext db, CancellationToken ct)
    {
        List<VentaLineaCommand> source;
        try { source = JsonSerializer.Deserialize<List<VentaLineaCommand>>(sale.LineasJson ?? "[]") ?? []; }
        catch (JsonException) { source = []; }
        if (source.Count == 0)
            return [new RemitoLineaSnapshot(sale.TipoCesped.Nombre, sale.Color, sale.CantidadM2)];
        var ids = source.Select(x => x.TipoCespedId).Distinct().ToArray();
        var names = await db.TiposCesped.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);
        return source.Select(x => new RemitoLineaSnapshot(
            names.GetValueOrDefault(x.TipoCespedId, "Producto"), x.Color, x.CantidadM2)).ToList();
    }

    internal static List<RemitoLineaSnapshot> DeserializeLines(string json)
    {
        try { return JsonSerializer.Deserialize<List<RemitoLineaSnapshot>>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    [GeneratedRegex("^[0-9.-]+$")]
    private static partial Regex DocumentRegex();
}

internal static class RemitoPdf
{
    private static readonly CultureInfo Argentina = CultureInfo.GetCultureInfo("es-AR");

    internal static byte[] Generate(Remito remito, IReadOnlyList<RemitoLineaSnapshot> lines,
        SucursalBranding branding)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var number = RemitoEndpoints.FormatNumber(remito.Numero, branding.PuntoVentaAfip);
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(Colors.Black));
            page.Header().Border(1).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(left =>
                {
                    if (branding.Logo is not null)
                        left.Item().Height(45).AlignLeft().Image(branding.Logo).FitArea();
                    else
                        left.Item().Text(branding.RazonSocial).FontSize(16).Bold();
                    left.Item().PaddingTop(4).Text(branding.RazonSocial).Bold();
                    if (!string.IsNullOrWhiteSpace(branding.NombreSucursal)) left.Item().Text(branding.NombreSucursal!);
                    var location = string.Join(" - ", new[] { branding.Domicilio, branding.Localidad }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (location.Length > 0) left.Item().Text(location);
                    if (!string.IsNullOrWhiteSpace(branding.Telefono)) left.Item().Text($"Tel.: {branding.Telefono}");
                    var tax = string.Join(" - ", new[] { branding.CondicionIva, string.IsNullOrWhiteSpace(branding.Cuit) ? null : $"CUIT: {branding.Cuit}" }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (tax.Length > 0) left.Item().Text(tax);
                    var fiscal = string.Join("   ", new[]
                    {
                        branding.InicioActividades.HasValue ? $"Inicio Act.: {branding.InicioActividades:dd/MM/yyyy}" : null,
                        string.IsNullOrWhiteSpace(branding.IngresosBrutos) ? null : $"Ing. Brutos: {branding.IngresosBrutos}"
                    }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    if (fiscal.Length > 0) left.Item().Text(fiscal);
                });
                row.ConstantItem(180).BorderLeft(1).PaddingLeft(10).Column(right =>
                {
                    right.Item().AlignCenter().Text("X").FontSize(28).Bold();
                    right.Item().AlignCenter().Text("REMITO").FontSize(18).Bold();
                    right.Item().PaddingTop(5).Text($"N°: {number}").Bold();
                    right.Item().Text($"FECHA: {remito.Fecha:dd/MM/yyyy}").Bold();
                });
            });
            page.Content().PaddingTop(12).Column(content =>
            {
                content.Spacing(10);
                content.Item().Border(1).Padding(9).Column(receiver =>
                {
                    receiver.Spacing(3);
                    Field(receiver, "CLIENTE", remito.ReceptorNombre);
                    Field(receiver, "DOMICILIO", remito.ReceptorDomicilio);
                    var place = string.Join(" - ", new[] { remito.ReceptorLocalidad, remito.ReceptorProvincia }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    Field(receiver, "LOCALIDAD - PROVINCIA", place);
                    Field(receiver, "TELÉFONO", remito.ReceptorTelefono);
                    Field(receiver, "IVA", remito.ReceptorCondicionIva);
                    Field(receiver, "C.U.I.T./DNI", remito.ReceptorCuit);
                });
                if (!string.IsNullOrWhiteSpace(remito.Observaciones))
                    content.Item().Border(1).Padding(9).Text(text =>
                    {
                        text.Span("OBSERVACIONES: ").Bold();
                        text.Span(remito.Observaciones);
                    });
                content.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns => { columns.RelativeColumn(4); columns.RelativeColumn(); });
                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Descripción");
                        HeaderCell(header.Cell(), "Cant. (m²)");
                    });
                    foreach (var line in lines)
                    {
                        var description = string.IsNullOrWhiteSpace(line.Color) ? line.Producto : $"{line.Producto} - {line.Color}";
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(6).Text(description);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Padding(6).AlignRight()
                            .Text(line.CantidadM2.ToString("0.##", Argentina));
                    }
                });
                var total = lines.Sum(x => x.CantidadM2);
                content.Item().AlignRight().Border(1).Padding(9)
                    .Text($"CANTIDAD TOTAL: {total.ToString("0.##", Argentina)} m²").Bold();
            });
            page.Footer().Column(footer =>
            {
                footer.Item().LineHorizontal(1);
                footer.Item().PaddingTop(5).AlignCenter().Text("Documento no válido como factura.").Bold();
            });
        })).GeneratePdf();
    }

    private static void Field(ColumnDescriptor column, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        column.Item().Text(text => { text.Span($"{label}: ").Bold(); text.Span(value); });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background(Colors.Grey.Lighten2).Border(1).Padding(6).Text(text).Bold();
}
