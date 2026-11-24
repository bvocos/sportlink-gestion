using System.Data;
using System.Security.Claims;
using Api.Features.Auth;
using Api.Features.Cuotas;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Features.Ventas;

public sealed record RegistrarCobroVentaRequest(decimal ImporteUsd, DateOnly FechaCobro,
    MonedaCaja MonedaCobro = MonedaCaja.USD, decimal? Cotizacion = null,
    string? MedioPago = null, string? Observaciones = null);
public sealed record AnularCobroVentaRequest(string? Motivo);

internal static class VentaSaldo
{
    public static decimal Calcular(decimal total, decimal entrega, IEnumerable<CobroVenta> cobros,
        bool seguimiento = true, FormaPago formaPago = FormaPago.Contado, EstadoVenta estado = EstadoVenta.Confirmada) =>
        !seguimiento || formaPago == FormaPago.Cuotas || estado == EstadoVenta.Cancelada
            ? 0m
            : Math.Max(0m, decimal.Round(total - entrega - cobros.Where(x => !x.Anulado).Sum(x => x.ImporteUsd), 2));

    public static decimal Calcular(decimal total, decimal entrega, decimal cobradoActivo) =>
        Math.Max(0m, decimal.Round(total - entrega - cobradoActivo, 2));
}

public static class CobroVentaEndpoints
{
    public static void MapCobroVentaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ventas/{id:guid}").WithTags("Cobros de ventas");
        group.MapGet("/cobros", List).RequirePermiso("ventas", "ver");
        group.MapPost("/cobros", Register).RequirePermiso("ventas", "ver").RequirePermiso("cuotas", "registrarPago");
        group.MapPost("/cobros/{cobroId:guid}/anular", Annul).RequirePermiso("ventas", "ver").RequirePermiso("cuotas", "anularPago");
        group.MapPost("/seguimiento-saldo", EnableTracking).RequirePermiso("ventas", "editar");
        group.MapGet("/cobros/{cobroId:guid}/recibo", Receipt).RequirePermiso("ventas", "ver");
    }

    private static bool CanSeeAmounts(ClaimsPrincipal user) => VentaEndpoints.PuedeVerMontos(user);

    private static async Task<IResult> List(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var sale = await db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sale is null) return Results.NotFound();
        var charges = await db.CobrosVenta.AsNoTracking().Where(x => x.VentaId == id)
            .OrderBy(x => x.FechaCobro).ThenBy(x => x.CreatedAt).ToListAsync(ct);
        if (!CanSeeAmounts(user))
            return Results.Ok(new { sale.Id, sale.SeguimientoSaldo, cobros = charges.Select(x => new
            { x.Id, x.FechaCobro, x.MedioPago, x.NumeroRecibo, x.Anulado, x.MotivoAnulacion }) });
        var collected = charges.Where(x => !x.Anulado).Sum(x => x.ImporteUsd);
        var balance = VentaSaldo.Calcular(sale.PrecioTotal, sale.MontoEntrega, collected);
        return Results.Ok(new
        {
            sale.Id, sale.SeguimientoSaldo, sale.FormaPago, sale.Estado, sale.CotizacionVenta,
            cliente = sale.Cliente.RazonSocial ?? $"{sale.Cliente.Nombre} {sale.Cliente.Apellido}".Trim(),
            producto = sale.TipoCesped.Nombre,
            precioTotalUsd = sale.PrecioTotal,
            entrega = new { montoUsd = sale.MontoEntrega, moneda = sale.MonedaEntrega,
                importeMoneda = sale.MonedaEntrega == MonedaCaja.ARS
                    ? MonedaCajaService.ImporteCobrado(sale.MontoEntrega, sale.MonedaEntrega, sale.CotizacionEntrega)
                    : sale.MontoEntrega,
                cotizacion = sale.CotizacionEntrega, fecha = sale.FechaVenta },
            cobros = charges.Select(x => new { x.Id, x.FechaCobro, x.ImporteUsd, x.MonedaCobro,
                x.ImporteMonedaCobro, x.Cotizacion, x.MedioPago, x.Observaciones, x.NumeroRecibo,
                x.Anulado, x.AnuladoPor, x.AnuladoAt, x.MotivoAnulacion }),
            cobradoUsd = sale.MontoEntrega + collected,
            saldoUsd = sale.SeguimientoSaldo && sale.FormaPago != FormaPago.Cuotas && sale.Estado != EstadoVenta.Cancelada ? balance : 0m,
            saldoArs = sale.CotizacionVenta is > 0 ? decimal.Round(balance * sale.CotizacionVenta.Value, 2) : (decimal?)null,
            cotizacionReferencia = sale.CotizacionVenta
        });
    }

    private static async Task<IResult> Register(Guid id, RegistrarCobroVentaRequest request, ClaimsPrincipal user,
        AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request);
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var strategy = db.Database.CreateExecutionStrategy();
        var attempt = 0;
        return await strategy.ExecuteAsync(async () =>
        {
            if (attempt++ > 0) db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var sale = await db.Ventas.SingleOrDefaultAsync(x => x.Id == id, ct);
            if (sale is null) return (IResult)Results.NotFound();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"Ventas\" WHERE \"Id\" = {id} FOR UPDATE", ct);
            if (sale.FormaPago == FormaPago.Cuotas) return Results.Conflict(new { message = "Las ventas en cuotas se cobran desde el módulo Cuotas." });
            if (sale.Estado == EstadoVenta.Cancelada) return Results.Conflict(new { message = "No se pueden registrar cobros sobre una venta cancelada." });
            if (!sale.SeguimientoSaldo) return Results.Conflict(new { message = "Esta venta no tiene seguimiento de saldo. Activalo desde el detalle de cobros." });
            if (request.FechaCobro < sale.FechaVenta || request.FechaCobro > FechaArgentina.Hoy())
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["fechaCobro"] = ["La fecha debe estar entre la fecha de venta y hoy."] });
            var collected = await db.CobrosVenta.Where(x => x.VentaId == id && !x.Anulado).SumAsync(x => (decimal?)x.ImporteUsd, ct) ?? 0m;
            var balance = VentaSaldo.Calcular(sale.PrecioTotal, sale.MontoEntrega, collected);
            if (request.ImporteUsd > balance)
                return Results.Conflict(new { message = $"El cobro supera el saldo pendiente de USD {balance:N2}." });
            var lastNumber = await db.MovimientosCaja.Where(x => x.SucursalId == sale.SucursalId && x.NumeroRecibo != null)
                .MaxAsync(x => (int?)x.NumeroRecibo, ct) ?? 0;
            var received = request.MonedaCobro == MonedaCaja.ARS
                ? MonedaCajaService.ImporteCobrado(request.ImporteUsd, request.MonedaCobro, request.Cotizacion)
                : request.ImporteUsd;
            var charge = new CobroVenta { SucursalId = sale.SucursalId, VentaId = sale.Id,
                FechaCobro = request.FechaCobro, ImporteUsd = request.ImporteUsd, MonedaCobro = request.MonedaCobro,
                ImporteMonedaCobro = received, Cotizacion = request.Cotizacion, MedioPago = request.MedioPago!.Trim(),
                Observaciones = Clean(request.Observaciones), NumeroRecibo = lastNumber + 1 };
            db.CobrosVenta.Add(charge);
            await db.SaveChangesAsync(ct);
            // Fecha es el momento de registración en caja; FechaCobro conserva la fecha comercial editable.
            var movement = new MovimientoCaja { SucursalId = sale.SucursalId, VentaId = sale.Id, CobroVentaId = charge.Id,
                Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow, Monto = request.ImporteUsd,
                MonedaCobro = request.MonedaCobro, ImporteMonedaCobro = received,
                CotizacionAplicada = request.MonedaCobro == MonedaCaja.ARS ? request.Cotizacion : null,
                NumeroRecibo = charge.NumeroRecibo, Concepto = $"Cobro venta {sale.Id.ToString()[..8]} — saldo", Usuario = UserName(user) };
            db.MovimientosCaja.Add(movement);
            await db.SaveChangesAsync(ct);
            charge.MovimientoCajaId = movement.Id;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (IResult)Results.Created($"/api/ventas/{id}/cobros/{charge.Id}", new
            { charge.Id, charge.NumeroRecibo, charge.ImporteUsd, saldoUsd = VentaSaldo.Calcular(balance, 0, request.ImporteUsd) });
        });
    }

    private static async Task<IResult> Annul(Guid id, Guid cobroId, AnularCobroVentaRequest request,
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var reason = Clean(request.Motivo);
        if (reason is null || reason.Length > 500)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["motivo"] = ["Ingresá un motivo de hasta 500 caracteres."] });
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var charge = await db.CobrosVenta.SingleOrDefaultAsync(x => x.Id == cobroId && x.VentaId == id, ct);
            if (charge is null) return (IResult)Results.NotFound();
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"CobrosVenta\" WHERE \"Id\" = {cobroId} FOR UPDATE", ct);
            if (charge.Anulado) return Results.Conflict(new { message = "El cobro ya fue anulado." });
            charge.Anulado = true; charge.AnuladoPor = UserName(user); charge.AnuladoAt = DateTimeOffset.UtcNow; charge.MotivoAnulacion = reason;
            var reversal = new MovimientoCaja { SucursalId = charge.SucursalId, VentaId = charge.VentaId,
                CobroVentaId = charge.Id, Tipo = TipoMovimiento.Retiro, Fecha = DateTimeOffset.UtcNow,
                Monto = charge.ImporteUsd, MonedaCobro = charge.MonedaCobro, ImporteMonedaCobro = charge.ImporteMonedaCobro,
                CotizacionAplicada = charge.Cotizacion, Concepto = $"Anulación cobro venta {id.ToString()[..8]}", Usuario = UserName(user) };
            db.MovimientosCaja.Add(reversal); await db.SaveChangesAsync(ct); charge.MovimientoAnulacionId = reversal.Id;
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return (IResult)Results.Ok(new { charge.Id, charge.Anulado });
        });
    }

    private static async Task<IResult> EnableTracking(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var sale = await db.Ventas.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sale is null) return Results.NotFound();
        if (sale.FormaPago == FormaPago.Cuotas || sale.Estado == EstadoVenta.Cancelada)
            return Results.Conflict(new { message = "El seguimiento solo puede activarse en ventas no canceladas y que no sean en cuotas." });
        if (sale.MontoEntrega >= sale.PrecioTotal)
            return Results.Conflict(new { message = "La venta no tiene saldo pendiente para controlar." });
        sale.SeguimientoSaldo = true; await db.SaveChangesAsync(ct);
        return Results.Ok(new { sale.Id, sale.SeguimientoSaldo });
    }

    private static async Task<IResult> Receipt(Guid id, Guid cobroId, ClaimsPrincipal user, AppDbContext db,
        SucursalBrandingService brandingService, CancellationToken ct)
    {
        if (!CanSeeAmounts(user)) return Results.Forbid();
        var charge = await db.CobrosVenta.AsNoTracking().Include(x => x.Venta).ThenInclude(x => x.Cliente)
            .Include(x => x.Venta).ThenInclude(x => x.TipoCesped)
            .SingleOrDefaultAsync(x => x.Id == cobroId && x.VentaId == id, ct);
        if (charge is null) return Results.NotFound();
        var branding = await brandingService.GetAsync(charge.SucursalId, ct);
        if (branding is null) return Results.NotFound();
        var previous = await db.CobrosVenta.AsNoTracking().Where(x => x.VentaId == id && !x.Anulado &&
            (x.FechaCobro < charge.FechaCobro || x.FechaCobro == charge.FechaCobro && x.CreatedAt <= charge.CreatedAt))
            .SumAsync(x => (decimal?)x.ImporteUsd, ct) ?? 0m;
        var remaining = VentaSaldo.Calcular(charge.Venta.PrecioTotal, charge.Venta.MontoEntrega, previous);
        var bytes = CobroVentaReceiptPdf.Generate(charge, branding, remaining);
        return Results.File(bytes, "application/pdf", $"Recibo-{charge.NumeroRecibo:00000000}.pdf");
    }

    private static Dictionary<string, string[]> Validate(RegistrarCobroVentaRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ImporteUsd <= 0) errors["importeUsd"] = ["El importe debe ser mayor que cero."];
        var currency = MonedaCajaService.Validate(request.MonedaCobro, request.Cotizacion);
        if (currency is not null) errors["cotizacion"] = [currency];
        if (string.IsNullOrWhiteSpace(request.MedioPago) || request.MedioPago.Trim().Length > 100)
            errors["medioPago"] = ["Seleccioná un medio de pago válido."];
        if (request.Observaciones?.Trim().Length > 500) errors["observaciones"] = ["Las observaciones admiten hasta 500 caracteres."];
        return errors;
    }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string UserName(ClaimsPrincipal user) => user.Identity?.Name ?? user.FindFirstValue("usuario") ?? "sistema";
}

