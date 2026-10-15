using System.Data;
using System.Security.Claims;
using System.Text;
using Api.Shared.Common;
using Api.Shared.Database;
using Api.Features.Auth;
using Api.Features.Cotizaciones;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Features.Cuotas;

public record PagoRequest(decimal Importe, string MedioPago, DateOnly FechaPago);
public record ActualizarVencimientoRequest(DateOnly FechaVencimiento);

public static class CuotaEndpoints
{
    internal static string? ValidatePayment(decimal importe, decimal saldoPendiente)
    {
        if (importe <= 0) return "El importe debe ser mayor a cero.";
        if (importe > saldoPendiente) return $"El pago no puede superar el saldo pendiente de {saldoPendiente:C}.";
        return null;
    }
    internal static string? ValidateCancellation(decimal importePagado, decimal importePactado)
    {
        if (importePagado <= 0) return "La cuota no tiene pagos para anular.";
        if (importePagado < importePactado) return "Sólo se puede anular una cuota completamente abonada.";
        return null;
    }
    internal static string? ValidateDueDate(DateOnly fechaVencimiento) =>
        fechaVencimiento == default ? "Ingresá una fecha de vencimiento válida." : null;
    internal static IQueryable<Cuota> VisibleQuery(IQueryable<Cuota> query, ClaimsPrincipal user,
        Guid? sucursalId = null) => query.WhereVisibleParaUsuario(user, sucursalId);
    internal static bool PuedeVerMontos(ClaimsPrincipal user) => user.IsInRole("Administrador") || PermisosMatriz.DesdeClaims(user).PuedeVerMontos("cuotas");
    internal static int NextReceiptNumber(int? currentMaximum) => (currentMaximum ?? 0) + 1;

    public static void MapCuotaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cuotas").WithTags("Cuotas");
        group.MapGet("/pendientes", (Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => List(user, db, false, sucursalId, ct)).RequirePermiso("cuotas", "ver");
        group.MapGet("/pendientes/resumen", ResumenPendiente).RequirePermiso("cuotas", "ver");
        group.MapGet("/abonadas", (Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => List(user, db, true, sucursalId, ct)).RequirePermiso("cuotas", "ver");
        group.MapPut("/{id:guid}/vencimiento", ActualizarVencimiento).RequirePermiso("cuotas", "editar");
        group.MapPost("/{id:guid}/pagos", RegistrarPago).RequirePermiso("cuotas", "registrarPago");
        group.MapPost("/{id:guid}/anular-pago", AnularPago).RequirePermiso("cuotas", "anularPago");
        group.MapGet("/{id:guid}/recibo", GenerarRecibo).RequirePermiso("cuotas", "ver");
        group.MapGet("/{id:guid}/comprobante", ObtenerComprobante).RequirePermiso("cuotas", "ver");
    }

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, bool abonadas, Guid? sucursalId, CancellationToken ct)
    {
        var query = VisibleQuery(db.Cuotas.AsNoTracking(), user, sucursalId)
            .Include(x => x.Venta).ThenInclude(x => x.Cliente)
            .Include(x => x.Venta).ThenInclude(x => x.TipoCesped)
            .Where(x => abonadas ? x.ImportePagado >= x.ImportePactado : x.ImportePagado < x.ImportePactado);
        var rows = await query.OrderByDescending(x => abonadas ? x.FechaPago : null).ThenBy(x => x.FechaVencimiento)
            .Select(x => new { x.Id, x.VentaId, cliente = x.Venta.Cliente.Nombre + " " + x.Venta.Cliente.Apellido,
                fechaVenta = x.Venta.FechaVenta, tipoCesped = x.Venta.TipoCesped.Nombre, totalVenta = x.Venta.PrecioTotal,
                x.Numero, x.FechaVencimiento, x.FechaPago, fechaImpacto = x.UpdatedAt ?? x.CreatedAt, x.MedioPago,
                x.ImportePactado, x.ImportePagado,
                estado = abonadas ? EstadoCuota.Pagada : x.FechaVencimiento < DateOnly.FromDateTime(DateTime.Today) && x.Estado != EstadoCuota.Pagada ? EstadoCuota.Vencida : x.Estado })
            .ToListAsync(ct);
        var verMontos = PuedeVerMontos(user);
        return Results.Ok(rows.Select(x => new
        {
            x.Id, x.VentaId, x.cliente, x.fechaVenta, x.tipoCesped,
            totalVenta = verMontos ? (decimal?)x.totalVenta : null,
            x.Numero, x.FechaVencimiento, x.FechaPago, x.fechaImpacto, x.MedioPago,
            importePactado = verMontos ? (decimal?)x.ImportePactado : null,
            importePagado = verMontos ? (decimal?)x.ImportePagado : null,
            saldoPendiente = verMontos ? (decimal?)(x.ImportePactado - x.ImportePagado) : null,
            x.estado
        }));
    }

