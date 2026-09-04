using System.Security.Claims;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Stock;

public record IngresoStockRequest(Guid DepositoId, Guid TipoCespedId, decimal CantidadM2, string? Observaciones);
internal record StockAuthorizationFailure(int StatusCode, string Message);

public static class StockEndpoints
{
    private const string RestrictedDepositMessage = "Sólo podés cargar ingresos en el depósito de tu sucursal.";

    public static void MapStockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stock").WithTags("Stock").RequireAuthorization("stock");
        group.MapGet("/", GetStock);
        group.MapGet("/movimientos", GetMovimientos);
        group.MapPost("/ingresos", RegisterIngreso);
    }

    internal static StockAuthorizationFailure? ValidateIngresoScope(
        ClaimsPrincipal currentUser, Guid depositoId, Guid? depositoPropioId)
    {
        if (currentUser.IsInRole("Administrador")) return null;
        return depositoPropioId.HasValue && depositoPropioId.Value == depositoId
            ? null
            : new(StatusCodes.Status403Forbidden, RestrictedDepositMessage);
    }

    internal static decimal SignedQuantity(TipoMovimientoStock tipo, decimal cantidadM2) =>
        tipo == TipoMovimientoStock.SalidaPorVenta ? -cantidadM2 : cantidadM2;

    private static async Task<Guid?> GetOwnDepositId(ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        if (currentUser.IsInRole("Administrador")) return null;
        if (!Guid.TryParse(currentUser.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await db.Usuarios.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => x.SucursalId.HasValue ? (Guid?)x.Sucursal!.DepositoPropioId : null)
            .SingleOrDefaultAsync(ct);
    }

    private static async Task<IResult> GetStock(ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var ownDepositId = await GetOwnDepositId(currentUser, db, ct);
        var isAdmin = currentUser.IsInRole("Administrador");
        var deposits = await db.Depositos.AsNoTracking().Where(x => x.Activo)
            .OrderBy(x => x.Nombre)
            .Select(x => new
            {
                x.Id,
                x.Nombre,
                productos = db.TiposCesped.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre)
                    .Select(p => new
                    {
                        tipoCespedId = p.Id,
                        nombre = p.Nombre,
                        stockActualM2 = db.MovimientosStock.Where(m => m.DepositoId == x.Id && m.TipoCespedId == p.Id)
                            .Sum(m => (decimal?)(m.Tipo == TipoMovimientoStock.SalidaPorVenta ? -m.CantidadM2 : m.CantidadM2)) ?? 0m
                    }).ToList()
            }).ToListAsync(ct);

        return Results.Ok(deposits.Select(x => new
        {
            x.Id,
            x.Nombre,
            x.productos,
            permiteRegistrarIngreso = isAdmin || ownDepositId == x.Id
        }));
    }

    private static async Task<IResult> GetMovimientos(
        Guid? depositoId, Guid? tipoCespedId, DateTime? desde, DateTime? hasta, int page, int pageSize,
        AppDbContext db, CancellationToken ct)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["desde"] = ["La fecha desde no puede ser posterior a la fecha hasta."] });
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);

        var query = db.MovimientosStock.AsNoTracking().Include(x => x.Deposito).Include(x => x.TipoCesped).AsQueryable();
        if (depositoId.HasValue) query = query.Where(x => x.DepositoId == depositoId.Value);
        if (tipoCespedId.HasValue) query = query.Where(x => x.TipoCespedId == tipoCespedId.Value);
        if (desde.HasValue) query = query.Where(x => x.Fecha >= desde.Value.Date);
        if (hasta.HasValue)
        {
            var exclusiveEnd = hasta.Value.Date.AddDays(1);
            query = query.Where(x => x.Fecha < exclusiveEnd);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Fecha)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.DepositoId,
                depositoNombre = x.Deposito.Nombre,
                x.TipoCespedId,
                tipoCespedNombre = x.TipoCesped.Nombre,
                x.Tipo,
                x.CantidadM2,
                cantidadConSigno = x.Tipo == TipoMovimientoStock.SalidaPorVenta ? -x.CantidadM2 : x.CantidadM2,
                x.Fecha,
                x.Usuario,
                x.Observaciones,
                x.VentaId
            }).ToListAsync(ct);

        return Results.Ok(new
        {
            items,
            total,
            page,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    private static async Task<IResult> RegisterIngreso(
        IngresoStockRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var observations = request.Observaciones?.Trim();
        if (request.CantidadM2 <= 0) errors["cantidadM2"] = ["La cantidad debe ser mayor que cero."];
        if (observations?.Length > 500) errors["observaciones"] = ["Las observaciones no pueden superar 500 caracteres."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var deposit = await db.Depositos.SingleOrDefaultAsync(x => x.Id == request.DepositoId && x.Activo, ct);
        if (deposit is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoId"] = ["Seleccioná un depósito activo."] });
        var product = await db.TiposCesped.SingleOrDefaultAsync(x => x.Id == request.TipoCespedId && x.Activo, ct);
        if (product is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipoCespedId"] = ["Seleccioná un producto activo."] });

        var ownDepositId = await GetOwnDepositId(currentUser, db, ct);
        var authorizationFailure = ValidateIngresoScope(currentUser, request.DepositoId, ownDepositId);
        if (authorizationFailure is not null)
            return Results.Problem(
                statusCode: authorizationFailure.StatusCode,
                title: "Acceso denegado",
                detail: authorizationFailure.Message);

        var movement = new MovimientoStock
        {
            DepositoId = deposit.Id,
            TipoCespedId = product.Id,
            Tipo = TipoMovimientoStock.Ingreso,
            CantidadM2 = request.CantidadM2,
            Fecha = DateTime.UtcNow,
            Usuario = currentUser.Identity?.Name ?? currentUser.FindFirstValue("usuario") ?? "sistema",
            Observaciones = string.IsNullOrWhiteSpace(observations) ? null : observations,
            VentaId = null
        };
        db.MovimientosStock.Add(movement);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/stock/movimientos/{movement.Id}", new
        {
            movement.Id,
            movement.DepositoId,
            movement.TipoCespedId,
            movement.Tipo,
            movement.CantidadM2,
            movement.Fecha,
            movement.Usuario,
            movement.Observaciones
        });
    }
}
