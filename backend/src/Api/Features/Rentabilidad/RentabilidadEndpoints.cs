using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Rentabilidad;

public static class RentabilidadEndpoints
{
    public static void MapRentabilidadEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/rentabilidad", GetReport).WithTags("Rentabilidad").RequireAuthorization("rentabilidad");

    private static async Task<IResult> GetReport(DateOnly? desde, DateOnly? hasta, int page, int pageSize,
        AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (desde.HasValue && hasta.HasValue && desde > hasta)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["hasta"] = ["La fecha hasta debe ser igual o posterior a la fecha desde."] });

        var umbral = (await db.Configuraciones.AsNoTracking()
            .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
        var query = db.Ventas.AsNoTracking().Include(x => x.Cliente)
            .Where(x => x.Estado != EstadoVenta.Cancelada);
        if (desde.HasValue) query = query.Where(x => x.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(x => x.FechaVenta <= hasta.Value);

        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.FechaVenta).ThenByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ventaIds = rows.Select(x => x.Id).ToArray();
        var cobrosPorVenta = await db.MovimientosCaja.AsNoTracking()
            .Where(x => x.VentaId != null && ventaIds.Contains(x.VentaId.Value))
            .GroupBy(x => x.VentaId!.Value)
            .ToDictionaryAsync(x => x.Key,
                x => x.Sum(m => m.Tipo == TipoMovimiento.Ingreso ? m.Monto : -m.Monto), ct);

        var items = rows.Select(v =>
        {
            var cobrado = cobrosPorVenta.GetValueOrDefault(v.Id);
            var pendiente = v.PrecioTotal - cobrado;
            var costoOperativo = v.CostoCompraTotal + v.CostoEnvio + v.OtrosCostos;
            var costoTotal = costoOperativo + v.Iva;
            var gananciaBruta = v.PrecioTotal - costoOperativo;
            var gananciaNeta = v.PrecioTotal - costoTotal;
            var margen = v.PrecioTotal == 0 ? 0 : gananciaNeta / v.PrecioTotal;
            var estado = pendiente > 0 ? "Pendiente de cobro" : margen < 0 ? "En pérdida" :
                margen >= umbral ? "Muy rentable" : "Rentable";
            return new
            {
                v.Id, v.FechaVenta, Cliente = v.Cliente.Nombre + " " + v.Cliente.Apellido,
                v.PrecioTotal, CostoOperativo = costoOperativo, v.Iva, CostoTotal = costoTotal,
                GananciaBruta = gananciaBruta, GananciaNeta = gananciaNeta, Margen = margen,
                v.MontoEntrega, TotalCobrado = cobrado, TotalPendiente = pendiente, EstadoFinanciero = estado
            };
        }).ToList();

        return Results.Ok(new { items, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }
}
