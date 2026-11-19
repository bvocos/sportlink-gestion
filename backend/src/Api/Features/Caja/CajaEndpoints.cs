using Api.Shared.Common;
using Api.Shared.Database;
using Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;

namespace Api.Features.Caja;

// SucursalId se conserva por compatibilidad con clientes anteriores, pero se ignora:
// todo movimiento se registra en la sucursal activa del usuario.
// Monto se interpreta en la moneda elegida. El equivalente USD se calcula en el servidor.
public record MovimientoRequest(TipoMovimiento Tipo, decimal Monto, string? Concepto, Guid? SucursalId = null,
    MonedaCaja Moneda = MonedaCaja.USD, decimal? Cotizacion = null);
public record ActualizarObservacionRequest(string? Observacion);

public static class CajaEndpoints
{
    internal static string? ValidateWithdrawal(TipoMovimiento tipo, decimal monto, decimal saldo, MonedaCaja moneda = MonedaCaja.USD) =>
        tipo == TipoMovimiento.Retiro && monto > saldo
            ? moneda == MonedaCaja.ARS
                ? $"El retiro supera los pesos disponibles en caja ({saldo:N2})."
                : $"El retiro supera los dólares disponibles en caja ({saldo:N2})."
            : null;
    public static void MapCajaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/caja").WithTags("Caja");
        group.MapGet("/", async (ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        {
            var activeSucursalId = user.SucursalId();
            var query = VisibleQuery(db.MovimientosCaja, user);
            return new
            {
                saldo = await query.SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto, ct),
                saldoUsd = await query.Where(x => x.MonedaCobro == MonedaCaja.USD)
                    .SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.ImporteMonedaCobro : -x.ImporteMonedaCobro, ct),
                saldoArs = await query.Where(x => x.MonedaCobro == MonedaCaja.ARS)
                    .SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.ImporteMonedaCobro : -x.ImporteMonedaCobro, ct),
                movimientos = await VisibleQuery(db.MovimientosCaja.AsNoTracking(), user).OrderByDescending(x => x.Fecha).Take(100).ToListAsync(ct),
                sucursales = user.IsInRole("Administrador") && activeSucursalId.HasValue
                    ? await db.Sucursales.AsNoTracking()
                        .Where(x => x.Id == activeSucursalId.Value && x.Activo)
                        .Select(x => new { x.Id, x.Nombre }).ToListAsync(ct)
                    : []
            };
        }).RequirePermiso("caja", "ver");
        group.MapPost("/movimientos", RegistrarMovimiento).RequirePermiso("caja", "crear");
        group.MapPut("/movimientos/{id:guid}/observacion", ActualizarObservacion).RequirePermiso("caja", "editar");
    }

    internal static IQueryable<MovimientoCaja> VisibleQuery(IQueryable<MovimientoCaja> query, ClaimsPrincipal user,
        Guid? sucursalId = null) => query.WhereVisibleParaUsuario(user, sucursalId);

    private static async Task<IResult> RegistrarMovimiento(MovimientoRequest request, ClaimsPrincipal currentUser, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var concepto = request.Concepto?.Trim();
        if (request.Monto <= 0) errors["monto"] = ["El monto debe ser mayor que cero."];
        var currencyError = MonedaCajaService.Validate(request.Moneda, request.Cotizacion);
        if (currencyError is not null) errors["cotizacion"] = [currencyError];
        if (string.IsNullOrWhiteSpace(concepto)) errors["concepto"] = ["La observación es obligatoria."];
        if (concepto?.Length > 500) errors["concepto"] = ["La observación no puede superar 500 caracteres."];
        var sucursalId = currentUser.SucursalId();
        if (!sucursalId.HasValue) errors["sucursalId"] = ["Elegí una sucursal activa antes de registrar un movimiento."];
        else if (!await db.Sucursales.AnyAsync(x => x.Id == sucursalId.Value && x.Activo, ct))
            errors["sucursalId"] = ["La sucursal seleccionada no existe o está inactiva."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var cotizacion = request.Moneda == MonedaCaja.ARS ? request.Cotizacion : null;
        var montoUsd = MonedaCajaService.MontoUsd(request.Monto, request.Moneda, cotizacion);

        var executionStrategy = db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            // Serializa movimientos de una misma sucursal para que retiros concurrentes no consuman el mismo saldo.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"Sucursales\" WHERE \"Id\" = {sucursalId!.Value} FOR UPDATE", ct);
            if (request.Tipo == TipoMovimiento.Retiro)
            {
                var saldo = await db.MovimientosCaja.Where(x => x.SucursalId == sucursalId!.Value && x.MonedaCobro == request.Moneda)
                    .SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.ImporteMonedaCobro : -x.ImporteMonedaCobro, ct);
                var withdrawalError = ValidateWithdrawal(request.Tipo, request.Monto, saldo, request.Moneda);
                if (withdrawalError is not null)
                    return (IResult)Results.Conflict(new { message = withdrawalError });
            }

            var movement = new MovimientoCaja
            {
                Tipo = request.Tipo, Monto = montoUsd, Concepto = concepto!,
                MonedaCobro = request.Moneda, ImporteMonedaCobro = Math.Round(request.Monto, 2, MidpointRounding.AwayFromZero),
                CotizacionAplicada = cotizacion,
                Usuario = currentUser.Identity?.Name ?? currentUser.FindFirstValue("usuario") ?? "sistema",
                Fecha = DateTimeOffset.UtcNow, SucursalId = sucursalId!.Value
            };
            db.MovimientosCaja.Add(movement);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (IResult)Results.Created($"/api/caja/movimientos/{movement.Id}", movement);
        });
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