    private static async Task<IResult> ResumenPendiente(string? buscar, Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var query = VisibleQuery(db.Cuotas.AsNoTracking(), user, sucursalId).Where(x => x.ImportePagado < x.ImportePactado);
        var term = buscar?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(x => EF.Functions.Like(x.Venta.Cliente.Nombre + " " + x.Venta.Cliente.Apellido, $"%{term}%"));
        var resumen = await query.GroupBy(_ => 1)
            .Select(g => new { cantidad = g.Count(), totalPendiente = g.Sum(x => x.ImportePactado - x.ImportePagado) })
            .SingleOrDefaultAsync(ct);
        if (!PuedeVerMontos(user)) return Results.Ok(new { cantidad = resumen?.cantidad ?? 0, totalPendiente = (decimal?)null });
        return Results.Ok(new { cantidad = resumen?.cantidad ?? 0, totalPendiente = (decimal?)(resumen?.totalPendiente ?? 0m) });
    }

    private static async Task<IResult> RegistrarPago(Guid id, PagoRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var medioPago = request.MedioPago?.Trim();
        if (string.IsNullOrWhiteSpace(medioPago) || medioPago.Length > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["medioPago"] = ["Seleccioná un medio de pago válido."] });
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var cuota = await VisibleQuery(db.Cuotas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
            if (cuota is null) return (IResult)Results.NotFound();
            var paymentError = ValidatePayment(request.Importe, cuota.ImportePactado - cuota.ImportePagado);
            if (paymentError is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["importe"] = [paymentError] });
            cuota.ImportePagado += request.Importe; cuota.FechaPago = request.FechaPago; cuota.MedioPago = medioPago;
            cuota.Estado = cuota.ImportePagado >= cuota.ImportePactado ? EstadoCuota.Pagada : EstadoCuota.PagadaParcial;
            var ultimoNumeroRecibo = await db.MovimientosCaja
                .Where(x => x.SucursalId == cuota.SucursalId && x.NumeroRecibo != null)
                .MaxAsync(x => (int?)x.NumeroRecibo, ct) ?? 0;
            db.MovimientosCaja.Add(new MovimientoCaja { Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow,
                Monto = request.Importe, Concepto = $"Pago cuota {cuota.Numero}", Usuario = UserName(user),
                CuotaId = cuota.Id, VentaId = cuota.VentaId, SucursalId = cuota.SucursalId,
                NumeroRecibo = NextReceiptNumber(ultimoNumeroRecibo) });
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return Results.Ok(new { cuota.Id, cuota.ImportePagado, cuota.Estado });
        });
    }

    private static async Task<IResult> AnularPago(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var cuota = await VisibleQuery(db.Cuotas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cuota is null) return Results.NotFound();
        var cancellationError = ValidateCancellation(cuota.ImportePagado, cuota.ImportePactado);
        if (cancellationError is not null) return Results.Conflict(new { message = cancellationError });
        var importeAnulado = cuota.ImportePagado;
        cuota.ImportePagado = 0; cuota.FechaPago = null; cuota.MedioPago = null; cuota.Estado = EstadoCuota.Pendiente;
        db.MovimientosCaja.Add(new MovimientoCaja { Tipo = TipoMovimiento.Retiro, Fecha = DateTimeOffset.UtcNow,
            Monto = importeAnulado, Concepto = $"Anulación cobro cuota {cuota.Numero}", Usuario = UserName(user),
            CuotaId = cuota.Id, VentaId = cuota.VentaId, SucursalId = cuota.SucursalId });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Ok(new { cuota.Id, importeAnulado, cuota.Estado });
        });
    }

    private static async Task<IResult> ActualizarVencimiento(Guid id, ActualizarVencimientoRequest request,
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var dueDateError = ValidateDueDate(request.FechaVencimiento);
        if (dueDateError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["fechaVencimiento"] = [dueDateError] });
        var cuota = await VisibleQuery(db.Cuotas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cuota is null) return Results.NotFound();
        cuota.FechaVencimiento = request.FechaVencimiento;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { cuota.Id, cuota.FechaVencimiento });
    }

    private static string UserName(ClaimsPrincipal user) =>
        user.Identity?.Name ?? user.FindFirstValue("usuario") ?? "sistema";

    private static async Task<ReciboData?> ObtenerDatosRecibo(
        Guid id, ClaimsPrincipal user, AppDbContext db,
        DolarBlueService dolarBlueService, CancellationToken ct)
    {
        var cuota = await VisibleQuery(db.Cuotas.AsNoTracking(), user)
            .Include(x => x.Venta).ThenInclude(x => x.Cliente)
            .Include(x => x.Venta).ThenInclude(x => x.TipoCesped)
            .Include(x => x.Sucursal)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cuota is null) return null;

        var movimiento = await db.MovimientosCaja.AsNoTracking()
            .Where(x => x.CuotaId == id && x.Tipo == TipoMovimiento.Ingreso)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (movimiento is null) return null;

        var sucursal = cuota.Sucursal!;
        var empresa = new ReciboEmisorDto(
            string.IsNullOrWhiteSpace(sucursal.RazonSocial) ? sucursal.Nombre : sucursal.RazonSocial,
            sucursal.DomicilioFiscal ?? "", sucursal.Localidad ?? "", sucursal.Telefono ?? "",
            sucursal.Email ?? "", sucursal.Cuit ?? "",
            sucursal.InicioActividades?.ToString("dd/MM/yyyy") ?? "",
            sucursal.IngresosBrutos ?? "", sucursal.CondicionIva ?? "");
        var cliente = cuota.Venta.Cliente;
        var localidad = string.Join(", ", new[] { cliente.Localidad, cliente.Provincia }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var dolarBlue = await dolarBlueService.GetAsync(ct);

        return new ReciboData(
            NumeroRecibo: movimiento.NumeroRecibo,
            PuntoVenta: sucursal.PuntoVentaAfip,
            Fecha: cuota.FechaPago ?? DateOnly.FromDateTime(DateTime.Today),
            Empresa: empresa,
            Cliente: new ReciboClienteDto(
                NombreCompleto: $"{cliente.Apellido.ToUpper()}, {cliente.Nombre.ToUpper()}",
                Domicilio: "",
                Localidad: localidad,
                Telefono: cliente.Telefono ?? "",
                CategoriaFiscal: "CONSUMIDOR FINAL",
                CUIT: ""),
            Cuota: new ReciboCuotaDto(
                Numero: cuota.Numero,
                TotalCuotas: cuota.Venta.CantidadCuotas,
                Producto: cuota.Venta.TipoCesped.Nombre),
            Pago: new ReciboPagoDto(
                Importe: cuota.ImportePagado,
                MedioPago: cuota.MedioPago ?? "Efectivo",
                ImporteEnLetras: NumerosEnLetras.ConvertirDolares(cuota.ImportePagado),
                CotizacionDolarBlueVenta: dolarBlue?.Venta));
    }

    private static async Task<IResult> GenerarRecibo(
        Guid id, ClaimsPrincipal user, AppDbContext db,
        DolarBlueService dolarBlueService,
        IWebHostEnvironment env, HttpContext http, CancellationToken ct)
    {
        var datos = await ObtenerDatosRecibo(id, user, db, dolarBlueService, ct);
        if (datos is null) return Results.NotFound();
        var bytes = CuotaReciboPdf.Generate(datos, Path.Combine(env.ContentRootPath, "Assets"));
        http.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        return Results.File(bytes, "application/pdf",
            $"Sportlink-Recibo-Cuota-{datos.Cuota.Numero:00}-{datos.Fecha:yyyyMMdd}.pdf");
    }

    private static async Task<IResult> ObtenerComprobante(
        Guid id, ClaimsPrincipal user, AppDbContext db,
        DolarBlueService dolarBlueService, CancellationToken ct)
    {
        var datos = await ObtenerDatosRecibo(id, user, db, dolarBlueService, ct);
        return datos is null ? Results.NotFound() : Results.Ok(datos);
    }
}

