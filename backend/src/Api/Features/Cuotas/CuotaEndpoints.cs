using System.Data;
using System.Security.Claims;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Cuotas;

public record PagoRequest(decimal Importe, string MedioPago, DateOnly FechaPago);
public record ActualizarVencimientoRequest(DateOnly FechaVencimiento);

public static class CuotaEndpoints
{
    internal static string? ValidatePayment(decimal importe, decimal saldoPendiente)
    {
        if (importe <= 0) return "El importe debe ser mayor a cero.";
        if (importe > saldoPendiente) return $"El pago no puede superar el saldo pendiente de {saldoPendiente:C}.";
        return null;
    }
    internal static string? ValidateCancellation(decimal importePagado, decimal importePactado)
    {
        if (importePagado <= 0) return "La cuota no tiene pagos para anular.";
        if (importePagado < importePactado) return "Sólo se puede anular una cuota completamente abonada.";
        return null;
    }
    internal static string? ValidateDueDate(DateOnly fechaVencimiento) =>
        fechaVencimiento == default ? "Ingresá una fecha de vencimiento válida." : null;
    internal static IQueryable<Cuota> VisibleQuery(IQueryable<Cuota> query, ClaimsPrincipal user,
        Guid? sucursalId = null) => query.WhereVisibleParaUsuario(user, sucursalId);

    public static void MapCuotaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/cuotas").WithTags("Cuotas").RequireAuthorization("cuotas");
        group.MapGet("/pendientes", (Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => List(user, db, false, sucursalId, ct));
        group.MapGet("/pendientes/resumen", ResumenPendiente);
        group.MapGet("/abonadas", (Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => List(user, db, true, sucursalId, ct));
        group.MapPut("/{id:guid}/vencimiento", ActualizarVencimiento);
        group.MapPost("/{id:guid}/pagos", RegistrarPago);
        group.MapPost("/{id:guid}/anular-pago", AnularPago);
    }

    private static async Task<IResult> List(ClaimsPrincipal user, AppDbContext db, bool abonadas, Guid? sucursalId, CancellationToken ct)
    {
        var query = VisibleQuery(db.Cuotas.AsNoTracking(), user, sucursalId)
            .Include(x => x.Venta).ThenInclude(x => x.Cliente)
            .Include(x => x.Venta).ThenInclude(x => x.TipoCesped)
            .Where(x => abonadas ? x.ImportePagado >= x.ImportePactado : x.ImportePagado < x.ImportePactado);
        var rows = await query.OrderByDescending(x => abonadas ? x.FechaPago : null).ThenBy(x => x.FechaVencimiento)
            .Select(x => new { x.Id, x.VentaId, cliente = x.Venta.Cliente.Nombre + " " + x.Venta.Cliente.Apellido,
                fechaVenta = x.Venta.FechaVenta, tipoCesped = x.Venta.TipoCesped.Nombre, totalVenta = x.Venta.PrecioTotal,
                x.Numero, x.FechaVencimiento, x.FechaPago, fechaImpacto = x.UpdatedAt ?? x.CreatedAt, x.MedioPago,
                x.ImportePactado, x.ImportePagado,
                estado = abonadas ? EstadoCuota.Pagada : x.FechaVencimiento < DateOnly.FromDateTime(DateTime.Today) && x.Estado != EstadoCuota.Pagada ? EstadoCuota.Vencida : x.Estado })
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> ResumenPendiente(string? buscar, Guid? sucursalId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var query = VisibleQuery(db.Cuotas.AsNoTracking(), user, sucursalId).Where(x => x.ImportePagado < x.ImportePactado);
        var term = buscar?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(x => EF.Functions.Like(x.Venta.Cliente.Nombre + " " + x.Venta.Cliente.Apellido, $"%{term}%"));
        var resumen = await query.GroupBy(_ => 1)
            .Select(g => new { cantidad = g.Count(), totalPendiente = g.Sum(x => x.ImportePactado - x.ImportePagado) })
            .SingleOrDefaultAsync(ct);
        return Results.Ok(resumen ?? new { cantidad = 0, totalPendiente = 0m });
    }

    private static async Task<IResult> RegistrarPago(Guid id, PagoRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var medioPago = request.MedioPago?.Trim();
        if (string.IsNullOrWhiteSpace(medioPago) || medioPago.Length > 100)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["medioPago"] = ["Seleccioná un medio de pago válido."] });
        var cuota = await VisibleQuery(db.Cuotas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cuota is null) return Results.NotFound();
        var paymentError = ValidatePayment(request.Importe, cuota.ImportePactado - cuota.ImportePagado);
        if (paymentError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["importe"] = [paymentError] });
        cuota.ImportePagado += request.Importe; cuota.FechaPago = request.FechaPago; cuota.MedioPago = medioPago;
        cuota.Estado = cuota.ImportePagado >= cuota.ImportePactado ? EstadoCuota.Pagada : EstadoCuota.PagadaParcial;
        db.MovimientosCaja.Add(new MovimientoCaja { Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow,
            Monto = request.Importe, Concepto = $"Pago cuota {cuota.Numero}", Usuario = UserName(user),
            CuotaId = cuota.Id, VentaId = cuota.VentaId, SucursalId = cuota.SucursalId });
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { cuota.Id, cuota.ImportePagado, cuota.Estado });
    }

    private static async Task<IResult> AnularPago(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var cuota = await VisibleQuery(db.Cuotas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cuota is null) return Results.NotFound();
        var cancellationError = ValidateCancellation(cuota.ImportePagado, cuota.ImportePactado);
        if (cancellationError is not null) return Results.Conflict(new { message = cancellationError });
        var importeAnulado = cuota.ImportePagado;
        cuota.ImportePagado = 0; cuota.FechaPago = null; cuota.MedioPago = null; cuota.Estado = EstadoCuota.Pendiente;
        db.MovimientosCaja.Add(new MovimientoCaja { Tipo = TipoMovimiento.Retiro, Fecha = DateTimeOffset.UtcNow,
            Monto = importeAnulado, Concepto = $"Anulación cobro cuota {cuota.Numero}", Usuario = UserName(user),
            CuotaId = cuota.Id, VentaId = cuota.VentaId, SucursalId = cuota.SucursalId });
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        return Results.Ok(new { cuota.Id, importeAnulado, cuota.Estado });
    }

    private static async Task<IResult> ActualizarVencimiento(Guid id, ActualizarVencimientoRequest request,
        ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var dueDateError = ValidateDueDate(request.FechaVencimiento);
        if (dueDateError is not null)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["fechaVencimiento"] = [dueDateError] });
        var cuota = await VisibleQuery(db.Cuotas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cuota is null) return Results.NotFound();
        cuota.FechaVencimiento = request.FechaVencimiento;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { cuota.Id, cuota.FechaVencimiento });
    }

    private static string UserName(ClaimsPrincipal user) =>
        user.Identity?.Name ?? user.FindFirstValue("usuario") ?? "sistema";
}
