using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace Api.Features.Caja;

public record MovimientoRequest(TipoMovimiento Tipo, decimal Monto, string? Concepto, Guid? SucursalId = null);
public record ActualizarObservacionRequest(string? Observacion);

public static class CajaEndpoints
{
    internal static string? ValidateWithdrawal(TipoMovimiento tipo, decimal monto, decimal saldo) =>
        tipo == TipoMovimiento.Retiro && monto > saldo
            ? $"El retiro supera el saldo disponible de {saldo:C}."
            : null;
    public static void MapCajaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/caja").WithTags("Caja").RequireAuthorization("caja");
        group.MapGet("/", async (Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => new
        {
            saldo = await VisibleQuery(db.MovimientosCaja, user, sucursalId).SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto, ct),
            movimientos = await VisibleQuery(db.MovimientosCaja.AsNoTracking(), user, sucursalId).OrderByDescending(x => x.Fecha).Take(100).ToListAsync(ct),
            sucursales = user.IsInRole("Administrador")
                ? await db.Sucursales.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Nombre)
                    .Select(x => new { x.Id, x.Nombre }).ToListAsync(ct)
                : []
        });
        group.MapPost("/movimientos", RegistrarMovimiento);
        group.MapPut("/movimientos/{id:guid}/observacion", ActualizarObservacion);
    }

    internal static IQueryable<MovimientoCaja> VisibleQuery(IQueryable<MovimientoCaja> query, ClaimsPrincipal user,
        Guid? sucursalId = null) => query.WhereVisibleParaUsuario(user, sucursalId);

    private static async Task<IResult> RegistrarMovimiento(MovimientoRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var concepto = request.Concepto?.Trim();
        if (request.Monto <= 0) errors["monto"] = ["El monto debe ser mayor que cero."];
        if (string.IsNullOrWhiteSpace(concepto)) errors["concepto"] = ["La observación es obligatoria."];
        if (concepto?.Length > 500) errors["concepto"] = ["La observación no puede superar 500 caracteres."];
        var sucursalId = currentUser.IsInRole("Administrador") ? request.SucursalId : currentUser.SucursalId();
        if (!sucursalId.HasValue) errors["sucursalId"] = ["Seleccioná la sucursal del movimiento."];
        else if (!await db.Sucursales.AnyAsync(x => x.Id == sucursalId.Value && x.Activo, ct))
            errors["sucursalId"] = ["La sucursal seleccionada no existe o está inactiva."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (request.Tipo == TipoMovimiento.Retiro)
        {
            var saldo = await db.MovimientosCaja.Where(x => x.SucursalId == sucursalId!.Value)
                .SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto, ct);
            var withdrawalError = ValidateWithdrawal(request.Tipo, request.Monto, saldo);
            if (withdrawalError is not null)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["monto"] = [withdrawalError] });
        }

        var movement = new MovimientoCaja
        {
            Tipo = request.Tipo, Monto = request.Monto, Concepto = concepto!,
            Usuario = currentUser.Identity?.Name ?? currentUser.FindFirstValue("usuario") ?? "sistema",
            Fecha = DateTimeOffset.UtcNow, SucursalId = sucursalId!.Value
        };
        db.MovimientosCaja.Add(movement);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/caja/movimientos/{movement.Id}", movement);
    }

    private static async Task<IResult> ActualizarObservacion(Guid id, ActualizarObservacionRequest request,
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var observacion = request.Observacion?.Trim();
        var errors = ValidateObservation(observacion);
        if (errors is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["observacion"] = [errors] });

        var movement = await VisibleQuery(db.MovimientosCaja, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (movement is null) return Results.NotFound();
        movement.Concepto = observacion!;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { movement.Id, observacion = movement.Concepto });
    }

    internal static string? ValidateObservation(string? observacion)
    {
        if (string.IsNullOrWhiteSpace(observacion)) return "La observación es obligatoria.";
        return observacion.Length > 500 ? "La observación no puede superar 500 caracteres." : null;
    }
}
