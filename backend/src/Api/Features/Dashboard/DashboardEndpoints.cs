using Api.Shared.Database;
using Api.Features.Rentabilidad;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Dashboard;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/dashboard", Get).WithTags("Dashboard").RequireAuthorization("dashboard");

    private static async Task<IResult> Get(
        AppDbContext db,
        DateOnly? desde,
        DateOnly? hasta,
        Guid? clienteId,
        Guid? tipoCespedId,
        string? estadoFinanciero,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var start = desde ?? today.AddDays(-6);
        var end = hasta ?? today;
        if (start > end)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["fechas"] = ["La fecha desde no puede ser posterior a la fecha hasta."] });
        if (end.DayNumber - start.DayNumber > 366)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["fechas"] = ["El período máximo permitido es de 367 días."] });

        if (!RentabilidadEndpoints.IsValidFinancialState(estadoFinanciero))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["estadoFinanciero"] = ["El estado financiero seleccionado no es válido."] });

        var filtered = db.Ventas.AsNoTracking()
            .Where(x => x.Estado != EstadoVenta.Cancelada && x.FechaVenta >= start && x.FechaVenta <= end);
        if (clienteId.HasValue) filtered = filtered.Where(x => x.ClienteId == clienteId.Value);
        if (tipoCespedId.HasValue) filtered = filtered.Where(x => x.TipoCespedId == tipoCespedId.Value);

        HashSet<Guid>? financialStateIds = null;
        if (!string.IsNullOrWhiteSpace(estadoFinanciero))
        {
            var umbral = (await db.Configuraciones.AsNoTracking()
                .SingleAsync(x => x.Clave == "UmbralMuyRentable", ct)).ValorDecimal;
            var candidates = await filtered.Include(x => x.Cliente).Include(x => x.AlicuotaIva)
                .ToListAsync(ct);
            var rows = await RentabilidadEndpoints.BuildRows(candidates, umbral, db, ct);
            financialStateIds = rows
                .Where(x => x.EstadoFinanciero.Equals(estadoFinanciero.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Id).ToHashSet();
            filtered = filtered.Where(x => financialStateIds.Contains(x.Id));
        }

        var grouped = await filtered
            .GroupBy(x => x.Estado == EstadoVenta.Entregada)
            .Select(g => new
            {
                finalizada = g.Key,
                cantidad = g.Count(),
                facturacion = g.Sum(x => x.PrecioTotal),
                metros = g.Sum(x => x.CantidadM2),
                gananciaNeta = g.Sum(x => x.GananciaNeta)
            }).ToListAsync(ct);

        var finalizadas = grouped.SingleOrDefault(x => x.finalizada);
        var enCurso = grouped.SingleOrDefault(x => !x.finalizada);
        var series = await filtered.GroupBy(x => x.FechaVenta)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                fecha = g.Key,
                facturacion = g.Sum(x => x.PrecioTotal),
                finalizadas = g.Where(x => x.Estado == EstadoVenta.Entregada).Sum(x => x.PrecioTotal),
                enCurso = g.Where(x => x.Estado != EstadoVenta.Entregada).Sum(x => x.PrecioTotal),
                gananciaNeta = g.Sum(x => x.GananciaNeta)
            }).ToListAsync(ct);

        var ventas = await filtered.Include(x => x.Cliente).Include(x => x.TipoCesped)
            .OrderByDescending(x => x.FechaVenta).ThenByDescending(x => x.CreatedAt).Take(8)
            .Select(x => new
            {
                x.Id,
                cliente = x.Cliente.Nombre + " " + x.Cliente.Apellido,
                tipoCesped = x.TipoCesped.Nombre,
                x.FechaVenta,
                x.PrecioTotal,
                x.CantidadM2,
                x.Estado
            }).ToListAsync(ct);

        var saldo = await db.MovimientosCaja.SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto, ct);
        var cuotasQuery = db.Cuotas.AsNoTracking()
            .Where(x => x.ImportePagado < x.ImportePactado)
            .Where(x => x.Venta.Estado != EstadoVenta.Cancelada && x.Venta.FechaVenta >= start && x.Venta.FechaVenta <= end)
            .Where(x => !clienteId.HasValue || x.Venta.ClienteId == clienteId.Value)
            .Where(x => !tipoCespedId.HasValue || x.Venta.TipoCespedId == tipoCespedId.Value);
        if (financialStateIds is not null)
            cuotasQuery = cuotasQuery.Where(x => financialStateIds.Contains(x.VentaId));
        var cuotasPendientes = await cuotasQuery.CountAsync(ct);

        var clientes = await db.Clientes.AsNoTracking().OrderBy(x => x.Apellido).ThenBy(x => x.Nombre)
            .Select(x => new { x.Id, nombre = x.Nombre + " " + x.Apellido }).ToListAsync(ct);
        var productos = await db.TiposCesped.AsNoTracking().OrderBy(x => x.Nombre)
            .Select(x => new { x.Id, x.Nombre, x.Activo }).ToListAsync(ct);

        var finalizadasCantidad = finalizadas?.cantidad ?? 0;
        var enCursoCantidad = enCurso?.cantidad ?? 0;
        var finalizadasFacturacion = finalizadas?.facturacion ?? 0;
        var enCursoFacturacion = enCurso?.facturacion ?? 0;
        return Results.Ok(new
        {
            periodo = new { desde = start, hasta = end },
            filtros = new { clientes, productos },
            total = new
            {
                cantidad = finalizadasCantidad + enCursoCantidad,
                facturacion = finalizadasFacturacion + enCursoFacturacion,
                metros = (finalizadas?.metros ?? 0) + (enCurso?.metros ?? 0),
                gananciaNeta = (finalizadas?.gananciaNeta ?? 0) + (enCurso?.gananciaNeta ?? 0)
            },
            finalizadas = new { cantidad = finalizadasCantidad, facturacion = finalizadasFacturacion },
            enCurso = new { cantidad = enCursoCantidad, facturacion = enCursoFacturacion },
            saldo,
            cuotasPendientes,
            series,
            ventas
        });
    }
}
