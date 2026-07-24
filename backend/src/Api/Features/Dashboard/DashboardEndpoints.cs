using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/dashboard", Get).WithTags("Dashboard").RequireAuthorization("dashboard");

    private static async Task<IResult> Get(AppDbContext db, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var inicioMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var ventasValidas = db.Ventas.AsNoTracking().Where(x => x.Estado != EstadoVenta.Cancelada);

        var ventas = await ventasValidas.Include(x => x.Cliente).Include(x => x.TipoCesped)
            .OrderByDescending(x => x.FechaVenta).ThenByDescending(x => x.CreatedAt).Take(5)
            .Select(x => new
            {
                x.Id, cliente = x.Cliente.Nombre + " " + x.Cliente.Apellido,
                tipoCesped = x.TipoCesped.Nombre, x.FechaVenta, x.PrecioTotal, x.CantidadM2, x.Estado
            }).ToListAsync(ct);

        var resumenHoy = await ventasValidas.Where(x => x.FechaVenta == hoy)
            .GroupBy(_ => 1)
            .Select(g => new { cantidad = g.Count(), facturacion = g.Sum(x => x.PrecioTotal), metros = g.Sum(x => x.CantidadM2) })
            .SingleOrDefaultAsync(ct);
        var resumenMes = await ventasValidas.Where(x => x.FechaVenta >= inicioMes && x.FechaVenta <= hoy)
            .GroupBy(_ => 1)
            .Select(g => new { cantidad = g.Count(), facturacion = g.Sum(x => x.PrecioTotal), metros = g.Sum(x => x.CantidadM2) })
            .SingleOrDefaultAsync(ct);
        var saldo = await db.MovimientosCaja.SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto, ct);
        var cuotasPendientes = await db.Cuotas.CountAsync(x => x.ImportePagado < x.ImportePactado, ct);

        return Results.Ok(new
        {
            ventas,
            saldo,
            cuotasPendientes,
            hoy = new
            {
                cantidad = resumenHoy?.cantidad ?? 0,
                facturacion = resumenHoy?.facturacion ?? 0,
                metros = resumenHoy?.metros ?? 0
            },
            mes = new
            {
                desde = inicioMes,
                hasta = hoy,
                cantidad = resumenMes?.cantidad ?? 0,
                facturacion = resumenMes?.facturacion ?? 0,
                metros = resumenMes?.metros ?? 0
            }
        });
    }
}
