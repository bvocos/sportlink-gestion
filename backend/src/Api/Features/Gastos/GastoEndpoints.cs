using Api.Shared.Database;
using Api.Shared.Common;
using Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Features.Gastos;

public record GastoRequest(DateOnly Fecha, string Categoria, string Descripcion, decimal Importe, string? Observaciones);

public static class GastoEndpoints
{
    public static void MapGastoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gastos").WithTags("Gastos");
        group.MapGet("/", List).RequirePermiso("gastos", "ver");
        group.MapPost("/", Create).RequirePermiso("gastos", "crear");
        group.MapPut("/{id:guid}", Update).RequirePermiso("gastos", "editar");
        group.MapDelete("/{id:guid}", Delete).RequirePermiso("gastos", "eliminar");
    }

    internal static IQueryable<Gasto> VisibleQuery(IQueryable<Gasto> query, ClaimsPrincipal user) =>
        query.WhereVisibleParaUsuario(user);

    internal static IQueryable<Gasto> VisibleByIdQuery(IQueryable<Gasto> query, ClaimsPrincipal user, Guid id) =>
        VisibleQuery(query, user).Where(x => x.Id == id);

    private static async Task<IResult> List(DateOnly? desde, DateOnly? hasta, string? buscar, int page, int pageSize, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);
        if (desde.HasValue && hasta.HasValue && desde > hasta)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["desde"] = ["La fecha desde no puede ser posterior a la fecha hasta."] });
        var query = VisibleQuery(db.Gastos.AsNoTracking(), user);
        if (desde.HasValue) query = query.Where(x => x.Fecha >= desde.Value);
        if (hasta.HasValue) query = query.Where(x => x.Fecha <= hasta.Value);
        if (!string.IsNullOrWhiteSpace(buscar)) query = query.Where(x => x.Categoria.Contains(buscar) || x.Descripcion.Contains(buscar) || (x.Observaciones != null && x.Observaciones.Contains(buscar)));
        var total = await query.CountAsync(ct);
        var totalImporte = await query.SumAsync(x => (decimal?)x.Importe, ct) ?? 0;
        var items = await query.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new { items, total, totalImporte, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    private static async Task<IResult> Create(GastoRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var sucursalId = currentUser.SucursalId();
        if (!sucursalId.HasValue)
            return Results.BadRequest(new { message = "Seleccioná una sucursal activa para registrar el gasto." });
        var gasto = new Gasto { SucursalId = sucursalId.Value }; Apply(gasto, request); db.Gastos.Add(gasto); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/gastos/{gasto.Id}", gasto);
    }

    private static async Task<IResult> Update(Guid id, GastoRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var gasto = await VisibleByIdQuery(db.Gastos, currentUser, id).SingleOrDefaultAsync(ct); if (gasto is null) return Results.NotFound();
        Apply(gasto, request); await db.SaveChangesAsync(ct); return Results.Ok(gasto);
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var gasto = await VisibleByIdQuery(db.Gastos, currentUser, id).SingleOrDefaultAsync(ct); if (gasto is null) return Results.NotFound();
        db.Gastos.Remove(gasto); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    internal static Dictionary<string, string[]> Validate(GastoRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Importe <= 0) errors["importe"] = ["El importe debe ser mayor que cero."];
        if (string.IsNullOrWhiteSpace(request.Categoria)) errors["categoria"] = ["La categoría es obligatoria."];
        else if (request.Categoria.Trim().Length > 100) errors["categoria"] = ["La categoría no puede superar 100 caracteres."];
        if (string.IsNullOrWhiteSpace(request.Descripcion)) errors["descripcion"] = ["La descripción es obligatoria."];
        else if (request.Descripcion.Trim().Length > 300) errors["descripcion"] = ["La descripción no puede superar 300 caracteres."];
        if (request.Observaciones?.Length > 1000) errors["observaciones"] = ["Las observaciones no pueden superar 1000 caracteres."];
        return errors;
    }

    private static void Apply(Gasto gasto, GastoRequest request)
    {
        gasto.Fecha = request.Fecha; gasto.Categoria = request.Categoria.Trim(); gasto.Descripcion = request.Descripcion.Trim();
        gasto.Importe = request.Importe; gasto.Observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim();
    }
}