// ── Records ─────────────────────────────────────────────────────────────────────

internal sealed record ReciboEmisorDto(string RazonSocial, string Domicilio, string Localidad,
    string Telefono, string Email, string CUIT, string InicioActividades, string IngBrutos,
    string CondicionFiscal);

internal sealed record ReciboData(
    int? NumeroRecibo, int? PuntoVenta, DateOnly Fecha,
    ReciboEmisorDto Empresa, ReciboClienteDto Cliente, ReciboCuotaDto Cuota, ReciboPagoDto Pago);

internal sealed record ReciboClienteDto(
    string NombreCompleto, string Domicilio, string Localidad,
    string Telefono, string CategoriaFiscal, string CUIT);

internal sealed record ReciboCuotaDto(int Numero, int? TotalCuotas, string Producto);

internal sealed record ReciboPagoDto(
    decimal Importe, string MedioPago, string ImporteEnLetras, decimal? CotizacionDolarBlueVenta);

// ── PDF Generator ────────────────────────────────────────────────────────────────

internal static class CuotaReciboPdf
{
    private static readonly System.Globalization.CultureInfo Ars =
        System.Globalization.CultureInfo.GetCultureInfo("es-AR");

    private static string M(decimal v) => $"USD {v.ToString("N2", Ars)}";

