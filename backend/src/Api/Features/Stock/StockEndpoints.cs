using System.Security.Claims;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Stock;

public record IngresoStockRequest(Guid DepositoId, Guid TipoCespedId, decimal CantidadM2, string? Observaciones);
public record RolloLoteRequest(char Posicion, string CodigoBarra, decimal CantidadM2);
public record IngresoLoteRequest(Guid DepositoId, Guid TipoCespedId, string? Color, IReadOnlyList<RolloLoteRequest>? Rollos, string? Observaciones);
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
        group.MapPost("/lotes", RegisterLote);
        group.MapGet("/lotes", GetLotes);
        group.MapGet("/lotes/buscar-por-codigo", SearchByBarcode);
    }

    internal static StockAuthorizationFailure? ValidateIngresoScope(ClaimsPrincipal currentUser, Guid depositoId, Guid? depositoPropioId)
    {
        if (currentUser.IsInRole("Administrador")) return null;
        return depositoPropioId.HasValue && depositoPropioId.Value == depositoId ? null : new(StatusCodes.Status403Forbidden, RestrictedDepositMessage);
    }

    internal static decimal SignedQuantity(TipoMovimientoStock tipo, decimal cantidadM2) => tipo == TipoMovimientoStock.SalidaPorVenta ? -cantidadM2 : cantidadM2;

    internal static Dictionary<string, string[]> ValidateRolls(IReadOnlyList<RolloLoteRequest>? rolls)
    {
        var errors = new Dictionary<string, string[]>();
        if (rolls is null || rolls.Count != 3)
        {
            errors["rollos"] = ["El lote debe contener exactamente 3 rollos, en las posiciones A, B y C."];
            return errors;
        }
        var positions = rolls.Select(x => char.ToUpperInvariant(x.Posicion)).ToArray();
        if (positions.Distinct().Count() != 3 || !positions.Order().SequenceEqual(new[] { 'A', 'B', 'C' })) errors["rollos"] = ["Las posiciones deben ser A, B y C, sin repetirse."];
        if (rolls.Any(x => string.IsNullOrWhiteSpace(x.CodigoBarra))) errors["codigoBarra"] = ["Los tres códigos de barra son obligatorios."];
        else if (rolls.Select(x => x.CodigoBarra.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3) errors["codigoBarra"] = ["Los códigos de barra no pueden repetirse dentro del lote."];
        if (rolls.Any(x => x.CantidadM2 <= 0)) errors["cantidadM2"] = ["Los m² de cada rollo deben ser mayores que cero."];
        if (rolls.Any(x => x.CodigoBarra.Trim().Length > 150)) errors["codigoBarra"] = ["Cada código de barra puede tener hasta 150 caracteres."];
        return errors;
    }

    internal static (LoteStock Lote, MovimientoStock Movimiento) CreateLotRegistration(IngresoLoteRequest request, string user, DateTime now)
    {
        var rolls = request.Rollos!.Select(x => new Rollo { Posicion = char.ToUpperInvariant(x.Posicion), CodigoBarra = x.CodigoBarra.Trim(), CantidadM2 = x.CantidadM2 }).OrderBy(x => x.Posicion).ToList();
        var lot = new LoteStock { DepositoId = request.DepositoId, TipoCespedId = request.TipoCespedId, Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(), FechaIngreso = now, Usuario = user, Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim(), Estado = EstadoLoteStock.Disponible, Rollos = rolls };
        var movement = new MovimientoStock { DepositoId = request.DepositoId, TipoCespedId = request.TipoCespedId, Tipo = TipoMovimientoStock.Ingreso, CantidadM2 = rolls.Sum(x => x.CantidadM2), Fecha = now, Usuario = user, Observaciones = lot.Observaciones };
        return (lot, movement);
    }

    internal static bool HasPreviouslyUsedBarcode(IEnumerable<string> requestedCodes, IEnumerable<string> existingCodes)
    {
        var existing = new HashSet<string>(existingCodes.Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
        return requestedCodes.Any(x => existing.Contains(x.Trim()));
    }

    private static async Task<Guid?> GetOwnDepositId(ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        if (currentUser.IsInRole("Administrador")) return null;
        if (!Guid.TryParse(currentUser.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await db.Usuarios.AsNoTracking().Where(x => x.Id == userId).Select(x => x.SucursalId.HasValue ? (Guid?)x.Sucursal!.DepositoPropioId : null).SingleOrDefaultAsync(ct);
    }

    private static async Task<IResult> GetStock(ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var ownDepositId = await GetOwnDepositId(currentUser, db, ct);
        var isAdmin = currentUser.IsInRole("Administrador");
        var deposits = await db.Depositos.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Nombre).Select(x => new
        {
            x.Id, x.Nombre,
            productos = db.TiposCesped.AsNoTracking().Where(p => p.Activo).OrderBy(p => p.Nombre).Select(p => new
            {
                tipoCespedId = p.Id, nombre = p.Nombre, p.ControlPorLotes, p.ColoresJson,
                stockActualM2 = db.MovimientosStock.Where(m => m.DepositoId == x.Id && m.TipoCespedId == p.Id).Sum(m => (decimal?)(m.Tipo == TipoMovimientoStock.SalidaPorVenta ? -m.CantidadM2 : m.CantidadM2)) ?? 0m
            }).ToList()
        }).ToListAsync(ct);
        return Results.Ok(deposits.Select(x => new { x.Id, x.Nombre, productos = x.productos.Select(p => new { p.tipoCespedId, p.nombre, p.ControlPorLotes, colores = DeserializeColors(p.ColoresJson), p.stockActualM2 }), permiteRegistrarIngreso = isAdmin || ownDepositId == x.Id }));
    }

    private static async Task<IResult> GetMovimientos(Guid? depositoId, Guid? tipoCespedId, DateTime? desde, DateTime? hasta, int page, int pageSize, AppDbContext db, CancellationToken ct)
    {
        if (desde.HasValue && hasta.HasValue && desde.Value.Date > hasta.Value.Date) return Results.ValidationProblem(new Dictionary<string, string[]> { ["desde"] = ["La fecha desde no puede ser posterior a la fecha hasta."] });
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);
        var query = db.MovimientosStock.AsNoTracking().Include(x => x.Deposito).Include(x => x.TipoCesped).AsQueryable();
        if (depositoId.HasValue) query = query.Where(x => x.DepositoId == depositoId.Value);
        if (tipoCespedId.HasValue) query = query.Where(x => x.TipoCespedId == tipoCespedId.Value);
        if (desde.HasValue) query = query.Where(x => x.Fecha >= desde.Value.Date);
        if (hasta.HasValue) { var end = hasta.Value.Date.AddDays(1); query = query.Where(x => x.Fecha < end); }
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.Fecha).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.DepositoId, depositoNombre = x.Deposito.Nombre, x.TipoCespedId, tipoCespedNombre = x.TipoCesped.Nombre, x.Tipo, x.CantidadM2, cantidadConSigno = x.Tipo == TipoMovimientoStock.SalidaPorVenta ? -x.CantidadM2 : x.CantidadM2, x.Fecha, x.Usuario, x.Observaciones, x.VentaId }).ToListAsync(ct);
        return Results.Ok(new { items, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    private static async Task<IResult> RegisterIngreso(IngresoStockRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(); var observations = request.Observaciones?.Trim();
        if (request.CantidadM2 <= 0) errors["cantidadM2"] = ["La cantidad debe ser mayor que cero."];
        if (observations?.Length > 500) errors["observaciones"] = ["Las observaciones no pueden superar 500 caracteres."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var deposit = await db.Depositos.SingleOrDefaultAsync(x => x.Id == request.DepositoId && x.Activo, ct);
        if (deposit is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoId"] = ["Seleccioná un depósito activo."] });
        var product = await db.TiposCesped.SingleOrDefaultAsync(x => x.Id == request.TipoCespedId && x.Activo, ct);
        if (product is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipoCespedId"] = ["Seleccioná un producto activo."] });
        if (product.ControlPorLotes) return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipoCespedId"] = ["Este producto se controla por lotes. Usá el ingreso por lote con sus tres rollos."] });
        var failure = ValidateIngresoScope(currentUser, request.DepositoId, await GetOwnDepositId(currentUser, db, ct));
        if (failure is not null) return Results.Problem(statusCode: failure.StatusCode, title: "Acceso denegado", detail: failure.Message);
        var movement = new MovimientoStock { DepositoId = deposit.Id, TipoCespedId = product.Id, Tipo = TipoMovimientoStock.Ingreso, CantidadM2 = request.CantidadM2, Fecha = DateTime.UtcNow, Usuario = UserName(currentUser), Observaciones = string.IsNullOrWhiteSpace(observations) ? null : observations };
        db.MovimientosStock.Add(movement); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/stock/movimientos/{movement.Id}", new { movement.Id, movement.DepositoId, movement.TipoCespedId, movement.CantidadM2, movement.Fecha });
    }

    private static async Task<IResult> RegisterLote(IngresoLoteRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateRolls(request.Rollos);
        if (request.Observaciones?.Trim().Length > 500) errors["observaciones"] = ["Las observaciones no pueden superar 500 caracteres."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var deposit = await db.Depositos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.DepositoId && x.Activo, ct);
        if (deposit is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoId"] = ["Seleccioná un depósito activo."] });
        var product = await db.TiposCesped.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.TipoCespedId && x.Activo, ct);
        if (product is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipoCespedId"] = ["Seleccioná un producto activo."] });
        if (!product.ControlPorLotes) return Results.ValidationProblem(new Dictionary<string, string[]> { ["tipoCespedId"] = ["Este producto no se controla por lotes. Usá el ingreso normal de stock."] });
        var colors = DeserializeColors(product.ColoresJson); var color = request.Color?.Trim();
        if (colors.Length > 0 && (string.IsNullOrWhiteSpace(color) || !colors.Contains(color, StringComparer.OrdinalIgnoreCase))) return Results.ValidationProblem(new Dictionary<string, string[]> { ["color"] = ["Seleccioná uno de los colores configurados para el producto."] });
        var failure = ValidateIngresoScope(currentUser, request.DepositoId, await GetOwnDepositId(currentUser, db, ct));
        if (failure is not null) return Results.Problem(statusCode: failure.StatusCode, title: "Acceso denegado", detail: failure.Message);
        var codes = request.Rollos!.Select(x => x.CodigoBarra.Trim()).ToArray();
        var existingCodes = await db.Rollos.Where(x => codes.Contains(x.CodigoBarra)).Select(x => x.CodigoBarra).ToListAsync(ct);
        if (HasPreviouslyUsedBarcode(codes, existingCodes)) return Results.Conflict(new { message = "Uno o más códigos de barra ya están registrados en otro lote." });
        var registration = CreateLotRegistration(request, UserName(currentUser), DateTime.UtcNow);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.LotesStock.Add(registration.Lote); db.MovimientosStock.Add(registration.Movimiento);
        try { await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); }
        catch (DbUpdateException) { await transaction.RollbackAsync(ct); return Results.Conflict(new { message = "No se pudo registrar el lote. Verificá que los códigos de barra no estén repetidos." }); }
        return Results.Created($"/api/stock/lotes/{registration.Lote.Id}", LotDto(registration.Lote, deposit.Nombre, product.Nombre));
    }

    private static async Task<IResult> GetLotes(Guid? depositoId, Guid? tipoCespedId, EstadoLoteStock? estado, AppDbContext db, CancellationToken ct)
    {
        var query = db.LotesStock.AsNoTracking().Include(x => x.Deposito).Include(x => x.TipoCesped).Include(x => x.Rollos).AsQueryable();
        if (depositoId.HasValue) query = query.Where(x => x.DepositoId == depositoId.Value);
        if (tipoCespedId.HasValue) query = query.Where(x => x.TipoCespedId == tipoCespedId.Value);
        if (estado.HasValue) query = query.Where(x => x.Estado == estado.Value);
        var lots = await query.OrderByDescending(x => x.FechaIngreso).ToListAsync(ct);
        return Results.Ok(lots.Select(x => LotDto(x, x.Deposito.Nombre, x.TipoCesped.Nombre)));
    }

    private static async Task<IResult> SearchByBarcode(string? codigoBarra, AppDbContext db, CancellationToken ct)
    {
        var code = codigoBarra?.Trim();
        if (string.IsNullOrWhiteSpace(code)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["codigoBarra"] = ["Ingresá un código de barra."] });
        var roll = await db.Rollos.AsNoTracking().Include(x => x.LoteStock).ThenInclude(x => x.Rollos).Include(x => x.LoteStock.Deposito).Include(x => x.LoteStock.TipoCesped).SingleOrDefaultAsync(x => x.CodigoBarra == code, ct);
        if (roll is null) return Results.NotFound(new { message = "No se encontró ningún rollo con ese código de barra." });
        object? sale = null;
        if (roll.LoteStock.VentaId.HasValue) sale = await db.Ventas.AsNoTracking().Where(x => x.Id == roll.LoteStock.VentaId.Value).Select(x => new { x.Id, x.FechaVenta, cliente = x.Cliente.Nombre + " " + x.Cliente.Apellido }).SingleOrDefaultAsync(ct);
        return Results.Ok(new { rollo = new { roll.Id, roll.Posicion, roll.CodigoBarra, roll.CantidadM2 }, lote = LotDto(roll.LoteStock, roll.LoteStock.Deposito.Nombre, roll.LoteStock.TipoCesped.Nombre), venta = sale });
    }

    private static object LotDto(LoteStock lot, string depositName, string productName) => new { lot.Id, lot.DepositoId, depositoNombre = depositName, lot.TipoCespedId, tipoCespedNombre = productName, lot.Color, lot.FechaIngreso, lot.Usuario, lot.Observaciones, lot.VentaId, lot.Estado, cantidadM2 = lot.Rollos.Sum(x => x.CantidadM2), rollos = lot.Rollos.OrderBy(x => x.Posicion).Select(x => new { x.Id, x.Posicion, x.CodigoBarra, x.CantidadM2 }) };
    private static string[] DeserializeColors(string json) => JsonSerializer.Deserialize<string[]>(json) ?? [];
    private static string UserName(ClaimsPrincipal user) => user.Identity?.Name ?? user.FindFirstValue("usuario") ?? "sistema";
}
