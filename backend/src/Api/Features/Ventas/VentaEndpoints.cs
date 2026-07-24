using Api.Shared.Common;
using Api.Shared.Database;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Ventas;

public record RegistrarVentaCommand(Guid ClienteId, DateOnly FechaVenta, Guid TipoCespedId, decimal CantidadM2,
    decimal PrecioUnitario, decimal PrecioTotal, decimal MontoEntrega, FormaPago FormaPago, int? CantidadCuotas, EstadoVenta Estado,
    DateOnly? FechaEntregaEstimada, string? Observaciones, decimal CostoCompraUnitario,
    decimal CostoEnvio, decimal OtrosCostos, Guid AlicuotaIvaId) : IRequest<VentaDto>;

public record VentaDto(Guid Id, Guid ClienteId, string Cliente, Guid TipoCespedId, string TipoCesped,
    Guid AlicuotaIvaId, DateOnly FechaVenta, decimal CantidadM2, decimal PrecioUnitario, decimal PrecioTotal, decimal MontoEntrega,
    decimal CostoCompraUnitario, decimal CostoEnvio, decimal OtrosCostos, FormaPago FormaPago,
    int? CantidadCuotas, EstadoVenta Estado, decimal GananciaNeta, decimal Margen,
    DateOnly? FechaEntregaEstimada, string? Observaciones);

public sealed class RegistrarVentaValidator : AbstractValidator<RegistrarVentaCommand>
{
    public RegistrarVentaValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty();
        RuleFor(x => x.TipoCespedId).NotEmpty();
        RuleFor(x => x.AlicuotaIvaId).NotEmpty();
        RuleFor(x => x.CantidadM2).GreaterThan(0);
        RuleFor(x => x.PrecioUnitario).GreaterThan(0);
        RuleFor(x => x.PrecioTotal).GreaterThan(0)
            .WithMessage("El importe final de la venta debe ser mayor que cero.");
        RuleFor(x => x.MontoEntrega).GreaterThan(0)
            .LessThanOrEqualTo(x => x.PrecioTotal)
            .WithMessage("La entrega debe ser mayor que cero y no puede superar el total de la venta.");
        RuleFor(x => x.MontoEntrega).LessThan(x => x.PrecioTotal)
            .When(x => x.FormaPago == FormaPago.Cuotas)
            .WithMessage("En una venta en cuotas la entrega debe ser menor al total para que exista saldo a financiar.");
        RuleFor(x => x.CostoCompraUnitario).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CostoEnvio).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtrosCostos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CantidadCuotas).NotNull().InclusiveBetween(1, 60).When(x => x.FormaPago == FormaPago.Cuotas);
        RuleFor(x => x.FechaEntregaEstimada).NotNull().When(x => x.Estado == EstadoVenta.Futura);
    }
}

public sealed class RegistrarVentaHandler(AppDbContext db) : IRequestHandler<RegistrarVentaCommand, VentaDto>
{
    public async Task<VentaDto> Handle(RegistrarVentaCommand request, CancellationToken ct)
    {
        var (cliente, tipo, alicuota) = await VentaService.GetReferences(db, request, ct);
        var venta = new Venta();
        VentaService.Apply(venta, request, alicuota.Porcentaje);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.Ventas.Add(venta);
        VentaService.CreateInstallments(venta, request);
        await db.SaveChangesAsync(ct); // La venta debe existir antes de referenciarla desde Caja.

        if (request.MontoEntrega > 0)
        {
            db.MovimientosCaja.Add(VentaService.CreateCashMovement(venta));
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return VentaService.ToDto(venta, cliente, tipo);
    }
}

internal static class VentaService
{
    public static async Task<(Cliente Cliente, TipoCesped Tipo, AlicuotaIva Alicuota)> GetReferences(
        AppDbContext db, RegistrarVentaCommand request, CancellationToken ct)
    {
        var cliente = await db.Clientes.FindAsync([request.ClienteId], ct) ?? throw new KeyNotFoundException("Cliente inexistente");
        var tipo = await db.TiposCesped.FindAsync([request.TipoCespedId], ct) ?? throw new KeyNotFoundException("Tipo de césped inexistente");
        var alicuota = await db.AlicuotasIva.FindAsync([request.AlicuotaIvaId], ct) ?? throw new KeyNotFoundException("Alícuota inexistente");
        return (cliente, tipo, alicuota);
    }

