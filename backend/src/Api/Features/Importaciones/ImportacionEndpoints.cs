using System.Security.Claims;
using Api.Features.Auth;
using Api.Features.Stock;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Importaciones;

public sealed record ImportacionRequest(string Codigo, string? NumeroDespacho, DateOnly Fecha, string? Observaciones);

public static class ImportacionEndpoints
{
    public static void MapImportacionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/importaciones").WithTags("Importaciones");
        group.MapGet("/", List).RequirePermiso("stock", "ver");
        group.MapGet("/saldos", Saldos).RequirePermiso("stock", "ver");
        app.MapGet("/api/ventas/importaciones/saldos", Saldos).WithTags("Importaciones")
            .RequirePermiso("ventas", "crear");
        group.MapGet("/{id:guid}", Detail).RequirePermiso("stock", "ver");
        group.MapPost("/", Create).RequirePermiso("stock", "crear");
        group.MapPut("/{id:guid}", Update).RequirePermiso("stock", "editar");
        group.MapDelete("/{id:guid}", Delete).RequirePermiso("stock", "eliminar");
    }

    private static async Task<IResult> List(string? buscar, int page, int pageSize, ClaimsPrincipal user,
        AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize == 0 ? 20 : pageSize, 1, 100);
        var visibleDeposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        var query = db.Importaciones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim().ToLower();
            query = query.Where(x => x.Codigo.ToLower().Contains(term) ||
                (x.NumeroDespacho != null && x.NumeroDespacho.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Fecha).ThenBy(x => x.Codigo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Codigo, x.NumeroDespacho, x.Fecha, x.Observaciones,
                totalIngresadoM2 = x.Movimientos.Where(m => visibleDeposits.Contains(m.DepositoId) && m.Tipo == TipoMovimientoStock.Ingreso).Sum(m => (decimal?)m.CantidadM2) ?? 0,
                saldoM2 = x.Movimientos.Where(m => visibleDeposits.Contains(m.DepositoId)).Sum(m => (decimal?)(m.Tipo == TipoMovimientoStock.SalidaPorVenta || m.Tipo == TipoMovimientoStock.AjusteSalida ? -m.CantidadM2 : m.CantidadM2)) ?? 0
            }).ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    private static async Task<IResult> Detail(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var importacion = await db.Importaciones.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (importacion is null) return Results.NotFound();
        var visibleDeposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        var balances = await db.MovimientosStock.AsNoTracking()
            .Where(x => x.ImportacionId == id && visibleDeposits.Contains(x.DepositoId))
            .GroupBy(x => new { x.DepositoId, Deposito = x.Deposito.Nombre, x.TipoCespedId, Producto = x.TipoCesped.Nombre })
            .Select(x => new { x.Key.DepositoId, depositoNombre = x.Key.Deposito, x.Key.TipoCespedId, productoNombre = x.Key.Producto, saldoM2 = x.Sum(m => m.Tipo == TipoMovimientoStock.SalidaPorVenta || m.Tipo == TipoMovimientoStock.AjusteSalida ? -m.CantidadM2 : m.CantidadM2) })
            .OrderBy(x => x.depositoNombre).ThenBy(x => x.productoNombre).ToListAsync(ct);
        var movements = await db.MovimientosStock.AsNoTracking()
            .Where(x => x.ImportacionId == id && visibleDeposits.Contains(x.DepositoId))
            .OrderByDescending(x => x.Fecha).Take(50)
            .Select(x => new { x.Id, x.DepositoId, depositoNombre = x.Deposito.Nombre, x.TipoCespedId, productoNombre = x.TipoCesped.Nombre, x.Tipo, x.CantidadM2, x.Fecha, x.Usuario, x.Observaciones })
            .ToListAsync(ct);
        return Results.Ok(new { importacion.Id, importacion.Codigo, importacion.NumeroDespacho, importacion.Fecha, importacion.Observaciones, saldos = balances, movimientos = movements });
    }

    private static async Task<IResult> Create(ImportacionRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var branch = user.SucursalId();
        if (!branch.HasValue) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sucursalId"] = ["Elegí una sucursal activa."] });
        var code = request.Codigo.Trim();
        if (await db.Importaciones.AnyAsync(x => x.Codigo == code, ct))
            return Results.Conflict(new { message = "Ya existe una importación con ese código." });
        var entity = new Importacion { SucursalId = branch.Value, Codigo = code, NumeroDespacho = Clean(request.NumeroDespacho), Fecha = request.Fecha, Observaciones = Clean(request.Observaciones) };
        db.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Ya existe una importación con ese código." }); }
        return Results.Created($"/api/importaciones/{entity.Id}", entity);
    }

    private static async Task<IResult> Update(Guid id, ImportacionRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var entity = await db.Importaciones.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Results.NotFound();
        var code = request.Codigo.Trim();
        if (!entity.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase) &&
            await db.MovimientosStock.AnyAsync(x => x.ImportacionId == id && x.Tipo == TipoMovimientoStock.SalidaPorVenta, ct))
            return Results.Conflict(new { message = "No se puede cambiar el código porque la importación ya tiene ventas asociadas." });
        if (await db.Importaciones.AnyAsync(x => x.Id != id && x.Codigo == code, ct))
            return Results.Conflict(new { message = "Ya existe una importación con ese código." });
        entity.Codigo = code; entity.NumeroDespacho = Clean(request.NumeroDespacho); entity.Fecha = request.Fecha; entity.Observaciones = Clean(request.Observaciones);
        await db.SaveChangesAsync(ct); return Results.Ok(entity);
    }

    private static async Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Importaciones.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Results.NotFound();
        if (await db.MovimientosStock.AnyAsync(x => x.ImportacionId == id, ct) || await db.LotesStock.AnyAsync(x => x.ImportacionId == id, ct))
            return Results.Conflict(new { message = "No se puede eliminar una importación que tiene movimientos o lotes asociados." });
        db.Remove(entity); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> Saldos(Guid depositoId, Guid tipoCespedId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!await SucursalDepositoService.CanUseDeposit(user, db, depositoId, ct)) return Results.NotFound();
        var grouped = await db.MovimientosStock.AsNoTracking().Where(x => x.DepositoId == depositoId && x.TipoCespedId == tipoCespedId)
            .GroupBy(x => x.ImportacionId).Select(x => new { importacionId = x.Key, saldoM2 = x.Sum(m => m.Tipo == TipoMovimientoStock.SalidaPorVenta || m.Tipo == TipoMovimientoStock.AjusteSalida ? -m.CantidadM2 : m.CantidadM2) })
            .Where(x => x.saldoM2 > 0).ToListAsync(ct);
        var ids = grouped.Where(x => x.importacionId.HasValue).Select(x => x.importacionId!.Value).ToArray();
        var imports = await db.Importaciones.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var rows = grouped.Select(x => new { x.importacionId, codigo = x.importacionId.HasValue ? imports.GetValueOrDefault(x.importacionId.Value)?.Codigo : null, etiqueta = x.importacionId.HasValue ? imports.GetValueOrDefault(x.importacionId.Value)?.Codigo : "Stock anterior (sin código)", x.saldoM2, fecha = x.importacionId.HasValue ? imports.GetValueOrDefault(x.importacionId.Value)?.Fecha : DateOnly.MinValue })
            .OrderBy(x => x.fecha).ThenBy(x => x.codigo).ToList();
        return Results.Ok(rows);
    }

    private static Dictionary<string, string[]> Validate(ImportacionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Codigo)) errors["codigo"] = ["Ingresá el código de importación."];
        else if (request.Codigo.Trim().Length > 50) errors["codigo"] = ["El código puede tener hasta 50 caracteres."];
        if (request.NumeroDespacho?.Trim().Length > 100) errors["numeroDespacho"] = ["El número de despacho puede tener hasta 100 caracteres."];
        if (request.Observaciones?.Trim().Length > 1000) errors["observaciones"] = ["Las observaciones pueden tener hasta 1000 caracteres."];
        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
