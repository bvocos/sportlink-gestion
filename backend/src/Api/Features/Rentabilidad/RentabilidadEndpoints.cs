using System.Globalization;
using System.Text;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Rentabilidad;

public static class RentabilidadEndpoints
{
    private sealed record ReportRow(Guid Id, DateOnly FechaVenta, string Cliente, decimal PrecioTotal,
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

    private static IQueryable<Venta> FilteredQuery(AppDbContext db, string? buscar)
    {
        var query = db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.AlicuotaIva)
            .Where(x => x.Estado != EstadoVenta.Cancelada);
        buscar = buscar?.Trim();
        if (string.IsNullOrWhiteSpace(buscar)) return query;
        if (Guid.TryParse(buscar, out var ventaId))
            return query.Where(x => x.Id == ventaId ||
                (x.Cliente.Nombre + " " + x.Cliente.Apellido).Contains(buscar));
        return query.Where(x => (x.Cliente.Nombre + " " + x.Cliente.Apellido).Contains(buscar));
    }

    private static async Task<List<ReportRow>> BuildRows(List<Venta> ventas, decimal umbral,
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

    private static async Task<IResult> GetReport(string? buscar, int page, int pageSize,
        AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var umbral = (await db.Configuraciones.AsNoTracking()
            .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
        var query = FilteredQuery(db, buscar);
        var total = await query.CountAsync(ct);
        var aggregate = await query.GroupBy(_ => 1).Select(g => new
        {
            FacturacionTotal = g.Sum(x => x.PrecioTotal),
            CostoTotal = g.Sum(x =>
                x.CostoCompraTotal + x.CostoEnvio + x.OtrosCostos +
                Math.Round((x.CostoCompraTotal + x.CostoEnvio + x.OtrosCostos) *
                    x.AlicuotaIva.Porcentaje / 100, 2)),
            GananciaNetaTotal = g.Sum(x =>
                x.PrecioTotal - x.CostoCompraTotal - x.CostoEnvio - x.OtrosCostos -
                Math.Round((x.CostoCompraTotal + x.CostoEnvio + x.OtrosCostos) *
                    x.AlicuotaIva.Porcentaje / 100, 2))
        }).SingleOrDefaultAsync(ct);
        var facturacion = aggregate?.FacturacionTotal ?? 0;
        var ganancia = aggregate?.GananciaNetaTotal ?? 0;
        var totales = new
        {
            cantidadVentas = total, facturacionTotal = facturacion,
            costoTotal = aggregate?.CostoTotal ?? 0, gananciaNetaTotal = ganancia,
            margenPromedioPonderado = facturacion == 0 ? 0 : ganancia / facturacion
        };
        var ventas = await query.OrderByDescending(x => x.FechaVenta).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = await BuildRows(ventas, umbral, db, ct);
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

    private static async Task<IResult> ExportAll(string? buscar, AppDbContext db, CancellationToken ct)
    {
        var umbral = (await db.Configuraciones.AsNoTracking()
            .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
        var ventas = await FilteredQuery(db, buscar).OrderByDescending(x => x.FechaVenta)
            .ThenByDescending(x => x.CreatedAt).ToListAsync(ct);
        var rows = await BuildRows(ventas, umbral, db, ct);
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
