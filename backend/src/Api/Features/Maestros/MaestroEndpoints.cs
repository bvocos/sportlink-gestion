using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Api.Features.Auth;
using Api.Shared.Common;
using System.Security.Claims;

namespace Api.Features.Maestros;

public record TipoCespedRequest(Guid DepositoId, string Nombre, string? Descripcion, string? DescripcionPresupuesto,
    string? EspecificacionesPresupuesto, string? FichaTecnicaUrl, decimal PrecioVentaM2, decimal PrecioContadoM2,
    decimal PrecioFinanciadoM2, decimal CostoM2, IReadOnlyList<string>? Colores, bool ControlPorLotes = false,
    bool Activo = true, IReadOnlyList<Guid>? DepositoIds = null);

public static class MaestroEndpoints
{
    public static void MapMaestroEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/maestros", GetAll).WithTags("Maestros").RequirePermiso("ventas", "ver");
        var types = app.MapGroup("/api/maestros/tipos-cesped").WithTags("Maestros - Tipos de césped");
        types.MapGet("/", GetTypes).RequirePermiso("administracion", "ver");
        types.MapGet("/depositos", ListVisibleDeposits).RequirePermiso("administracion", "ver");
        types.MapPost("/", CreateType).RequirePermiso("administracion", "crear");
        types.MapPut("/{id:guid}", UpdateType).RequirePermiso("administracion", "editar");
        types.MapDelete("/{id:guid}", DeleteType).RequirePermiso("administracion", "editar");
    }

    private static string[] Colors(TipoCesped type) => JsonSerializer.Deserialize<string[]>(type.ColoresJson) ?? [];

    internal static Guid[] AssignedDepositIds(TipoCesped type) =>
        type.Depositos.Select(x => x.DepositoId).Append(type.DepositoId)
            .Where(x => x != Guid.Empty).Distinct().ToArray();

    internal static Guid[] VisibleAssignedDepositIds(TipoCesped type, IReadOnlySet<Guid> visibleDepositIds) =>
        AssignedDepositIds(type).Where(visibleDepositIds.Contains).ToArray();

    private static object ToDto(TipoCesped type, IReadOnlyDictionary<Guid, string>? visibleDeposits = null)
    {
        var ids = visibleDeposits is null
            ? AssignedDepositIds(type)
            : VisibleAssignedDepositIds(type, visibleDeposits.Keys.ToHashSet());
        return new
        {
            type.Id, depositoId = ids.Length > 0 ? ids[0] : type.DepositoId, depositoIds = ids,
            depositos = ids.Where(id => visibleDeposits?.ContainsKey(id) == true)
                .Select(id => new { id, nombre = visibleDeposits![id] }).ToArray(),
            type.Nombre, type.Descripcion, type.DescripcionPresupuesto, type.EspecificacionesPresupuesto,
            type.FichaTecnicaUrl, type.PrecioVentaM2, type.PrecioContadoM2, type.PrecioFinanciadoM2, type.CostoM2,
            colores = Colors(type), type.ControlPorLotes, type.Activo
        };
    }

    private static async Task<Dictionary<Guid, string>> VisibleDepositNames(ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        await SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user)
            .ToDictionaryAsync(x => x.Id, x => x.Nombre, ct);

    private static async Task<IResult> ListVisibleDeposits(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var deposits = await SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user)
            .OrderBy(x => x.Nombre)
            .Select(x => new { x.Id, x.SucursalId, x.Nombre, x.Activo })
            .ToListAsync(ct);
        return Results.Ok(deposits);
    }

    private static async Task<IReadOnlyList<object>> GetTypes(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var deposits = await VisibleDepositNames(user, db, ct);
        return (await SucursalDepositoService.VisibleProducts(
                db.TiposCesped.AsNoTracking().Include(x => x.Depositos), user, db.Depositos.AsNoTracking())
            .OrderBy(x => x.Nombre).ToListAsync(ct)).Select(type => ToDto(type, deposits)).ToList();
    }

    private static async Task<object> GetAll(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var deposits = await VisibleDepositNames(user, db, ct);
        return new
        {
            tiposCesped = (await SucursalDepositoService.VisibleProducts(
                    db.TiposCesped.AsNoTracking().Include(x => x.Depositos).Where(x => x.Activo), user,
                    db.Depositos.AsNoTracking().Where(x => x.Activo))
                .OrderBy(x => x.Nombre).ToListAsync(ct)).Select(type => ToDto(type, deposits)).ToList(),
            alicuotasIva = await db.AlicuotasIva.AsNoTracking().OrderBy(x => x.Porcentaje).ToListAsync(ct),
            tiposCliente = Enum.GetNames<TipoCliente>(), formasPago = Enum.GetNames<FormaPago>()
        };
    }

    private static string[] NormalizeColors(IReadOnlyList<string>? colors) =>
        (colors ?? []).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    private static Guid[] NormalizeDeposits(TipoCespedRequest request) =>
        (request.DepositoIds is { Count: > 0 } ? request.DepositoIds : [request.DepositoId])
            .Where(x => x != Guid.Empty).Distinct().ToArray();

    private static Dictionary<string, string[]>? Validate(TipoCespedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return new() { ["nombre"] = ["El nombre es obligatorio."] };
        if (request.Nombre.Length > 150) return new() { ["nombre"] = ["El nombre no puede superar 150 caracteres."] };
        if (request.PrecioVentaM2 < 0) return new() { ["precioVentaM2"] = ["El precio de venta no puede ser negativo."] };
        if (request.PrecioContadoM2 < 0) return new() { ["precioContadoM2"] = ["El precio de contado no puede ser negativo."] };
        if (request.PrecioFinanciadoM2 < 0) return new() { ["precioFinanciadoM2"] = ["El precio financiado no puede ser negativo."] };
        if (request.CostoM2 < 0) return new() { ["costoM2"] = ["El costo no puede ser negativo."] };
        if (!string.IsNullOrWhiteSpace(request.FichaTecnicaUrl) &&
            (!Uri.TryCreate(request.FichaTecnicaUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            return new() { ["fichaTecnicaUrl"] = ["La ficha técnica debe ser una URL http o https válida."] };
        if (NormalizeColors(request.Colores).Any(x => x.Length > 100)) return new() { ["colores"] = ["Cada color puede tener hasta 100 caracteres."] };
        return null;
    }

    private static async Task<bool> DepositsAreValid(Guid[] ids, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        ids.Length > 0 && await SucursalDepositoService.VisibleDeposits(db.Depositos.Where(x => x.Activo), user)
            .CountAsync(x => ids.Contains(x.Id), ct) == ids.Length;

    private static async Task<IResult> CreateType(TipoCespedRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors is not null) return Results.ValidationProblem(errors);
        var depositIds = NormalizeDeposits(request);
        if (!await DepositsAreValid(depositIds, user, db, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoIds"] = ["Seleccioná al menos un depósito activo de la sucursal."] });
        var name = request.Nombre.Trim();
        if (await db.TiposCesped.AnyAsync(x => x.Nombre == name &&
            (depositIds.Contains(x.DepositoId) || x.Depositos.Any(d => depositIds.Contains(d.DepositoId))), ct))
            return Results.Conflict(new { message = "Ya existe un producto con ese nombre en uno de los depósitos seleccionados." });
        var type = new TipoCesped
        {
            DepositoId = depositIds[0], Nombre = name, Descripcion = request.Descripcion?.Trim(),
            DescripcionPresupuesto = request.DescripcionPresupuesto?.Trim(), EspecificacionesPresupuesto = request.EspecificacionesPresupuesto?.Trim(),
            FichaTecnicaUrl = request.FichaTecnicaUrl?.Trim(), PrecioVentaM2 = request.PrecioVentaM2,
            PrecioContadoM2 = request.PrecioContadoM2, PrecioFinanciadoM2 = request.PrecioFinanciadoM2,
            CostoM2 = request.CostoM2, ColoresJson = JsonSerializer.Serialize(NormalizeColors(request.Colores)),
            ControlPorLotes = request.ControlPorLotes, Activo = request.Activo,
            Depositos = depositIds.Select(x => new TipoCespedDeposito { DepositoId = x }).ToList()
        };
        db.TiposCesped.Add(type); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/maestros/tipos-cesped/{type.Id}", ToDto(type));
    }

    private static async Task<IResult> UpdateType(Guid id, TipoCespedRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors is not null) return Results.ValidationProblem(errors);
        var depositIds = NormalizeDeposits(request);
        if (!await DepositsAreValid(depositIds, user, db, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoIds"] = ["Seleccioná al menos un depósito activo de la sucursal."] });
        var visibleDepositIds = await SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user)
            .Select(x => x.Id).ToArrayAsync(ct);
        var type = await db.TiposCesped.Include(x => x.Depositos)
            .SingleOrDefaultAsync(x => x.Id == id &&
                (visibleDepositIds.Contains(x.DepositoId) || x.Depositos.Any(d => visibleDepositIds.Contains(d.DepositoId))), ct);
        if (type is null) return Results.NotFound();
        var name = request.Nombre.Trim();
        if (await db.TiposCesped.AnyAsync(x => x.Id != id && x.Nombre == name &&
            (depositIds.Contains(x.DepositoId) || x.Depositos.Any(d => depositIds.Contains(d.DepositoId))), ct))
            return Results.Conflict(new { message = "Ya existe un producto con ese nombre en uno de los depósitos seleccionados." });
        ApplyVisibleDepositAssignments(type, depositIds, visibleDepositIds);
        type.Nombre = name; type.Descripcion = request.Descripcion?.Trim();
        type.DescripcionPresupuesto = request.DescripcionPresupuesto?.Trim(); type.EspecificacionesPresupuesto = request.EspecificacionesPresupuesto?.Trim();
        type.FichaTecnicaUrl = request.FichaTecnicaUrl?.Trim(); type.PrecioVentaM2 = request.PrecioVentaM2;
        type.PrecioContadoM2 = request.PrecioContadoM2; type.PrecioFinanciadoM2 = request.PrecioFinanciadoM2;
        type.CostoM2 = request.CostoM2; type.ColoresJson = JsonSerializer.Serialize(NormalizeColors(request.Colores));
        type.ControlPorLotes = request.ControlPorLotes; type.Activo = request.Activo;
        await db.SaveChangesAsync(ct); return Results.Ok(ToDto(type));
    }

    internal static void ApplyVisibleDepositAssignments(TipoCesped type, IReadOnlyCollection<Guid> requestedDepositIds,
        IReadOnlyCollection<Guid> visibleDepositIds)
    {
        var visible = visibleDepositIds.ToHashSet();
        var requested = requestedDepositIds.ToHashSet();
        if (visible.Contains(type.DepositoId)) type.DepositoId = requestedDepositIds.First();
        foreach (var assignment in type.Depositos
                     .Where(x => visible.Contains(x.DepositoId) && !requested.Contains(x.DepositoId)).ToList())
            type.Depositos.Remove(assignment);
        foreach (var depositId in requestedDepositIds.Where(x => type.Depositos.All(d => d.DepositoId != x)))
            type.Depositos.Add(new TipoCespedDeposito { TipoCespedId = type.Id, DepositoId = depositId });
    }

    private static async Task<IResult> DeleteType(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var visibleDeposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        var type = await db.TiposCesped.SingleOrDefaultAsync(x => x.Id == id &&
            (visibleDeposits.Contains(x.DepositoId) || x.Depositos.Any(d => visibleDeposits.Contains(d.DepositoId))), ct);
        if (type is null) return Results.NotFound();
        var productId = id.ToString();
        if (await db.Ventas.AnyAsync(x => x.TipoCespedId == id || (x.LineasJson != null && x.LineasJson.Contains(productId)), ct) ||
            await db.MovimientosStock.AnyAsync(x => x.TipoCespedId == id, ct) || await db.LotesStock.AnyAsync(x => x.TipoCespedId == id, ct))
            return Results.Conflict(new { message = "Este tipo tiene ventas o movimientos de stock asociados. Podés desactivarlo en lugar de eliminarlo." });
        db.TiposCesped.Remove(type); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
}
