using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Maestros;

public record TipoCespedRequest(string Nombre, string? Descripcion, decimal PrecioVentaM2, decimal CostoM2, bool Activo = true);

public static class MaestroEndpoints
{
    public static void MapMaestroEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/maestros", GetAll).WithTags("Maestros").RequireAuthorization("ventas");
        var types = app.MapGroup("/api/maestros/tipos-cesped").WithTags("Maestros - Tipos de césped").RequireAuthorization("administracion");
        types.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            await db.TiposCesped.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct));
        types.MapPost("/", CreateType);
        types.MapPut("/{id:guid}", UpdateType);
        types.MapDelete("/{id:guid}", DeleteType);
    }

    private static async Task<object> GetAll(AppDbContext db, CancellationToken ct) => new
    {
        tiposCesped = await db.TiposCesped.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Nombre).ToListAsync(ct),
        alicuotasIva = await db.AlicuotasIva.AsNoTracking().OrderBy(x => x.Porcentaje).ToListAsync(ct),
        tiposCliente = Enum.GetNames<TipoCliente>(), formasPago = Enum.GetNames<FormaPago>()
    };

    private static Dictionary<string, string[]>? Validate(TipoCespedRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre)) return new() { ["nombre"] = ["El nombre es obligatorio."] };
        if (request.Nombre.Length > 150) return new() { ["nombre"] = ["El nombre no puede superar 150 caracteres."] };
        if (request.PrecioVentaM2 < 0) return new() { ["precioVentaM2"] = ["El precio de venta no puede ser negativo."] };
        if (request.CostoM2 < 0) return new() { ["costoM2"] = ["El costo no puede ser negativo."] };
        return null;
    }

    private static async Task<IResult> CreateType(TipoCespedRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors is not null) return Results.ValidationProblem(errors);
        var name = request.Nombre.Trim();
        if (await db.TiposCesped.AnyAsync(x => x.Nombre == name, ct)) return Results.Conflict(new { message = "Ya existe un tipo de césped con ese nombre." });
        var type = new TipoCesped { Nombre = name, Descripcion = request.Descripcion?.Trim(), PrecioVentaM2 = request.PrecioVentaM2, CostoM2 = request.CostoM2, Activo = request.Activo };
        db.TiposCesped.Add(type); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/maestros/tipos-cesped/{type.Id}", type);
    }

    private static async Task<IResult> UpdateType(Guid id, TipoCespedRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors is not null) return Results.ValidationProblem(errors);
        var type = await db.TiposCesped.FindAsync([id], ct); if (type is null) return Results.NotFound();
        var name = request.Nombre.Trim();
        if (await db.TiposCesped.AnyAsync(x => x.Id != id && x.Nombre == name, ct)) return Results.Conflict(new { message = "Ya existe un tipo de césped con ese nombre." });
        type.Nombre = name; type.Descripcion = request.Descripcion?.Trim(); type.PrecioVentaM2 = request.PrecioVentaM2; type.CostoM2 = request.CostoM2; type.Activo = request.Activo;
        await db.SaveChangesAsync(ct); return Results.Ok(type);
    }

    private static async Task<IResult> DeleteType(Guid id, AppDbContext db, CancellationToken ct)
    {
        var type = await db.TiposCesped.FindAsync([id], ct); if (type is null) return Results.NotFound();
        if (await db.Ventas.AnyAsync(x => x.TipoCespedId == id, ct))
            return Results.Conflict(new { message = "Este tipo tiene ventas asociadas. Podés desactivarlo en lugar de eliminarlo." });
        db.TiposCesped.Remove(type); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
}