internal static class CobroVentaReceiptPdf
{
    public static byte[] Generate(CobroVenta charge, SucursalBranding branding, decimal remaining)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(32); page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10));
            page.Header().Row(row =>
            {
                if (branding.Logo is not null) row.ConstantItem(150).Height(48).Image(branding.Logo).FitArea();
                else row.RelativeItem().Text(branding.RazonSocial).Bold().FontSize(16);
                row.RelativeItem().AlignRight().Column(c => { c.Item().Text("RECIBO").Bold().FontSize(22); c.Item().Text($"N° {charge.NumeroRecibo:00000000}"); });
            });
            page.Content().PaddingTop(24).Column(c =>
            {
                c.Spacing(10);
                c.Item().Text($"Cliente: {charge.Venta.Cliente.RazonSocial ?? $"{charge.Venta.Cliente.Nombre} {charge.Venta.Cliente.Apellido}"}").Bold();
                c.Item().Text($"Fecha: {charge.FechaCobro:dd/MM/yyyy}");
                c.Item().Text($"Concepto: Cobro de saldo de la venta {charge.Venta.FechaVenta:dd/MM/yyyy}/{charge.VentaId.ToString()[..8]}");
                c.Item().Text($"Producto: {charge.Venta.TipoCesped.Nombre}");
                c.Item().Text($"USD aplicado: USD {charge.ImporteUsd:N2}").Bold().FontSize(15);
                c.Item().Text($"Son {NumerosEnLetras.ConvertirDolares(charge.ImporteUsd)}.");
                c.Item().Text(charge.MonedaCobro == MonedaCaja.ARS
                    ? $"Cobrado en pesos: ARS {charge.ImporteMonedaCobro:N2} (cotización {charge.Cotizacion:N4})"
                    : $"Cobrado en dólares: USD {charge.ImporteMonedaCobro:N2}");
                c.Item().Text($"Medio de pago: {charge.MedioPago}");
                c.Item().Text($"Saldo restante: USD {remaining:N2}").Bold();
                c.Item().Text("Importes expresados en dólares estadounidenses (USD).").FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();
    }
}