    private static string Cotizacion(decimal? venta) => venta.HasValue
        ? $"El cambio del dólar es de ARS {venta.Value.ToString("N2", Ars)} por USD según el momento de emitir el comprobante."
        : "La cotización del dólar blue no estaba disponible al momento de emitir el comprobante.";

    public static byte[] Generate(ReciboData d, string assets)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;
        var logo = Path.Combine(assets, "sportlink-logo.png");

        return Document.Create(c => c.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9).FontColor(Colors.Black));

            page.Content().Border(1).BorderColor(Colors.Grey.Darken4).Column(main =>
            {
                // ── TOP HEADER ──────────────────────────────────────────────────
                main.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4).Row(r =>
                {
                    // Left: Logo + company info stacked
                    r.RelativeItem(5).BorderRight(1).BorderColor(Colors.Grey.Darken4).Column(left =>
                    {
                        left.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4)
                            .Padding(8).Height(52)
                            .Image(logo).FitArea();

                        left.Item().Padding(6).Column(col =>
                        {
                            col.Spacing(1);
                            col.Item().Text(d.Empresa.RazonSocial).Bold();
                            if (!string.IsNullOrWhiteSpace(d.Empresa.Domicilio))
                                col.Item().Text(d.Empresa.Domicilio);
                            if (!string.IsNullOrWhiteSpace(d.Empresa.Localidad))
                                col.Item().Text(d.Empresa.Localidad);
                            if (!string.IsNullOrWhiteSpace(d.Empresa.Telefono))
                                col.Item().Text($"TELEFONO: {d.Empresa.Telefono}");
                            if (!string.IsNullOrWhiteSpace(d.Empresa.Email))
                                col.Item().Text(d.Empresa.Email);
                        });
                    });

                    // Center: X box spans full height
                    r.ConstantItem(55).BorderRight(1).BorderColor(Colors.Grey.Darken4)
                        .AlignCenter().AlignMiddle()
                        .Text("X").FontSize(28).Bold();

                    // Right: RECIBO title + fiscal info stacked
                    r.RelativeItem(7).Column(right =>
                    {
                        right.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4).Padding(8).Column(col =>
                        {
                            col.Spacing(2);
                            col.Item().AlignRight().Text("DOCUMENTO NO VALIDO COMO FACTURA").FontSize(7.5f);
                            col.Item().AlignRight().Text("RECIBO").FontSize(22).Bold();
                            var numero = d.NumeroRecibo.HasValue
                                ? d.PuntoVenta.HasValue
                                    ? $"N° {d.PuntoVenta:00000}- {d.NumeroRecibo:00000000}"
                                    : $"N° {d.NumeroRecibo:00000000}"
                                : "";
                            if (numero.Length > 0)
                                col.Item().AlignRight().Text(numero).FontSize(11);
                            col.Item().AlignRight()
                                .Text($"FECHA: {d.Fecha:dd/MM/yyyy}").FontSize(11).Bold();
                        });

                        right.Item().Padding(6).Column(col =>
                        {
                            col.Spacing(2);
                            if (!string.IsNullOrWhiteSpace(d.Empresa.CondicionFiscal) ||
                                !string.IsNullOrWhiteSpace(d.Empresa.CUIT))
                                col.Item().Row(rr =>
                                {
                                    rr.RelativeItem().Text(d.Empresa.CondicionFiscal).Bold();
                                    rr.AutoItem().Text(string.IsNullOrWhiteSpace(d.Empresa.CUIT) ? "" : $"C.U.I.T.: {d.Empresa.CUIT}");
                                });
                            if (!string.IsNullOrWhiteSpace(d.Empresa.InicioActividades))
                                col.Item().Text($"INICIO ACT.: {d.Empresa.InicioActividades}");
                            if (!string.IsNullOrWhiteSpace(d.Empresa.IngBrutos))
                                col.Item().Text($"ING. BRUTOS: {d.Empresa.IngBrutos}");
                        });
                    });
                });

                // ── CLIENT INFO ─────────────────────────────────────────────────
                void LabelRow(string label, string value) =>
                    main.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4).Padding(3).Row(r =>
                    {
                        r.AutoItem().Text(label).Bold();
                        r.RelativeItem().PaddingLeft(4).Text(value);
                    });

                LabelRow("SEÑOR/ES:", d.Cliente.NombreCompleto);
                LabelRow("DOMICILIO:", d.Cliente.Domicilio);
                LabelRow("LOCALIDAD:", d.Cliente.Localidad);

                main.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4).Row(r =>
                {
                    r.RelativeItem(3).Padding(3).Row(rr =>
                    {
                        rr.AutoItem().Text("CATEGORIA FISCAL: ").Bold();
                        rr.RelativeItem().Text(d.Cliente.CategoriaFiscal);
                    });
                    r.ConstantItem(1).Background(Colors.Grey.Darken4);
                    r.RelativeItem(2).Padding(3).Row(rr =>
                    {
                        rr.AutoItem().Text("C.U.I.T.: ").Bold();
                        rr.RelativeItem().Text(d.Cliente.CUIT);
                    });
                });

                LabelRow("TELEFONOS:", d.Cliente.Telefono);
                LabelRow("CONCEPTO:", $"PAGO A CUENTA {d.Cuota.Producto.ToUpper()}");
                LabelRow("OBSERVACIONES:",
                    $"COBRO DE CUOTA {d.Cuota.Numero} DE {d.Cuota.TotalCuotas?.ToString() ?? "?"}");

                main.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4).Height(6);

                // ── PAYMENT TABLE ────────────────────────────────────────────────
                main.Item().Background(Colors.Grey.Lighten2).BorderBottom(1).BorderColor(Colors.Grey.Darken4)
                    .Padding(4).AlignCenter().Text("DETALLE DE PAGOS RECIBIDOS").Bold();

                main.Item().BorderBottom(1).BorderColor(Colors.Grey.Darken4).Row(r =>
                {
                    r.RelativeItem().Padding(5).Text(d.Pago.MedioPago.ToUpper());
                    r.ConstantItem(1).Background(Colors.Grey.Darken4);
                    r.ConstantItem(115).Padding(5).AlignRight().Text(M(d.Pago.Importe)).Bold();
                });

                main.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten1)
                    .Padding(5).Text(Cotizacion(d.Pago.CotizacionDolarBlueVenta)).FontSize(8);

                for (var i = 0; i < 3; i++)
                    main.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten1).Height(14);

                main.Item().BorderTop(1).BorderColor(Colors.Grey.Darken4).Row(r =>
                {
                    r.RelativeItem();
                    r.ConstantItem(1).Background(Colors.Grey.Darken4);
                    r.ConstantItem(240).Row(rr =>
                    {
                        rr.RelativeItem().BorderRight(1).BorderColor(Colors.Grey.Darken4)
                            .Padding(4).AlignRight().Text("TOTAL RECIBIDO:").Bold();
                        rr.ConstantItem(115).Padding(4).AlignRight().Text(M(d.Pago.Importe)).Bold();
                    });
                });

                // ── AMOUNT IN WORDS ──────────────────────────────────────────────
                main.Item().BorderTop(1).BorderColor(Colors.Grey.Darken4).Padding(8)
                    .DefaultTextStyle(s => s.FontSize(8.5f))
                    .Text(t =>
                    {
                        t.Span("Recibimos la suma de ");
                        t.Span(d.Pago.ImporteEnLetras).Bold();
                        t.Span(" en concepto de los items detallados anteriormente");
                    });

                // ── TOTAL ────────────────────────────────────────────────────────
                main.Item().BorderTop(1).BorderColor(Colors.Grey.Darken4).Row(r =>
                {
                    r.RelativeItem();
                    r.ConstantItem(180).Padding(8).Row(rr =>
                    {
                        rr.RelativeItem().Text("TOTAL:").Bold().FontSize(11);
                        rr.AutoItem().AlignRight().Text(M(d.Pago.Importe)).FontSize(12).Bold();
                    });
                });
            });
        })).GeneratePdf();
    }
}