    public static void Apply(Venta venta, RegistrarVentaCommand r, decimal porcentajeIva)
    {
        var total = r.PrecioTotal;
        var costoCompra = r.CostoCompraUnitario * r.CantidadM2;
        var iva = total * porcentajeIva / 100;
        var gananciaBruta = total - costoCompra - r.CostoEnvio - r.OtrosCostos;
        venta.ClienteId = r.ClienteId; venta.TipoCespedId = r.TipoCespedId; venta.AlicuotaIvaId = r.AlicuotaIvaId;
        venta.FechaVenta = r.FechaVenta; venta.CantidadM2 = r.CantidadM2; venta.PrecioUnitario = r.PrecioUnitario;
        venta.PrecioTotal = total; venta.MontoEntrega = r.MontoEntrega; venta.CostoCompraUnitario = r.CostoCompraUnitario; venta.CostoCompraTotal = costoCompra;
        venta.CostoEnvio = r.CostoEnvio; venta.OtrosCostos = r.OtrosCostos; venta.Iva = iva;
        venta.GananciaBruta = gananciaBruta; venta.GananciaNeta = gananciaBruta - iva;
        venta.Margen = total == 0 ? 0 : venta.GananciaNeta / total; venta.FormaPago = r.FormaPago;
        venta.CantidadCuotas = r.FormaPago == FormaPago.Cuotas ? r.CantidadCuotas : null;
        venta.Estado = r.Estado; venta.FechaEntregaEstimada = r.Estado == EstadoVenta.Futura ? r.FechaEntregaEstimada : null;
        venta.Observaciones = r.Observaciones;
    }

    public static void CreateInstallments(Venta venta, RegistrarVentaCommand r)
    {
        if (r.FormaPago != FormaPago.Cuotas) return;
        var count = r.CantidadCuotas!.Value;
        var saldoFinanciado = venta.PrecioTotal - venta.MontoEntrega;
        var amount = Math.Round(saldoFinanciado / count, 2);
        for (var i = 1; i <= count; i++)
            venta.Cuotas.Add(new Cuota { VentaId = venta.Id, ClienteId = r.ClienteId, Numero = i,
                FechaVencimiento = r.FechaVenta.AddMonths(i),
                ImportePactado = i == count ? saldoFinanciado - amount * (count - 1) : amount,
                Estado = EstadoCuota.Pendiente });
    }

    public static MovimientoCaja CreateCashMovement(Venta venta) => new()
    {
        Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow, Monto = venta.MontoEntrega,
        Concepto = $"Entrega inicial venta {venta.Id}", VentaId = venta.Id
    };

    public static VentaDto ToDto(Venta v, Cliente c, TipoCesped t) => new(v.Id, v.ClienteId,
        $"{c.Nombre} {c.Apellido}", v.TipoCespedId, t.Nombre, v.AlicuotaIvaId, v.FechaVenta,
        v.CantidadM2, v.PrecioUnitario, v.PrecioTotal, v.MontoEntrega, v.CostoCompraUnitario, v.CostoEnvio,
        v.OtrosCostos, v.FormaPago, v.CantidadCuotas, v.Estado, v.GananciaNeta, v.Margen,
        v.FechaEntregaEstimada, v.Observaciones);
}

public static class VentaEndpoints
{
    public static void MapVentaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ventas").WithTags("Ventas").RequireAuthorization("ventas");
        group.MapPost("/", async (RegistrarVentaCommand command, ISender sender, CancellationToken ct) =>
            Results.Created("/api/ventas", await sender.Send(command, ct)));
        group.MapGet("/", List);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/entregar", (Guid id, AppDbContext db, CancellationToken ct) => ChangeStatus(id, EstadoVenta.Entregada, db, ct));
        group.MapPost("/{id:guid}/confirmar", ResetDelivery);
        group.MapPost("/{id:guid}/cancelar", (Guid id, AppDbContext db, CancellationToken ct) => ChangeStatus(id, EstadoVenta.Cancelada, db, ct));
        app.MapGet("/api/ventas/proximas-entregas", UpcomingDeliveries).WithTags("Ventas").RequireAuthorization("entregas");
    }

