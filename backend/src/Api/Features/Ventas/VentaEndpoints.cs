using Api.Shared.Common;
using Api.Shared.Database;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Security.Claims;
using System.Text.Json;

namespace Api.Features.Ventas;

public record VentaLineaCommand(Guid TipoCespedId, string? Color, decimal CantidadM2,
    decimal PrecioCompraM2, decimal PrecioVentaM2, decimal Total, Guid? LoteStockId = null);

public record RegistrarVentaCommand(Guid ClienteId, DateOnly FechaVenta, Guid TipoCespedId, decimal CantidadM2,
    decimal PrecioUnitario, decimal PrecioTotal, decimal MontoEntrega, FormaPago FormaPago, int? CantidadCuotas, EstadoVenta Estado,
    DateOnly? FechaEntregaEstimada, string? Observaciones, decimal CostoCompraUnitario,
    decimal CostoEnvio, decimal OtrosCostos, Guid AlicuotaIvaId, string? Color = null,
    List<VentaLineaCommand>? Lineas = null, Guid? SucursalId = null, Guid DepositoId = default) : IRequest<VentaDto>;

public record VentaDto(Guid Id, Guid SucursalId, Guid DepositoId, Guid ClienteId, string Cliente, Guid TipoCespedId, string TipoCesped,
    Guid AlicuotaIvaId, DateOnly FechaVenta, decimal CantidadM2, decimal PrecioUnitario, decimal PrecioTotal, decimal MontoEntrega,
    decimal CostoCompraUnitario, decimal CostoEnvio, decimal OtrosCostos, FormaPago FormaPago,
    int? CantidadCuotas, EstadoVenta Estado, decimal GananciaNeta, decimal Margen,
    DateOnly? FechaEntregaEstimada, string? Observaciones, string? Color, List<VentaLineaCommand> Lineas);

public sealed class RegistrarVentaValidator : AbstractValidator<RegistrarVentaCommand>
{
    public RegistrarVentaValidator()
    {
        RuleFor(x => x.ClienteId).NotEmpty();
        RuleFor(x => x.TipoCespedId).NotEmpty();
        RuleFor(x => x.AlicuotaIvaId).NotEmpty();
        RuleFor(x => x.DepositoId).NotEmpty().WithMessage("Seleccioná el depósito del que sale el stock.");
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
        RuleFor(x => x.CostoCompraUnitario).GreaterThan(0)
            .WithMessage("Ingresá el costo de compra por m² correspondiente a la venta.");
        RuleFor(x => x.CostoEnvio).GreaterThanOrEqualTo(0);
        RuleFor(x => x.OtrosCostos).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CantidadCuotas).NotNull().InclusiveBetween(1, 60).When(x => x.FormaPago == FormaPago.Cuotas);
        RuleFor(x => x.FechaEntregaEstimada).NotNull().When(x => x.Estado == EstadoVenta.Futura);
        RuleForEach(x => x.Lineas).ChildRules(linea =>
        {
            linea.RuleFor(x => x.TipoCespedId).NotEmpty();
            linea.RuleFor(x => x.CantidadM2).GreaterThan(0);
            linea.RuleFor(x => x.PrecioCompraM2).GreaterThan(0);
            linea.RuleFor(x => x.PrecioVentaM2).GreaterThan(0);
            linea.RuleFor(x => x.Total).GreaterThan(0);
        });
    }
}

internal sealed class StockInsuficienteException(string producto, decimal faltante) : Exception
{
    public string Producto { get; } = producto;
    public decimal Faltante { get; } = faltante;
}

internal sealed class VentaScopeException(string message) : Exception(message);
internal sealed class LoteNoDisponibleException(string message) : Exception(message);

