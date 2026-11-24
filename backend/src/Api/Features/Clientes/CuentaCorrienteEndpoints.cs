using System.Security.Claims;
using System.Text;
using Api.Features.Auth;
using Api.Features.Cotizaciones;
using Api.Features.Cuotas;
using Api.Features.Ventas;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Features.Clientes;

public static class CuentaCorrienteEndpoints
{
    private sealed record AccountMovement(DateOnly Fecha, string Concepto, decimal ImporteUsd,
        MonedaCaja? Moneda, decimal? ImporteMoneda, decimal? Cotizacion, bool Anulado, Guid VentaId);
    private sealed record AccountMovementRow(DateOnly Fecha, string Concepto, decimal ImporteUsd,
        MonedaCaja? Moneda, decimal? ImporteMoneda, decimal? Cotizacion, bool Anulado, Guid VentaId,
        decimal SaldoUsd);
    public static void MapCuentaCorrienteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clientes").WithTags("Cuenta corriente");
        group.MapGet("/deudores", Debtors).RequirePermiso("clientes", "ver").RequirePermiso("ventas", "ver");
        group.MapGet("/{id:guid}/cuenta-corriente", Account).RequirePermiso("clientes", "ver").RequirePermiso("ventas", "ver");
        group.MapGet("/{id:guid}/cuenta-corriente.csv", Csv).RequirePermiso("clientes", "ver").RequirePermiso("ventas", "ver");
        group.MapGet("/{id:guid}/cuenta-corriente.pdf", Pdf).RequirePermiso("clientes", "ver").RequirePermiso("ventas", "ver");
    }

    private static async Task<IResult> Account(Guid id, ClaimsPrincipal user, AppDbContext db,
        DolarBlueService dolar, CancellationToken ct)
    {
        if (!VentaEndpoints.PuedeVerMontos(user)) return Results.Forbid();
        var data = await Build(id, user, db, dolar, ct);
        return data is null ? Results.NotFound() : Results.Ok(data);
    }

    private static async Task<IResult> Debtors(ClaimsPrincipal user, AppDbContext db, DolarBlueService dolar,
        string? buscar, string? orden, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        if (!VentaEndpoints.PuedeVerMontos(user)) return Results.Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var today = FechaArgentina.Hoy();
        var visibleSales = VentaEndpoints.VisibleQuery(db.Ventas.AsNoTracking(), user);
        var visibleInstallments = CuotaEndpoints.VisibleQuery(db.Cuotas.AsNoTracking(), user);
        var query = db.Clientes.AsNoTracking().Select(c => new
        {
            c.Id, nombre = c.RazonSocial ?? (c.Nombre + " " + c.Apellido).Trim(),
            saldoVentas = visibleSales.Where(v => v.ClienteId == c.Id && v.SeguimientoSaldo &&
                v.FormaPago != FormaPago.Cuotas && v.Estado != EstadoVenta.Cancelada)
                .Sum(v => (decimal?)(v.PrecioTotal - v.MontoEntrega -
                    v.Cobros.Where(x => !x.Anulado).Sum(x => (decimal?)x.ImporteUsd))) ?? 0m,
            cuotas = visibleInstallments.Where(q => q.ClienteId == c.Id && q.Venta.Estado != EstadoVenta.Cancelada)
                .Sum(q => (decimal?)(q.ImportePactado - q.ImportePagado)) ?? 0m,
            vencida = visibleInstallments.Where(q => q.ClienteId == c.Id && q.Venta.Estado != EstadoVenta.Cancelada &&
                q.FechaVencimiento < today && q.ImportePagado < q.ImportePactado)
                .Sum(q => (decimal?)(q.ImportePactado - q.ImportePagado)) ?? 0m,
            cantidadVentas = visibleSales.Count(v => v.ClienteId == c.Id && v.SeguimientoSaldo &&
                v.FormaPago != FormaPago.Cuotas && v.Estado != EstadoVenta.Cancelada &&
                v.PrecioTotal - v.MontoEntrega - (v.Cobros.Where(x => !x.Anulado).Sum(x => (decimal?)x.ImporteUsd) ?? 0m) > 0),
            cantidadCuotas = visibleInstallments.Count(q => q.ClienteId == c.Id && q.Venta.Estado != EstadoVenta.Cancelada && q.ImportePagado < q.ImportePactado),
            ultimoCobroVenta = db.CobrosVenta.Where(x => visibleSales.Any(v => v.Id == x.VentaId) && x.Venta.ClienteId == c.Id && !x.Anulado).Max(x => (DateOnly?)x.FechaCobro),
            ultimoCobroCuota = db.MovimientosCaja.Where(x => x.CuotaId != null && x.Tipo == TipoMovimiento.Ingreso &&
                visibleInstallments.Any(q => q.Id == x.CuotaId && q.ClienteId == c.Id)).Max(x => (DateTimeOffset?)x.Fecha)
        }).Where(x => x.saldoVentas + x.cuotas > 0);
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim().ToLower(); query = query.Where(x => x.nombre.ToLower().Contains(term));
        }
        query = string.Equals(orden, "atraso", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(x => x.vencida).ThenByDescending(x => x.saldoVentas + x.cuotas)
            : query.OrderByDescending(x => x.saldoVentas + x.cuotas);
        var total = await query.CountAsync(ct);
        var blue = await dolar.GetAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new { items = items.Select(x => new { x.Id, x.nombre,
            deudaTotalUsd = Math.Max(0, x.saldoVentas) + Math.Max(0, x.cuotas), deudaVencidaUsd = Math.Max(0, x.vencida),
            x.cantidadVentas, x.cantidadCuotas,
            ultimoCobro = Latest(x.ultimoCobroVenta, x.ultimoCobroCuota),
            deudaTotalArs = blue?.Venta is > 0 ? (Math.Max(0, x.saldoVentas) + Math.Max(0, x.cuotas)) * blue.Venta : (decimal?)null }),
            total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize), cotizacionReferencia = blue?.Venta });
    }

    private static async Task<object?> Build(Guid id, ClaimsPrincipal user, AppDbContext db, DolarBlueService dolar, CancellationToken ct)
    {
        var client = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (client is null) return null;
        var today = FechaArgentina.Hoy();
        var visibleSales = VentaEndpoints.VisibleQuery(db.Ventas.AsNoTracking(), user);
        var sales = await visibleSales.Where(x => x.ClienteId == id && x.SeguimientoSaldo &&
                x.FormaPago != FormaPago.Cuotas && x.Estado != EstadoVenta.Cancelada)
            .Select(x => new { x.Id, x.FechaVenta, producto = x.TipoCesped.Nombre, x.PrecioTotal, x.MontoEntrega, x.Estado,
                cobrado = x.Cobros.Where(c => !c.Anulado).Sum(c => (decimal?)c.ImporteUsd) ?? 0m })
            .ToListAsync(ct);
        var saleRows = sales.Select(x => new { x.Id, x.FechaVenta, x.producto, total = x.PrecioTotal,
            cobrado = x.MontoEntrega + x.cobrado, saldo = VentaSaldo.Calcular(x.PrecioTotal, x.MontoEntrega, x.cobrado),
            x.Estado, diasDesdeVenta = today.DayNumber - x.FechaVenta.DayNumber }).Where(x => x.saldo > 0).ToList();
        var installments = await CuotaEndpoints.VisibleQuery(db.Cuotas.AsNoTracking(), user).Where(x => x.ClienteId == id && x.Venta.Estado != EstadoVenta.Cancelada &&
                x.ImportePagado < x.ImportePactado)
            .OrderBy(x => x.FechaVencimiento).Select(x => new { x.Id, x.VentaId, x.Numero, x.FechaVencimiento,
                pactado = x.ImportePactado, pagado = x.ImportePagado, pendiente = x.ImportePactado - x.ImportePagado, x.Estado })
            .ToListAsync(ct);
        var timeline = new List<AccountMovement>();
        timeline.AddRange(sales.Select(x => new AccountMovement(x.FechaVenta, "Venta", x.PrecioTotal,
            null, null, null, false, x.Id)));
        timeline.AddRange(sales.Where(x => x.MontoEntrega > 0).Select(x => new AccountMovement(x.FechaVenta,
            "Entrega inicial", -x.MontoEntrega, null, null, null, false, x.Id)));
        var chargeMovements = await db.CobrosVenta.AsNoTracking().Where(x => !x.Anulado &&
                visibleSales.Any(v => v.Id == x.VentaId) && x.Venta.ClienteId == id && x.Venta.Estado != EstadoVenta.Cancelada)
            .Select(x => new AccountMovement(x.FechaCobro, "Cobro de saldo", -x.ImporteUsd, x.MonedaCobro,
                x.ImporteMonedaCobro, x.Cotizacion, x.Anulado, x.VentaId)).ToListAsync(ct);
        timeline.AddRange(chargeMovements);
        var installmentSales = await visibleSales.Where(x => x.ClienteId == id && x.FormaPago == FormaPago.Cuotas &&
                x.Estado != EstadoVenta.Cancelada)
            .Select(x => new { x.Id, x.FechaVenta, x.PrecioTotal, x.MontoEntrega }).ToListAsync(ct);
        timeline.AddRange(installmentSales.Select(x => new AccountMovement(x.FechaVenta, "Venta en cuotas",
            x.PrecioTotal, null, null, null, false, x.Id)));
        timeline.AddRange(installmentSales.Where(x => x.MontoEntrega > 0).Select(x => new AccountMovement(
            x.FechaVenta, "Entrega inicial", -x.MontoEntrega, null, null, null, false, x.Id)));
        var installmentPayments = await db.MovimientosCaja.AsNoTracking()
            .Where(x => x.CuotaId != null && x.VentaId != null && x.Tipo == TipoMovimiento.Ingreso &&
                visibleSales.Any(v => v.Id == x.VentaId && v.ClienteId == id && v.Estado != EstadoVenta.Cancelada))
            .Select(x => new { x.Fecha, x.Monto, x.MonedaCobro, x.ImporteMonedaCobro, x.CotizacionAplicada, VentaId = x.VentaId!.Value })
            .ToListAsync(ct);
        timeline.AddRange(installmentPayments.Select(x => new AccountMovement(FechaArgentina.Hoy(x.Fecha), "Pago de cuota", -x.Monto,
                x.MonedaCobro, x.ImporteMonedaCobro, x.CotizacionAplicada, false, x.VentaId))
            );
        var installmentReversals = await db.MovimientosCaja.AsNoTracking()
            .Where(x => x.CuotaId != null && x.VentaId != null && x.Tipo == TipoMovimiento.Retiro &&
                x.Concepto.StartsWith("Anulaci") && visibleSales.Any(v => v.Id == x.VentaId && v.ClienteId == id))
            .Select(x => new { x.Fecha, x.Monto, x.MonedaCobro, x.ImporteMonedaCobro, x.CotizacionAplicada, VentaId = x.VentaId!.Value })
            .ToListAsync(ct);
        timeline.AddRange(installmentReversals.Select(x => new AccountMovement(FechaArgentina.Hoy(x.Fecha),
            "Anulación de pago de cuota", x.Monto, x.MonedaCobro, x.ImporteMonedaCobro,
            x.CotizacionAplicada, true, x.VentaId)));
        var debtSales = saleRows.Sum(x => x.saldo); var debtInstallments = installments.Sum(x => x.pendiente);
        var overdue = installments.Where(x => x.FechaVencimiento < today).Sum(x => x.pendiente);
        var blue = await dolar.GetAsync(ct); var total = debtSales + debtInstallments;
        return new
        {
            cliente = new { client.Id, nombre = client.RazonSocial ?? $"{client.Nombre} {client.Apellido}".Trim() },
            resumen = new { deudaTotalUsd = total, saldoVentasUsd = debtSales, cuotasPendientesUsd = debtInstallments,
                cuotasVencidasUsd = overdue, cantidadVentasConSaldo = saleRows.Count,
                proximoVencimiento = installments.Select(x => (DateOnly?)x.FechaVencimiento).FirstOrDefault(),
                deudaTotalArs = blue?.Venta is > 0 ? total * blue.Venta : (decimal?)null, cotizacionReferencia = blue?.Venta },
            ventasConSaldo = saleRows, cuotasPendientes = installments,
            movimientos = WithRunningBalance(timeline).OrderByDescending(x => x.Fecha).Take(200)
        };
    }

    private static DateOnly? Latest(DateOnly? charge, DateTimeOffset? installment)
    {
        var installmentDate = installment.HasValue ? FechaArgentina.Hoy(installment.Value) : (DateOnly?)null;
        if (!charge.HasValue) return installmentDate;
        if (!installmentDate.HasValue) return charge;
        return charge.Value >= installmentDate.Value ? charge : installmentDate;
    }

    private static IEnumerable<AccountMovementRow> WithRunningBalance(IEnumerable<AccountMovement> movements)
    {
        decimal balance = 0;
        return movements.OrderBy(x => x.Fecha).ThenBy(x => x.Concepto).Select(x =>
        {
            balance = decimal.Round(balance + x.ImporteUsd, 2);
            return new AccountMovementRow(x.Fecha, x.Concepto, x.ImporteUsd, x.Moneda, x.ImporteMoneda,
                x.Cotizacion, x.Anulado, x.VentaId, balance);
        }).ToList();
    }

    private static async Task<IResult> Csv(Guid id, ClaimsPrincipal user, AppDbContext db, DolarBlueService dolar, CancellationToken ct)
    {
        if (!VentaEndpoints.PuedeVerMontos(user)) return Results.Forbid();
        var data = await Build(id, user, db, dolar, ct); if (data is null) return Results.NotFound();
        var json = System.Text.Json.JsonSerializer.SerializeToElement(data);
        var sb = new StringBuilder("Tipo;Fecha;Concepto;Pendiente USD\r\n");
        foreach (var row in json.GetProperty("ventasConSaldo").EnumerateArray())
            sb.AppendLine($"Venta;{CsvCell(row.GetProperty("fechaVenta").GetString())};{CsvCell(row.GetProperty("producto").GetString())};{row.GetProperty("saldo").GetDecimal():0.00}");
        foreach (var row in json.GetProperty("cuotasPendientes").EnumerateArray())
            sb.AppendLine($"Cuota;{CsvCell(row.GetProperty("fechaVencimiento").GetString())};Cuota {row.GetProperty("numero").GetInt32()};{row.GetProperty("pendiente").GetDecimal():0.00}");
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return Results.File(utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes(sb.ToString())).ToArray(),
            "text/csv; charset=utf-8", $"Cuenta-corriente-{id}.csv");
    }

    private static async Task<IResult> Pdf(Guid id, ClaimsPrincipal user, AppDbContext db, DolarBlueService dolar,
        SucursalBrandingService brandingService, CancellationToken ct)
    {
        if (!VentaEndpoints.PuedeVerMontos(user)) return Results.Forbid();
        var client = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct); if (client is null) return Results.NotFound();
        var data = await Build(id, user, db, dolar, ct); var json = System.Text.Json.JsonSerializer.SerializeToElement(data);
        var branding = await brandingService.GetAsync(client.SucursalId, ct); if (branding is null) return Results.NotFound();
        var bytes = Document.Create(doc => doc.Page(page =>
        {
            page.Size(PageSizes.A4); page.Margin(35); page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9));
            page.Header().Row(r => { if (branding.Logo is not null) r.ConstantItem(150).Height(45).Image(branding.Logo).FitArea();
                r.RelativeItem().AlignRight().Text("ESTADO DE CUENTA").Bold().FontSize(18); });
            page.Content().PaddingTop(20).Column(c =>
            {
                c.Spacing(8); c.Item().Text($"Cliente: {client.RazonSocial ?? $"{client.Nombre} {client.Apellido}"}").Bold();
                c.Item().Text($"Fecha de emisión: {FechaArgentina.Hoy():dd/MM/yyyy}");
                c.Item().Text($"Saldo de ventas: USD {json.GetProperty("resumen").GetProperty("saldoVentasUsd").GetDecimal():N2}");
                c.Item().Text($"Cuotas pendientes: USD {json.GetProperty("resumen").GetProperty("cuotasPendientesUsd").GetDecimal():N2}");
                c.Item().Text($"TOTAL ADEUDADO: USD {json.GetProperty("resumen").GetProperty("deudaTotalUsd").GetDecimal():N2}").Bold().FontSize(14);
                c.Item().Text("Documento informativo. No válido como factura.").FontColor(Colors.Grey.Darken1);
            });
        })).GeneratePdf();
        return Results.File(bytes, "application/pdf", $"Estado-cuenta-{id}.pdf");
    }

    private static string CsvCell(string? value)
    {
        value ??= "";
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