    private static async Task<PaginatedResponse<VentaDto>> List(AppDbContext db, int page = 1, int pageSize = 20,
        string? estado = null, CancellationToken ct = default)
    {
        var query = db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped).AsQueryable();
        if (Enum.TryParse<EstadoVenta>(estado, true, out var parsed)) query = query.Where(x => x.Estado == parsed);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.FechaVenta).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(v => VentaService.ToDto(v, v.Cliente, v.TipoCesped)).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    private static async Task<IResult> Update(Guid id, RegistrarVentaCommand request, AppDbContext db,
        IValidator<RegistrarVentaCommand> validator, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Api.Features.Ventas.Update");
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Results.ValidationProblem(validation.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));

        var venta = await db.Ventas.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (venta is null) return Results.NotFound();
        if (await db.Cuotas.AnyAsync(x => x.VentaId == id && x.ImportePagado > 0, ct))
            return Results.Conflict(new { message = "No se puede modificar una venta que ya tiene cuotas cobradas." });

        var (cliente, tipo, alicuota) = await VentaService.GetReferences(db, request, ct);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            // ExecuteDelete evita conflictos entre cuotas eliminadas y las nuevas con el mismo número.
            await db.MovimientosCaja.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);
            await db.Cuotas.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);

            db.ChangeTracker.Clear();
            var updated = new Venta { Id = id };
            VentaService.Apply(updated, request, alicuota.Porcentaje);

            var affected = await db.Ventas.Where(x => x.Id == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ClienteId, updated.ClienteId)
                .SetProperty(x => x.TipoCespedId, updated.TipoCespedId)
                .SetProperty(x => x.AlicuotaIvaId, updated.AlicuotaIvaId)
                .SetProperty(x => x.FechaVenta, updated.FechaVenta)
                .SetProperty(x => x.FechaEntregaEstimada, updated.FechaEntregaEstimada)
                .SetProperty(x => x.CantidadM2, updated.CantidadM2)
                .SetProperty(x => x.PrecioUnitario, updated.PrecioUnitario)
                .SetProperty(x => x.PrecioTotal, updated.PrecioTotal)
                .SetProperty(x => x.MontoEntrega, updated.MontoEntrega)
                .SetProperty(x => x.CostoCompraUnitario, updated.CostoCompraUnitario)
                .SetProperty(x => x.CostoCompraTotal, updated.CostoCompraTotal)
                .SetProperty(x => x.CostoEnvio, updated.CostoEnvio)
                .SetProperty(x => x.OtrosCostos, updated.OtrosCostos)
                .SetProperty(x => x.Iva, updated.Iva)
                .SetProperty(x => x.GananciaBruta, updated.GananciaBruta)
                .SetProperty(x => x.GananciaNeta, updated.GananciaNeta)
                .SetProperty(x => x.Margen, updated.Margen)
                .SetProperty(x => x.FormaPago, updated.FormaPago)
                .SetProperty(x => x.CantidadCuotas, updated.CantidadCuotas)
                .SetProperty(x => x.Estado, updated.Estado)
                .SetProperty(x => x.Observaciones, updated.Observaciones)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);

            if (affected != 1)
            {
                await transaction.RollbackAsync(ct);
                return Results.Conflict(new { message = "La venta ya no existe o fue modificada durante la operación." });
            }

            VentaService.CreateInstallments(updated, request);
            if (updated.Cuotas.Count > 0)
                db.Cuotas.AddRange(updated.Cuotas);

            if (request.MontoEntrega > 0)
                db.MovimientosCaja.Add(VentaService.CreateCashMovement(updated));

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return Results.Ok(VentaService.ToDto(updated, cliente, tipo));
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Error de base de datos al modificar la venta {VentaId}", id);
            return Results.Problem(title: "No se pudo modificar la venta",
                detail: "No fue posible guardar los cambios. Intentá nuevamente.",
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error inesperado al modificar la venta {VentaId}", id);
            return Results.Problem(title: "No se pudo modificar la venta",
                detail: "Ocurrió un error inesperado al guardar la venta.",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct)
    {
        if (!await db.Ventas.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.MovimientosCaja.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);
        await db.Cuotas.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);
        await db.Ventas.Where(x => x.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeStatus(Guid id, EstadoVenta status, AppDbContext db, CancellationToken ct)
    {
        var venta = await db.Ventas.FindAsync([id], ct);
        if (venta is null) return Results.NotFound();
        if (venta.Estado == EstadoVenta.Entregada) return Results.Conflict(new { message = "Una venta entregada no puede modificarse." });
        venta.Estado = status; await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> ResetDelivery(Guid id, AppDbContext db, CancellationToken ct)
    {
        var venta = await db.Ventas.FindAsync([id], ct);
        if (venta is null) return Results.NotFound();
        if (venta.Estado != EstadoVenta.Entregada)
            return Results.Conflict(new { message = "Sólo se puede revertir una venta entregada." });
        venta.Estado = EstadoVenta.Confirmada;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpcomingDeliveries(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped)
            .Where(x => x.Estado == EstadoVenta.Futura && x.FechaEntregaEstimada != null)
            .OrderBy(x => x.FechaEntregaEstimada).ThenBy(x => x.Cliente.Apellido)
            .Select(x => new { x.Id, Cliente = x.Cliente.Nombre + " " + x.Cliente.Apellido,
                TipoCesped = x.TipoCesped.Nombre, x.CantidadM2, x.FechaEntregaEstimada,
                x.Observaciones })
            .ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.Today);
        return Results.Ok(rows.Select(x => new { x.Id, x.Cliente, x.TipoCesped, x.CantidadM2,
            x.FechaEntregaEstimada, x.Observaciones,
            DiasRestantes = x.FechaEntregaEstimada!.Value.DayNumber - today.DayNumber }));
    }
}