public sealed class RegistrarVentaHandler(AppDbContext db, IHttpContextAccessor httpContextAccessor) : IRequestHandler<RegistrarVentaCommand, VentaDto>
{
    public async Task<VentaDto> Handle(RegistrarVentaCommand request, CancellationToken ct)
    {
        var currentUser = httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
        var sucursalId = currentUser.IsInRole("Administrador") ? request.SucursalId : currentUser.SucursalId();
        if (!sucursalId.HasValue)
            throw new VentaScopeException("Seleccioná la sucursal a la que pertenece la venta.");
        if (!await db.Sucursales.AnyAsync(x => x.Id == sucursalId.Value && x.Activo, ct))
            throw new VentaScopeException("La sucursal seleccionada no existe o está inactiva.");
        if (!await db.Depositos.AnyAsync(x => x.Id == request.DepositoId && x.Activo, ct))
            throw new VentaScopeException("El depósito seleccionado no existe o está inactivo.");
        request = request with { SucursalId = sucursalId };
        request = await VentaService.NormalizeLines(db, request, ct);
        var (cliente, tipo, alicuota) = await VentaService.GetReferences(db, request, ct);
        request = VentaService.NormalizeColor(request, tipo);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var venta = new Venta();
        request = await VentaService.ResolveControlledLots(db, request, null, venta.Id, ct);
        VentaService.Apply(venta, request, alicuota.Porcentaje);
        var stockLines = VentaService.StockLines(request);
        await VentaService.ValidateStock(db, request.DepositoId, stockLines, ct);
        db.Ventas.Add(venta);
        VentaService.CreateInstallments(venta, request);
        db.MovimientosStock.AddRange(VentaService.CreateStockMovements(venta, stockLines,
            currentUser.Identity?.Name ?? currentUser.FindFirstValue("usuario") ?? "sistema"));
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
    public static Task<decimal> StockActual(AppDbContext db, Guid depositoId, Guid tipoCespedId, CancellationToken ct) =>
        db.MovimientosStock.Where(x => x.DepositoId == depositoId && x.TipoCespedId == tipoCespedId)
            .SumAsync(x => x.Tipo == TipoMovimientoStock.SalidaPorVenta ? -x.CantidadM2 : x.CantidadM2, ct);

    internal static decimal StockShortage(decimal stockActual, decimal cantidadSolicitada) =>
        Math.Max(cantidadSolicitada - stockActual, 0);

    internal static void ValidateLotSelection(LoteStock? lot, VentaLineaCommand line, Guid depositId, Guid? currentSaleId)
    {
        if (lot is null || (lot.Estado != EstadoLoteStock.Disponible && lot.VentaId != currentSaleId))
            throw new LoteNoDisponibleException("Ese lote ya fue vendido o no existe.");
        if (lot.DepositoId != depositId || lot.TipoCespedId != line.TipoCespedId ||
            !string.Equals(lot.Color ?? "", line.Color?.Trim() ?? "", StringComparison.OrdinalIgnoreCase))
            throw new LoteNoDisponibleException("El lote seleccionado no corresponde al depósito, producto o color de la línea.");
    }

    internal static void MarkLotSold(LoteStock lot, Guid saleId)
    {
        lot.Estado = EstadoLoteStock.Vendido;
        lot.VentaId = saleId;
    }

    internal static void ReleaseLot(LoteStock lot)
    {
        lot.Estado = EstadoLoteStock.Disponible;
        lot.VentaId = null;
    }

    public static async Task<RegistrarVentaCommand> ResolveControlledLots(AppDbContext db, RegistrarVentaCommand request,
        Guid? currentSaleId, Guid targetSaleId, CancellationToken ct)
    {
        var lines = StockLines(request).ToList();
        var productIds = lines.Select(x => x.TipoCespedId).Distinct().ToArray();
        var controlled = await db.TiposCesped.Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.ControlPorLotes, ct);
        var selectedLots = new HashSet<Guid>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (!controlled.GetValueOrDefault(line.TipoCespedId)) continue;
            if (!line.LoteStockId.HasValue || !selectedLots.Add(line.LoteStockId.Value))
                throw new LoteNoDisponibleException(line.LoteStockId.HasValue ? "No podés usar el mismo lote en más de una línea." : "Seleccioná un lote disponible para el producto controlado por lotes.");
            var lot = await db.LotesStock.Include(x => x.Rollos).SingleOrDefaultAsync(x => x.Id == line.LoteStockId.Value, ct);
            ValidateLotSelection(lot, line, request.DepositoId, currentSaleId);
            var quantity = lot!.Rollos.Sum(x => x.CantidadM2);
            if (lot.Rollos.Count != 3 || quantity <= 0) throw new LoteNoDisponibleException("El lote no tiene sus tres rollos correctamente registrados.");
            MarkLotSold(lot, targetSaleId);
            lines[index] = line with { CantidadM2 = quantity, Color = lot.Color, Total = Math.Round(quantity * line.PrecioVentaM2, 2) };
        }
        var first = lines[0];
        return request with { Lineas = lines, TipoCespedId = first.TipoCespedId, Color = first.Color, CantidadM2 = lines.Sum(x => x.CantidadM2), PrecioUnitario = lines.Sum(x => x.Total) / lines.Sum(x => x.CantidadM2), PrecioTotal = lines.Sum(x => x.Total), CostoCompraUnitario = lines.Sum(x => x.PrecioCompraM2 * x.CantidadM2) / lines.Sum(x => x.CantidadM2) };
    }


    internal static IReadOnlyList<VentaLineaCommand> StockLines(RegistrarVentaCommand request) =>
        request.Lineas is { Count: > 0 } ? request.Lineas :
        [new VentaLineaCommand(request.TipoCespedId, request.Color, request.CantidadM2,
            request.CostoCompraUnitario, request.PrecioUnitario, request.PrecioTotal)];

    public static async Task ValidateStock(AppDbContext db, Guid depositoId,
        IReadOnlyList<VentaLineaCommand> lines, CancellationToken ct)
    {
        foreach (var group in lines.GroupBy(x => x.TipoCespedId))
        {
            var requested = group.Sum(x => x.CantidadM2);
            var available = await StockActual(db, depositoId, group.Key, ct);
            var shortage = StockShortage(available, requested);
            if (shortage <= 0) continue;
            var product = await db.TiposCesped.Where(x => x.Id == group.Key).Select(x => x.Nombre)
                .SingleOrDefaultAsync(ct) ?? group.Key.ToString();
            throw new StockInsuficienteException(product, shortage);
        }
    }
    public static async Task<RegistrarVentaCommand> NormalizeLines(AppDbContext db,
        RegistrarVentaCommand request, CancellationToken ct)
    {
        if (request.Lineas is not { Count: > 0 }) return request;
        var ids = request.Lineas.Select(x => x.TipoCespedId).Distinct().ToArray();
        var products = await db.TiposCesped.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        if (products.Count != ids.Length) throw new KeyNotFoundException("Uno de los productos seleccionados no existe.");
        var lines = request.Lineas.Select(line =>
        {
            var product = products[line.TipoCespedId];
            var colors = JsonSerializer.Deserialize<string[]>(product.ColoresJson) ?? [];
            if (colors.Length == 0) return line with { Color = null };
            var color = colors.FirstOrDefault(x => string.Equals(x, line.Color?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (color is null) throw new KeyNotFoundException($"Seleccioná un color disponible para {product.Nombre}.");
            return line with { Color = color };
        }).ToList();
        var first = lines[0];
        return request with
        {
            Lineas = lines, TipoCespedId = first.TipoCespedId, Color = first.Color,
            CantidadM2 = lines.Sum(x => x.CantidadM2),
            PrecioUnitario = lines.Sum(x => x.Total) / lines.Sum(x => x.CantidadM2),
            PrecioTotal = lines.Sum(x => x.Total),
            CostoCompraUnitario = lines.Sum(x => x.PrecioCompraM2 * x.CantidadM2) / lines.Sum(x => x.CantidadM2)
        };
    }
    public static RegistrarVentaCommand NormalizeColor(RegistrarVentaCommand request, TipoCesped tipo)
    {
        var colors = JsonSerializer.Deserialize<string[]>(tipo.ColoresJson) ?? [];
        if (colors.Length == 0) return request with { Color = null };
        var selected = colors.FirstOrDefault(x => string.Equals(x, request.Color?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (selected is null) throw new KeyNotFoundException("Seleccioná un color disponible para el producto.");
        return request with { Color = selected };
    }

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
        var costoCompra = r.Lineas is { Count: > 0 }
            ? r.Lineas.Sum(x => x.PrecioCompraM2 * x.CantidadM2)
            : r.CostoCompraUnitario * r.CantidadM2;
        var costoOperativo = costoCompra + r.CostoEnvio + r.OtrosCostos;
        var iva = FinancialCalculator.CalculateIva(costoOperativo, porcentajeIva);
        var gananciaBruta = total - costoOperativo;
        venta.ClienteId = r.ClienteId; venta.TipoCespedId = r.TipoCespedId; venta.AlicuotaIvaId = r.AlicuotaIvaId; venta.Color = r.Color;
        venta.SucursalId = r.SucursalId ?? venta.SucursalId;
        venta.DepositoId = r.DepositoId == Guid.Empty ? venta.DepositoId : r.DepositoId;
        venta.FechaVenta = r.FechaVenta; venta.CantidadM2 = r.CantidadM2; venta.PrecioUnitario = r.PrecioUnitario;
        venta.PrecioTotal = total; venta.MontoEntrega = r.MontoEntrega; venta.CostoCompraUnitario = r.CostoCompraUnitario; venta.CostoCompraTotal = costoCompra;
        venta.CostoEnvio = r.CostoEnvio; venta.OtrosCostos = r.OtrosCostos; venta.Iva = iva;
        venta.GananciaBruta = gananciaBruta; venta.GananciaNeta = gananciaBruta - iva;
        venta.Margen = total == 0 ? 0 : venta.GananciaNeta / total; venta.FormaPago = r.FormaPago;
        venta.CantidadCuotas = r.FormaPago == FormaPago.Cuotas ? r.CantidadCuotas : null;
        venta.Estado = r.Estado; venta.FechaEntregaEstimada = r.Estado == EstadoVenta.Futura ? r.FechaEntregaEstimada : null;
        venta.Observaciones = r.Observaciones;
        venta.LineasJson = r.Lineas is { Count: > 0 } ? JsonSerializer.Serialize(r.Lineas) : null;
    }

    public static void CreateInstallments(Venta venta, RegistrarVentaCommand r)
    {
        if (r.FormaPago != FormaPago.Cuotas) return;
        var count = r.CantidadCuotas!.Value;
        var saldoFinanciado = venta.PrecioTotal - venta.MontoEntrega;
        var amount = Math.Round(saldoFinanciado / count, 2);
        for (var i = 1; i <= count; i++)
            venta.Cuotas.Add(new Cuota { VentaId = venta.Id, ClienteId = r.ClienteId, SucursalId = venta.SucursalId, Numero = i,
                FechaVencimiento = r.FechaVenta.AddMonths(i),
                ImportePactado = i == count ? saldoFinanciado - amount * (count - 1) : amount,
                Estado = EstadoCuota.Pendiente });
    }

    public static MovimientoCaja CreateCashMovement(Venta venta) => new()
    {
        Tipo = TipoMovimiento.Ingreso, Fecha = DateTimeOffset.UtcNow, Monto = venta.MontoEntrega,
        Concepto = $"Entrega inicial venta {venta.Id}", VentaId = venta.Id, SucursalId = venta.SucursalId
    };

    public static IReadOnlyList<MovimientoStock> CreateStockMovements(Venta venta,
        IReadOnlyList<VentaLineaCommand> lines, string usuario) => lines.Select(line => new MovimientoStock
        {
            DepositoId = venta.DepositoId, TipoCespedId = line.TipoCespedId,
            Tipo = TipoMovimientoStock.SalidaPorVenta, CantidadM2 = line.CantidadM2,
            Fecha = DateTime.UtcNow, Usuario = usuario, VentaId = venta.Id,
            Observaciones = $"Salida por venta {venta.Id}"
        }).ToList();

    public static VentaDto ToDto(Venta v, Cliente c, TipoCesped t) => new(v.Id, v.SucursalId, v.DepositoId, v.ClienteId,
        $"{c.Nombre} {c.Apellido}", v.TipoCespedId, t.Nombre, v.AlicuotaIvaId, v.FechaVenta,
        v.CantidadM2, v.PrecioUnitario, v.PrecioTotal, v.MontoEntrega, v.CostoCompraUnitario, v.CostoEnvio,
        v.OtrosCostos, v.FormaPago, v.CantidadCuotas, v.Estado, v.GananciaNeta, v.Margen,
        v.FechaEntregaEstimada, v.Observaciones, v.Color,
        JsonSerializer.Deserialize<List<VentaLineaCommand>>(v.LineasJson ?? "[]") ?? []);
}

public static class VentaEndpoints
{
    public static void MapVentaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/ventas").WithTags("Ventas").RequireAuthorization("ventas");
        group.MapPost("/", async (RegistrarVentaCommand command, ISender sender, CancellationToken ct) =>
        {
            try { return Results.Created("/api/ventas", await sender.Send(command, ct)); }
            catch (StockInsuficienteException exception)
            {
                return Results.Conflict(new { message = $"Stock insuficiente de {exception.Producto}. Faltan {exception.Faltante:0.##} m²." });
            }
            catch (LoteNoDisponibleException exception)
            {
                return Results.Conflict(new { message = exception.Message });
            }
            catch (VentaScopeException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["sucursal"] = [exception.Message] });
            }
        });
        group.MapGet("/", List);
        group.MapGet("/{id:guid}", GetById);
        group.MapGet("/filtros", Filters);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/entregar", (Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => ChangeStatus(id, EstadoVenta.Entregada, user, db, ct));
        group.MapPost("/{id:guid}/confirmar", ResetDelivery);
        group.MapPost("/{id:guid}/cancelar", (Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) => ChangeStatus(id, EstadoVenta.Cancelada, user, db, ct));
        app.MapGet("/api/ventas/proximas-entregas", UpcomingDeliveries).WithTags("Ventas").RequireAuthorization("entregas");
    }

    internal static IQueryable<Venta> VisibleQuery(IQueryable<Venta> query, ClaimsPrincipal user,
        Guid? sucursalId = null) => query.WhereVisibleParaUsuario(user, sucursalId);

    private static async Task<PaginatedResponse<VentaDto>> List(ClaimsPrincipal user, AppDbContext db, int page = 1, int pageSize = 20,
        string? estado = null, DateOnly? desde = null, DateOnly? hasta = null,
        Guid? clienteId = null, Guid? tipoCespedId = null, Guid? sucursalId = null, CancellationToken ct = default)
    {
        var query = VisibleQuery(db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped), user, sucursalId);
        if (Enum.TryParse<EstadoVenta>(estado, true, out var parsed)) query = query.Where(x => x.Estado == parsed);
        if (desde.HasValue) query = query.Where(x => x.FechaVenta >= desde.Value);
        if (hasta.HasValue) query = query.Where(x => x.FechaVenta <= hasta.Value);
        if (clienteId.HasValue) query = query.Where(x => x.ClienteId == clienteId.Value);
        if (tipoCespedId.HasValue)
        {
            var productId = tipoCespedId.Value.ToString();
            query = query.Where(x => x.TipoCespedId == tipoCespedId.Value ||
                (x.LineasJson != null && x.LineasJson.Contains(productId)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.FechaVenta).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(v => VentaService.ToDto(v, v.Cliente, v.TipoCesped)).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    private static async Task<IResult> GetById(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var venta = await VisibleQuery(db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped), user)
            .SingleOrDefaultAsync(x => x.Id == id, ct);
        return venta is null ? Results.NotFound() : Results.Ok(VentaService.ToDto(venta, venta.Cliente, venta.TipoCesped));
    }

    private static async Task<object> Filters(AppDbContext db, CancellationToken ct) => new
    {
        clientes = await db.Clientes.AsNoTracking().OrderBy(x => x.Apellido).ThenBy(x => x.Nombre)
            .Select(x => new
            {
                x.Id,
                nombre = x.Nombre + " " + x.Apellido,
                nombreCompleto = x.Nombre + " " + x.Apellido,
                x.Telefono,
                x.Localidad
            }).ToListAsync(ct),
        tiposCesped = await db.TiposCesped.AsNoTracking().OrderBy(x => x.Nombre)
            .Select(x => new { x.Id, x.Nombre, x.Activo }).ToListAsync(ct),
        sucursales = await db.Sucursales.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Nombre)
            .Select(x => new { x.Id, x.Nombre }).ToListAsync(ct),
        depositos = await db.Depositos.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Nombre)
            .Select(x => new
            {
                x.Id, x.Nombre,
                productos = db.TiposCesped.AsNoTracking().Where(p => p.Activo).Select(p => new
                {
                    tipoCespedId = p.Id, nombre = p.Nombre,
                    stockActualM2 = db.MovimientosStock.Where(m => m.DepositoId == x.Id && m.TipoCespedId == p.Id)
                        .Sum(m => (decimal?)(m.Tipo == TipoMovimientoStock.SalidaPorVenta ? -m.CantidadM2 : m.CantidadM2)) ?? 0m
                }).ToList()
            }).ToListAsync(ct)
    };

    private static async Task<IResult> Update(Guid id, RegistrarVentaCommand request, ClaimsPrincipal user, AppDbContext db,
        IValidator<RegistrarVentaCommand> validator, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Api.Features.Ventas.Update");
        var venta = await VisibleQuery(db.Ventas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (venta is null) return Results.NotFound();

        // La sucursal y el depósito no se cambian al editar: mover stock requiere un circuito específico.
        request = request with { SucursalId = venta.SucursalId, DepositoId = venta.DepositoId };

        try { request = await VentaService.NormalizeLines(db, request, ct); }
        catch (KeyNotFoundException exception)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Lineas"] = [exception.Message] });
        }

        var hasPaidInstallments = await db.Cuotas.AnyAsync(x => x.VentaId == id && x.ImportePagado > 0, ct);
        if (hasPaidInstallments)
            return await UpdateDateOrColorPreservingPayments(venta, request, db, ct);

        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return Results.ValidationProblem(validation.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray()));

        var (cliente, tipo, alicuota) = await VentaService.GetReferences(db, request, ct);
        request = VentaService.NormalizeColor(request, tipo);
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            await db.LotesStock.Where(x => x.VentaId == id).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Estado, EstadoLoteStock.Disponible)
                .SetProperty(x => x.VentaId, (Guid?)null), ct);
            await db.MovimientosStock.Where(x => x.VentaId == id &&
                x.Tipo == TipoMovimientoStock.SalidaPorVenta).ExecuteDeleteAsync(ct);
            db.ChangeTracker.Clear();
            request = await VentaService.ResolveControlledLots(db, request, id, id, ct);
            var stockLines = VentaService.StockLines(request);
            try { await VentaService.ValidateStock(db, venta.DepositoId, stockLines, ct); }
            catch (StockInsuficienteException exception)
            {
                return Results.Conflict(new
                {
                    message = $"Stock insuficiente de {exception.Producto}. Faltan {exception.Faltante:0.##} m²."
                });
            }

            // ExecuteDelete evita conflictos entre cuotas eliminadas y las nuevas con el mismo número.
            await db.MovimientosCaja.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);
            await db.Cuotas.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);

            var updated = new Venta { Id = id };
            VentaService.Apply(updated, request, alicuota.Porcentaje);

            var affected = await VisibleQuery(db.Ventas.Where(x => x.Id == id), user).ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.ClienteId, updated.ClienteId)
                .SetProperty(x => x.TipoCespedId, updated.TipoCespedId)
                .SetProperty(x => x.AlicuotaIvaId, updated.AlicuotaIvaId)
                .SetProperty(x => x.Color, updated.Color)
                .SetProperty(x => x.LineasJson, updated.LineasJson)
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

            db.MovimientosStock.AddRange(VentaService.CreateStockMovements(updated, stockLines,
                user.Identity?.Name ?? user.FindFirstValue("usuario") ?? "sistema"));

            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return Results.Ok(VentaService.ToDto(updated, cliente, tipo));
        }
        catch (LoteNoDisponibleException exception)
        {
            return Results.Conflict(new { message = exception.Message });
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

    internal static bool HasOnlySaleDateOrColorChanged(Venta venta, RegistrarVentaCommand request) =>
        venta.ClienteId == request.ClienteId &&
        venta.TipoCespedId == request.TipoCespedId &&
        venta.AlicuotaIvaId == request.AlicuotaIvaId &&
        venta.CantidadM2 == request.CantidadM2 &&
        venta.PrecioUnitario == request.PrecioUnitario &&
        venta.PrecioTotal == request.PrecioTotal &&
        venta.MontoEntrega == request.MontoEntrega &&
        venta.CostoCompraUnitario == request.CostoCompraUnitario &&
        venta.CostoEnvio == request.CostoEnvio &&
        venta.OtrosCostos == request.OtrosCostos &&
        venta.FormaPago == request.FormaPago &&
        venta.CantidadCuotas == request.CantidadCuotas &&
        venta.Estado == request.Estado &&
        venta.FechaEntregaEstimada == request.FechaEntregaEstimada &&
        string.Equals(venta.LineasJson ?? "", request.Lineas is { Count: > 0 } ? JsonSerializer.Serialize(request.Lineas) : "", StringComparison.Ordinal) &&
        string.Equals(venta.Observaciones ?? "", request.Observaciones ?? "", StringComparison.Ordinal);

    private static async Task<IResult> UpdateDateOrColorPreservingPayments(Venta venta, RegistrarVentaCommand request,
        AppDbContext db, CancellationToken ct)
    {
        if (!HasOnlySaleDateOrColorChanged(venta, request))
            return Results.Conflict(new
            {
                message = "Esta venta tiene cuotas cobradas. Sólo se pueden modificar la fecha de venta y el color; los importes y demás datos deben mantenerse sin cambios."
            });

        var product = await db.TiposCesped.AsNoTracking().SingleAsync(x => x.Id == venta.TipoCespedId, ct);
        var colorChanged = !string.Equals(venta.Color ?? "", request.Color?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
        if (colorChanged)
        {
            try { request = VentaService.NormalizeColor(request, product); }
            catch (KeyNotFoundException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Color"] = [exception.Message] });
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        venta.FechaVenta = request.FechaVenta;
        if (colorChanged) venta.Color = request.Color;
        var installments = await db.Cuotas.Where(x => x.VentaId == venta.Id).ToListAsync(ct);
        foreach (var installment in installments)
            installment.FechaVencimiento = request.FechaVenta.AddMonths(installment.Numero);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var client = await db.Clientes.AsNoTracking().SingleAsync(x => x.Id == venta.ClienteId, ct);
        return Results.Ok(VentaService.ToDto(venta, client, product));
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!await VisibleQuery(db.Ventas, user).AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.LotesStock.Where(x => x.VentaId == id).ExecuteUpdateAsync(setters => setters
            .SetProperty(x => x.Estado, EstadoLoteStock.Disponible)
            .SetProperty(x => x.VentaId, (Guid?)null), ct);
        await db.MovimientosStock.Where(x => x.VentaId == id && x.Tipo == TipoMovimientoStock.SalidaPorVenta).ExecuteDeleteAsync(ct);
        await db.MovimientosCaja.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);
        await db.Cuotas.Where(x => x.VentaId == id).ExecuteDeleteAsync(ct);
        await VisibleQuery(db.Ventas.Where(x => x.Id == id), user).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> ChangeStatus(Guid id, EstadoVenta status, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var venta = await VisibleQuery(db.Ventas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (venta is null) return Results.NotFound();
        if (venta.Estado == EstadoVenta.Entregada) return Results.Conflict(new { message = "Una venta entregada no puede modificarse." });
        venta.Estado = status; await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> ResetDelivery(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var venta = await VisibleQuery(db.Ventas, user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (venta is null) return Results.NotFound();
        if (venta.Estado != EstadoVenta.Entregada)
            return Results.Conflict(new { message = "Sólo se puede revertir una venta entregada." });
        venta.Estado = EstadoVenta.Confirmada;
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UpcomingDeliveries(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var rows = await VisibleQuery(db.Ventas.AsNoTracking().Include(x => x.Cliente).Include(x => x.TipoCesped), user)
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
