using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Rentabilidad;

public static class RentabilidadEndpoints
{
    public static void MapRentabilidadEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/rentabilidad", GetReport).WithTags("Rentabilidad");

    private static async Task<IResult> GetReport(AppDbContext db, CancellationToken ct)
    {
        var umbral = (await db.Configuraciones.AsNoTracking()
            .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
        var rows = await db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.Cuotas)
            .Where(x => x.Estado != EstadoVenta.Cancelada).ToListAsync(ct);
        var cobrosPorVenta = await db.MovimientosCaja.AsNoTracking()
            .Where(x => x.VentaId != null)
            .GroupBy(x => x.VentaId!.Value)
            .ToDictionaryAsync(x => x.Key, x => x.Sum(m => m.Tipo == TipoMovimiento.Ingreso ? m.Monto : -m.Monto), ct);
        var report = rows.Select(v =>
        {
            var cobrado = cobrosPorVenta.GetValueOrDefault(v.Id);
            var pendiente = v.PrecioTotal - cobrado;
            var costoOperativo = v.CostoCompraTotal + v.CostoEnvio + v.OtrosCostos;
            var costoTotal = costoOperativo + v.Iva;
            var gananciaBruta = v.PrecioTotal - costoOperativo;
            var gananciaNeta = v.PrecioTotal - costoTotal;
            var margen = v.PrecioTotal == 0 ? 0 : gananciaNeta / v.PrecioTotal;
            var estado = pendiente > 0 ? "Pendiente de cobro" : margen < 0 ? "En pérdida" : margen >= umbral ? "Muy rentable" : "Rentable";
            return new { v.Id, Cliente = v.Cliente.Nombre + " " + v.Cliente.Apellido,
                v.PrecioTotal, CostoOperativo = costoOperativo, v.Iva, CostoTotal = costoTotal,
                GananciaBruta = gananciaBruta, GananciaNeta = gananciaNeta, Margen = margen,
                v.MontoEntrega, TotalCobrado = cobrado, TotalPendiente = pendiente, EstadoFinanciero = estado };
        });
        return Results.Ok(report);
    }
}
