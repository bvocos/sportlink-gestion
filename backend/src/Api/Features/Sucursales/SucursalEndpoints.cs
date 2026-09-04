using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Sucursales;

public record DepositoRequest(string Nombre, bool Activo = true);
public record SucursalRequest(string Nombre, Guid DepositoPropioId, int? PuntoVentaAfip, bool Activo = true);
public record EstadoMaestroRequest(bool Activo);

public static class SucursalEndpoints
{
    public static void MapSucursalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sucursales")
            .WithTags("Administración - Sucursales")
            .RequireAuthorization("Administrador");

        group.MapGet("/depositos", ListDepositos);
        group.MapPost("/depositos", CreateDeposito);
        group.MapPut("/depositos/{id:guid}", UpdateDeposito);
        group.MapPatch("/depositos/{id:guid}/estado", SetDepositoEstado);
        group.MapDelete("/depositos/{id:guid}", DeleteDeposito);

        group.MapGet("/", ListSucursales);
        group.MapPost("/", CreateSucursal);
        group.MapPut("/{id:guid}", UpdateSucursal);
        group.MapPatch("/{id:guid}/estado", SetSucursalEstado);
        group.MapDelete("/{id:guid}", DeleteSucursal);
    }

    private static object DepositoDto(Deposito deposito) => new
    {
        deposito.Id,
        deposito.Nombre,
        deposito.Activo
    };

    private static object SucursalDto(Sucursal sucursal) => new
    {
        sucursal.Id,
        sucursal.Nombre,
        sucursal.DepositoPropioId,
        depositoPropioNombre = sucursal.DepositoPropio.Nombre,
        sucursal.PuntoVentaAfip,
        sucursal.Activo
    };

    private static Dictionary<string, string[]>? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new() { ["nombre"] = ["El nombre es obligatorio."] };
        if (name.Trim().Length > 150)
            return new() { ["nombre"] = ["El nombre no puede superar 150 caracteres."] };
        return null;
    }

    private static async Task<IReadOnlyList<object>> ListDepositos(AppDbContext db, CancellationToken ct) =>
        (await db.Depositos.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct))
            .Select(DepositoDto).ToList();

    private static async Task<IResult> CreateDeposito(DepositoRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateName(request.Nombre);
        if (errors is not null) return Results.ValidationProblem(errors);
        var name = request.Nombre.Trim();
        if (await db.Depositos.AnyAsync(x => x.Nombre == name, ct))
            return Results.Conflict(new { message = "Ya existe un depósito con ese nombre." });

        var deposito = new Deposito { Nombre = name, Activo = request.Activo };
        db.Depositos.Add(deposito);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/sucursales/depositos/{deposito.Id}", DepositoDto(deposito));
    }

    private static async Task<IResult> UpdateDeposito(Guid id, DepositoRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateName(request.Nombre);
        if (errors is not null) return Results.ValidationProblem(errors);
        var deposito = await db.Depositos.FindAsync([id], ct);
        if (deposito is null) return Results.NotFound();
        var name = request.Nombre.Trim();
        if (await db.Depositos.AnyAsync(x => x.Id != id && x.Nombre == name, ct))
            return Results.Conflict(new { message = "Ya existe un depósito con ese nombre." });
        if (!request.Activo && await db.Sucursales.AnyAsync(x => x.DepositoPropioId == id && x.Activo, ct))
            return Results.Conflict(new { message = "El depósito tiene sucursales activas asociadas. Desactivá primero esas sucursales." });

        deposito.Nombre = name;
        deposito.Activo = request.Activo;
        await db.SaveChangesAsync(ct);
        return Results.Ok(DepositoDto(deposito));
    }

    private static async Task<IResult> SetDepositoEstado(Guid id, EstadoMaestroRequest request, AppDbContext db, CancellationToken ct)
    {
        var deposito = await db.Depositos.FindAsync([id], ct);
        if (deposito is null) return Results.NotFound();
        if (!request.Activo && await db.Sucursales.AnyAsync(x => x.DepositoPropioId == id && x.Activo, ct))
            return Results.Conflict(new { message = "El depósito tiene sucursales activas asociadas. Desactivá primero esas sucursales." });
        deposito.Activo = request.Activo;
        await db.SaveChangesAsync(ct);
        return Results.Ok(DepositoDto(deposito));
    }

    private static async Task<IResult> DeleteDeposito(Guid id, AppDbContext db, CancellationToken ct)
    {
        var deposito = await db.Depositos.FindAsync([id], ct);
        if (deposito is null) return Results.NotFound();
        if (await db.MovimientosStock.AnyAsync(x => x.DepositoId == id, ct))
            return Results.Conflict(new { message = "El depósito tiene movimientos de stock asociados. Podés desactivarlo en lugar de eliminarlo." });
        if (await db.Sucursales.AnyAsync(x => x.DepositoPropioId == id, ct))
            return Results.Conflict(new { message = "El depósito tiene sucursales asociadas. Podés desactivarlo en lugar de eliminarlo." });
        db.Depositos.Remove(deposito);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<object>> ListSucursales(AppDbContext db, CancellationToken ct) =>
        (await db.Sucursales.AsNoTracking().Include(x => x.DepositoPropio)
            .OrderBy(x => x.Nombre).ToListAsync(ct)).Select(SucursalDto).ToList();

    private static async Task<IResult> CreateSucursal(SucursalRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateName(request.Nombre);
        if (errors is not null) return Results.ValidationProblem(errors);
        var name = request.Nombre.Trim();
        if (await db.Sucursales.AnyAsync(x => x.Nombre == name, ct))
            return Results.Conflict(new { message = "Ya existe una sucursal con ese nombre." });
        var deposito = await db.Depositos.SingleOrDefaultAsync(x => x.Id == request.DepositoPropioId && x.Activo, ct);
        if (deposito is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoPropioId"] = ["Seleccioná un depósito activo."] });

        var sucursal = new Sucursal
        {
            Nombre = name,
            DepositoPropioId = deposito.Id,
            DepositoPropio = deposito,
            PuntoVentaAfip = request.PuntoVentaAfip,
            Activo = request.Activo
        };
        db.Sucursales.Add(sucursal);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/sucursales/{sucursal.Id}", SucursalDto(sucursal));
    }

    private static async Task<IResult> UpdateSucursal(Guid id, SucursalRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = ValidateName(request.Nombre);
        if (errors is not null) return Results.ValidationProblem(errors);
        var sucursal = await db.Sucursales.Include(x => x.DepositoPropio).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sucursal is null) return Results.NotFound();
        var name = request.Nombre.Trim();
        if (await db.Sucursales.AnyAsync(x => x.Id != id && x.Nombre == name, ct))
            return Results.Conflict(new { message = "Ya existe una sucursal con ese nombre." });
        var deposito = await db.Depositos.SingleOrDefaultAsync(x => x.Id == request.DepositoPropioId && x.Activo, ct);
        if (deposito is null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoPropioId"] = ["Seleccioná un depósito activo."] });

        sucursal.Nombre = name;
        sucursal.DepositoPropioId = deposito.Id;
        sucursal.DepositoPropio = deposito;
        sucursal.PuntoVentaAfip = request.PuntoVentaAfip;
        sucursal.Activo = request.Activo;
        await db.SaveChangesAsync(ct);
        return Results.Ok(SucursalDto(sucursal));
    }

    private static async Task<IResult> SetSucursalEstado(Guid id, EstadoMaestroRequest request, AppDbContext db, CancellationToken ct)
    {
        var sucursal = await db.Sucursales.Include(x => x.DepositoPropio).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (sucursal is null) return Results.NotFound();
        if (request.Activo && !sucursal.DepositoPropio.Activo)
            return Results.Conflict(new { message = "No se puede activar una sucursal cuyo depósito está inactivo." });
        sucursal.Activo = request.Activo;
        await db.SaveChangesAsync(ct);
        return Results.Ok(SucursalDto(sucursal));
    }

    private static async Task<IResult> DeleteSucursal(Guid id, AppDbContext db, CancellationToken ct)
    {
        var sucursal = await db.Sucursales.FindAsync([id], ct);
        if (sucursal is null) return Results.NotFound();
        if (await db.Usuarios.AnyAsync(x => x.SucursalId == id, ct))
            return Results.Conflict(new { message = "La sucursal tiene usuarios asociados. Podés desactivarla en lugar de eliminarla." });
        db.Sucursales.Remove(sucursal);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