// ── Conversor de números a letras (español) ───────────────────────────────────────

internal static class NumerosEnLetras
{
    private static readonly string[] Uni =
        ["", "un", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho", "nueve",
         "diez", "once", "doce", "trece", "catorce", "quince", "dieciséis",
         "diecisiete", "dieciocho", "diecinueve"];
    private static readonly string[] Dec =
        ["", "", "veinte", "treinta", "cuarenta", "cincuenta",
         "sesenta", "setenta", "ochenta", "noventa"];
    private static readonly string[] Cen =
        ["", "ciento", "doscientos", "trescientos", "cuatrocientos", "quinientos",
         "seiscientos", "setecientos", "ochocientos", "novecientos"];

    private static string Grupos(long n)
    {
        if (n <= 0) return "";
        var sb = new StringBuilder();

        if (n >= 1_000_000_000)
        {
            var m = n / 1_000_000_000;
            sb.Append(m == 1 ? "mil" : Grupos(m).Trim() + " mil");
            sb.Append(" millones ");
            n %= 1_000_000_000;
        }

        if (n >= 1_000_000)
        {
            var m = n / 1_000_000;
            sb.Append(m == 1 ? "un millón " : Grupos(m).Trim() + " millones ");
            n %= 1_000_000;
        }

        if (n >= 1000)
        {
            var m = n / 1000;
            sb.Append(m == 1 ? "mil " : Grupos(m).Trim() + " mil ");
            n %= 1000;
        }

        if (n >= 100)
        {
            sb.Append(n == 100 ? "cien " : Cen[n / 100] + " ");
            n %= 100;
        }

        if (n >= 20)
        {
            if (n is >= 21 and <= 29)
                sb.Append(n % 10 == 0 ? "veinte" : "veinti" + (n % 10 == 1 ? "ún" : Uni[n % 10]));
            else
            {
                sb.Append(Dec[n / 10]);
                if (n % 10 > 0) { sb.Append(" y "); sb.Append(Uni[n % 10]); }
            }
            sb.Append(' ');
        }
        else if (n > 0)
        {
            sb.Append(Uni[n]); sb.Append(' ');
        }

        return sb.ToString();
    }

    public static string ConvertirDolares(decimal importe)
    {
        var redondeado = Math.Round(Math.Max(importe, 0), 2, MidpointRounding.AwayFromZero);
        var entero = (long)Math.Floor(redondeado);
        var centavos = (int)((redondeado - entero) * 100);
        var texto = Grupos(entero).Trim();
        var terminaEnMillon = texto.EndsWith("millón") || texto.EndsWith("millones");
        var dolares = entero switch
        {
            0 => "cero dólares",
            1 => "un dólar",
            _ => $"{texto} {(terminaEnMillon ? "de " : "")}dólares"
        };
        var textoCentavos = centavos == 0 ? "cero" : Grupos(centavos).Trim();
        var unidadCentavos = centavos == 1 ? "centavo" : "centavos";
        return $"{dolares} con {textoCentavos} {unidadCentavos}";
    }
}
