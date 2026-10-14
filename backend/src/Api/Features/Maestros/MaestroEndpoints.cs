using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Api.Features.Auth;
using Api.Shared.Common;
using System.Security.Claims;

namespace Api.Features.Maestros;

public record TipoCespedRequest(Guid DepositoId, string Nombre, string? Descripcion, string? DescripcionPresupuesto, string? EspecificacionesPresupuesto, string? FichaTecnicaUrl, decimal PrecioVentaM2, decimal PrecioContadoM2, decimal PrecioFinanciadoM2, decimal CostoM2,
    IReadOnlyList<string>? Colores, bool ControlPorLotes = false, bool Activo = true);

public static class MaestroEndpoints
{
    public static void MapMaestroEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/maestros", GetAll).WithTags("Maestros").RequirePermiso("ventas", "ver");
        var types = app.MapGroup("/api/maestros/tipos-cesped").WithTags("Maestros - Tipos de césped");
        types.MapGet("/", GetTypes).RequirePermiso("administracion", "ver");
        types.MapPost("/", CreateType).RequirePermiso("administracion", "crear");
        types.MapPut("/{id:guid}", UpdateType).RequirePermiso("administracion", "editar");
        types.MapDelete("/{id:guid}", DeleteType).RequirePermiso("administracion", "editar");
    }

    private static string[] Colors(TipoCesped type) =>
        JsonSerializer.Deserialize<string[]>(type.ColoresJson) ?? [];

    private static object ToDto(TipoCesped type) => new
    {
        type.Id, type.DepositoId, type.Nombre, type.Descripcion, type.DescripcionPresupuesto, type.EspecificacionesPresupuesto, type.FichaTecnicaUrl, type.PrecioVentaM2, type.PrecioContadoM2, type.PrecioFinanciadoM2, type.CostoM2,
        colores = Colors(type), type.ControlPorLotes, type.Activo
    };

    private static async Task<IReadOnlyList<object>> GetTypes(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var deposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        return (await db.TiposCesped.AsNoTracking().Where(x => deposits.Contains(x.DepositoId))
            .OrderBy(x => x.Nombre).ToListAsync(ct)).Select(ToDto).ToList();
    }

    private static async Task<object> GetAll(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var deposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking().Where(x => x.Activo), user).Select(x => x.Id);
        return new {
        tiposCesped = (await db.TiposCesped.AsNoTracking().Where(x => deposits.Contains(x.DepositoId) && x.Activo).OrderBy(x => x.Nombre)
            .ToListAsync(ct)).Select(ToDto).ToList(),
        alicuotasIva = await db.AlicuotasIva.AsNoTracking().OrderBy(x => x.Porcentaje).ToListAsync(ct),
        tiposCliente = Enum.GetNames<TipoCliente>(), formasPago = Enum.GetNames<FormaPago>()
        };
    }

    private static string[] NormalizeColors(IReadOnlyList<string>? colors) =>
        (colors ?? []).Select(x => x.Trim()).Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private static Dictionary<string, string[]>? Validate(TipoCespedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return new() { ["nombre"] = ["El nombre es obligatorio."] };
        if (request.Nombre.Length > 150) return new() { ["nombre"] = ["El nombre no puede superar 150 caracteres."] };
        if (request.PrecioVentaM2 < 0) return new() { ["precioVentaM2"] = ["El precio de venta no puede ser negativo."] };
        if (request.PrecioContadoM2 < 0) return new() { ["precioContadoM2"] = ["El precio de contado no puede ser negativo."] };
        if (request.PrecioFinanciadoM2 < 0) return new() { ["precioFinanciadoM2"] = ["El precio financiado no puede ser negativo."] };
        if (request.CostoM2 < 0) return new() { ["costoM2"] = ["El costo no puede ser negativo."] };
        if (!string.IsNullOrWhiteSpace(request.FichaTecnicaUrl) && (!Uri.TryCreate(request.FichaTecnicaUrl.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            return new() { ["fichaTecnicaUrl"] = ["La ficha técnica debe ser una URL http o https válida."] };
        if (NormalizeColors(request.Colores).Any(x => x.Length > 100))
            return new() { ["colores"] = ["Cada color puede tener hasta 100 caracteres."] };
        return null;
    }

    private static async Task<IResult> CreateType(TipoCespedRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors is not null) return Results.ValidationProblem(errors);
        var depositoId = request.DepositoId;
        if (!await SucursalDepositoService.CanUseDeposit(user, db, depositoId, ct)) return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoId"] = ["Seleccioná un depósito activo de la sucursal."] });
        var name = request.Nombre.Trim();
        if (await db.TiposCesped.AnyAsync(x => x.DepositoId == depositoId && x.Nombre == name, ct)) return Results.Conflict(new { message = "Ya existe un tipo de césped con ese nombre en este depósito." });
        var type = new TipoCesped { DepositoId = depositoId, Nombre = name, Descripcion = request.Descripcion?.Trim(), DescripcionPresupuesto = request.DescripcionPresupuesto?.Trim(), EspecificacionesPresupuesto = request.EspecificacionesPresupuesto?.Trim(), FichaTecnicaUrl = request.FichaTecnicaUrl?.Trim(), PrecioVentaM2 = request.PrecioVentaM2, PrecioContadoM2 = request.PrecioContadoM2, PrecioFinanciadoM2 = request.PrecioFinanciadoM2, CostoM2 = request.CostoM2, ColoresJson = JsonSerializer.Serialize(NormalizeColors(request.Colores)), ControlPorLotes = request.ControlPorLotes, Activo = request.Activo };
        db.TiposCesped.Add(type); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/maestros/tipos-cesped/{type.Id}", ToDto(type));
    }

    private static async Task<IResult> UpdateType(Guid id, TipoCespedRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors is not null) return Results.ValidationProblem(errors);
        var depositoId = request.DepositoId;
        if (!await SucursalDepositoService.CanUseDeposit(user, db, depositoId, ct)) return Results.NotFound();
        var type = await db.TiposCesped.SingleOrDefaultAsync(x => x.Id == id && x.DepositoId == depositoId, ct); if (type is null) return Results.NotFound();
        var name = request.Nombre.Trim();
        if (await db.TiposCesped.AnyAsync(x => x.Id != id && x.DepositoId == depositoId && x.Nombre == name, ct)) return Results.Conflict(new { message = "Ya existe un tipo de césped con ese nombre en este depósito." });
        type.Nombre = name; type.Descripcion = request.Descripcion?.Trim(); type.DescripcionPresupuesto = request.DescripcionPresupuesto?.Trim(); type.EspecificacionesPresupuesto = request.EspecificacionesPresupuesto?.Trim(); type.FichaTecnicaUrl = request.FichaTecnicaUrl?.Trim(); type.PrecioVentaM2 = request.PrecioVentaM2; type.PrecioContadoM2 = request.PrecioContadoM2; type.PrecioFinanciadoM2 = request.PrecioFinanciadoM2; type.CostoM2 = request.CostoM2; type.ColoresJson = JsonSerializer.Serialize(NormalizeColors(request.Colores)); type.ControlPorLotes = request.ControlPorLotes; type.Activo = request.Activo;
        await db.SaveChangesAsync(ct); return Results.Ok(ToDto(type));
    }

    private static async Task<IResult> DeleteType(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var visibleDeposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        var type = await db.TiposCesped.SingleOrDefaultAsync(x => x.Id == id && visibleDeposits.Contains(x.DepositoId), ct); if (type is null) return Results.NotFound();
        var productId = id.ToString();
        if (await db.Ventas.AnyAsync(x => x.TipoCespedId == id ||
            (x.LineasJson != null && x.LineasJson.Contains(productId)), ct) ||
            await db.MovimientosStock.AnyAsync(x => x.TipoCespedId == id, ct) ||
            await db.LotesStock.AnyAsync(x => x.TipoCespedId == id, ct))
            return Results.Conflict(new { message = "Este tipo tiene ventas o movimientos de stock asociados. Podés desactivarlo en lugar de eliminarlo." });
        db.TiposCesped.Remove(type); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
}
