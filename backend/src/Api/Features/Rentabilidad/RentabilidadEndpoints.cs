using System.Globalization;
using System.Text;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Rentabilidad;

public static class RentabilidadEndpoints
{
    internal sealed record ReportRow(Guid Id, DateOnly FechaVenta, string Cliente, decimal PrecioTotal,
        decimal CostoOperativo, decimal Iva, decimal CostoTotal, decimal GananciaBruta,
        decimal GananciaNeta, decimal Margen, decimal MontoEntrega, decimal TotalCobrado,
        decimal TotalPendiente, decimal SaldoPendienteCuotas, FormaPago FormaPago,
        string EstadoFinanciero);

    internal static (decimal TotalCobrado, decimal TotalPendiente) CalculateCollectionBalance(
        decimal precioTotal, decimal montoEntrega, decimal cuotasPagadas)
    {
        var cobrado = montoEntrega + cuotasPagadas;
        return (cobrado, Math.Max(precioTotal - cobrado, 0));
    }

    public static void MapRentabilidadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/rentabilidad").WithTags("Rentabilidad")
            .RequireAuthorization("rentabilidad");
        group.MapGet("/", GetReport);
        group.MapGet("/exportar", ExportAll);
    }

    internal static IQueryable<Venta> ApplyFilters(IQueryable<Venta> query, string? buscar,
        DateOnly? desde, DateOnly? hasta)
    {
        if (desde.HasValue) query = query.Where(x => x.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(x => x.FechaVenta <= hasta.Value);
        buscar = buscar?.Trim();
        if (string.IsNullOrWhiteSpace(buscar)) return query;
        if (Guid.TryParse(buscar, out var ventaId))
            return query.Where(x => x.Id == ventaId ||
                (x.Cliente.Nombre + " " + x.Cliente.Apellido).Contains(buscar));
        return query.Where(x => (x.Cliente.Nombre + " " + x.Cliente.Apellido).Contains(buscar));
    }

    private static IQueryable<Venta> FilteredQuery(AppDbContext db, string? buscar,
        DateOnly? desde, DateOnly? hasta) =>
        ApplyFilters(db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.AlicuotaIva)
            .Where(x => x.Estado != EstadoVenta.Cancelada), buscar, desde, hasta);

    internal static bool IsValidFinancialState(string? estado) => string.IsNullOrWhiteSpace(estado) ||
        FinancialStates.Contains(estado.Trim(), StringComparer.OrdinalIgnoreCase);

    internal static readonly string[] FinancialStates =
        ["Pendiente de cobro", "Rentable", "Muy rentable", "En pérdida"];

    internal static async Task<List<ReportRow>> BuildRows(List<Venta> ventas, decimal umbral,
        AppDbContext db, CancellationToken ct)
    {
        var ventaIds = ventas.Select(x => x.Id).ToArray();
        var cuotas = await db.Cuotas.AsNoTracking()
            .Where(x => ventaIds.Contains(x.VentaId))
            .GroupBy(x => x.VentaId)
            .ToDictionaryAsync(x => x.Key,
                x => new
                {
                    Pagado = x.Sum(c => c.ImportePagado),
                    Pendiente = x.Sum(c => c.ImportePactado - c.ImportePagado)
                }, ct);
        return ventas.Select(v =>
        {
            var resumenCuotas = cuotas.GetValueOrDefault(v.Id);
            var saldo = CalculateCollectionBalance(v.PrecioTotal, v.MontoEntrega,
                resumenCuotas?.Pagado ?? 0);
            var pendienteCuotas = Math.Max(resumenCuotas?.Pendiente ?? 0, 0);
            var costoOperativo = v.CostoCompraTotal + v.CostoEnvio + v.OtrosCostos;
            var iva = FinancialCalculator.CalculateIva(costoOperativo, v.AlicuotaIva.Porcentaje);
            var costoTotal = costoOperativo + iva;
            var gananciaBruta = v.PrecioTotal - costoOperativo;
            var gananciaNeta = v.PrecioTotal - costoTotal;
            var margen = v.PrecioTotal == 0 ? 0 : gananciaNeta / v.PrecioTotal;
            var estado = saldo.TotalPendiente > 0 ? "Pendiente de cobro" : margen < 0 ? "En pérdida" :
                margen >= umbral ? "Muy rentable" : "Rentable";
            return new ReportRow(v.Id, v.FechaVenta, v.Cliente.Nombre + " " + v.Cliente.Apellido,
                v.PrecioTotal, costoOperativo, iva, costoTotal, gananciaBruta, gananciaNeta,
                margen, v.MontoEntrega, saldo.TotalCobrado, saldo.TotalPendiente,
                pendienteCuotas, v.FormaPago, estado);
        }).ToList();
    }

    private static async Task<IResult> GetReport(string? buscar, DateOnly? desde, DateOnly? hasta,
        string? estadoFinanciero, int page, int pageSize,
        AppDbContext db, CancellationToken ct)
    {
        if (desde > hasta)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["fechas"] = ["La fecha desde no puede ser posterior a la fecha hasta."] });
        if (!IsValidFinancialState(estadoFinanciero))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["estadoFinanciero"] = ["El estado financiero seleccionado no es válido."] });
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var umbral = (await db.Configuraciones.AsNoTracking()
            .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
        var ventas = await FilteredQuery(db, buscar, desde, hasta)
            .OrderByDescending(x => x.FechaVenta).ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var rows = await BuildRows(ventas, umbral, db, ct);
        if (!string.IsNullOrWhiteSpace(estadoFinanciero))
            rows = rows.Where(x => x.EstadoFinanciero.Equals(estadoFinanciero.Trim(),
                StringComparison.OrdinalIgnoreCase)).ToList();
        var total = rows.Count;
        var facturacion = rows.Sum(x => x.PrecioTotal);
        var ganancia = rows.Sum(x => x.GananciaNeta);
        var totales = new
        {
            cantidadVentas = total, facturacionTotal = facturacion,
            costoTotal = rows.Sum(x => x.CostoTotal), gananciaNetaTotal = ganancia,
            margenPromedioPonderado = facturacion == 0 ? 0 : ganancia / facturacion
        };
        var items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Results.Ok(new
        {
            totales, items, page, pageSize, total,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    private static string CsvCell(object? value)
    {
        var text = value?.ToString() ?? "";
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }

    private static async Task<IResult> ExportAll(string? buscar, DateOnly? desde, DateOnly? hasta,
        string? estadoFinanciero, AppDbContext db, CancellationToken ct)
    {
        if (desde > hasta)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["fechas"] = ["La fecha desde no puede ser posterior a la fecha hasta."] });
        if (!IsValidFinancialState(estadoFinanciero))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["estadoFinanciero"] = ["El estado financiero seleccionado no es válido."] });
        var umbral = (await db.Configuraciones.AsNoTracking()
            .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
        var ventas = await FilteredQuery(db, buscar, desde, hasta).OrderByDescending(x => x.FechaVenta)
            .ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var rows = await BuildRows(ventas, umbral, db, ct);
        if (!string.IsNullOrWhiteSpace(estadoFinanciero))
            rows = rows.Where(x => x.EstadoFinanciero.Equals(estadoFinanciero.Trim(),
                StringComparison.OrdinalIgnoreCase)).ToList();
        var culture = CultureInfo.GetCultureInfo("es-AR");
        var csv = new StringBuilder();
        csv.AppendLine("ID venta;Fecha;Cliente;Venta;Costo operativo;IVA;Costo total;Ganancia bruta;Ganancia neta;Margen %;Cobrado;Pendiente total;Pendiente en cuotas;Estado");
        foreach (var row in rows)
            csv.AppendLine(string.Join(';', new[]
            {
                CsvCell(row.Id), CsvCell(row.FechaVenta.ToString("yyyy-MM-dd")), CsvCell(row.Cliente),
                CsvCell(row.PrecioTotal.ToString("0.00", culture)), CsvCell(row.CostoOperativo.ToString("0.00", culture)),
                CsvCell(row.Iva.ToString("0.00", culture)), CsvCell(row.CostoTotal.ToString("0.00", culture)),
                CsvCell(row.GananciaBruta.ToString("0.00", culture)), CsvCell(row.GananciaNeta.ToString("0.00", culture)),
                CsvCell((row.Margen * 100).ToString("0.00", culture)), CsvCell(row.TotalCobrado.ToString("0.00", culture)),
                CsvCell(row.TotalPendiente.ToString("0.00", culture)),
                CsvCell(row.SaldoPendienteCuotas.ToString("0.00", culture)), CsvCell(row.EstadoFinanciero)
            }));
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return Results.File(bytes, "text/csv; charset=utf-8",
            $"rentabilidad-{DateTime.Today:yyyy-MM-dd}.csv");
    }
}
