using System.Security.Claims;
using System.Data;
using Api.Features.Auth;
using Api.Features.Stock;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Importaciones;

public sealed record ImportacionRequest(string Codigo, string? NumeroDespacho, DateOnly Fecha, string? Observaciones);
public sealed record RecepcionImportacionRequest(Guid? ImportacionId, ImportacionRequest? Nueva, Guid DepositoId,
    string? Observaciones, IReadOnlyList<RecepcionImportacionLinea>? Lineas);
public sealed record RecepcionImportacionLinea(Guid TipoCespedId, decimal? CantidadM2, string? Color,
    IReadOnlyList<RolloLoteRequest>? Rollos);

public static class ImportacionEndpoints
{
    public static void MapImportacionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/importaciones").WithTags("Importaciones");
        group.MapGet("/", List).RequirePermiso("stock", "ver");
        group.MapGet("/saldos", Saldos).RequirePermiso("stock", "ver");
        app.MapGet("/api/ventas/importaciones/saldos", Saldos).WithTags("Importaciones")
            .RequirePermiso("ventas", "crear");
        group.MapGet("/{id:guid}", Detail).RequirePermiso("stock", "ver");
        group.MapPost("/", Create).RequirePermiso("stock", "crear");
        group.MapPost("/recepcion", Receive).RequirePermiso("stock", "crear");
        group.MapPut("/{id:guid}", Update).RequirePermiso("stock", "editar");
        group.MapDelete("/{id:guid}", Delete).RequirePermiso("stock", "eliminar");
    }

    private static async Task<IResult> List(string? buscar, int page, int pageSize, ClaimsPrincipal user,
        AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize == 0 ? 20 : pageSize, 1, 100);
        var visibleDeposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        var query = db.Importaciones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var term = buscar.Trim().ToLower();
            query = query.Where(x => x.Codigo.ToLower().Contains(term) ||
                (x.NumeroDespacho != null && x.NumeroDespacho.ToLower().Contains(term)));
        }
        var total = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(x => x.Fecha).ThenBy(x => x.Codigo)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, x.Codigo, x.NumeroDespacho, x.Fecha, x.Observaciones,
                totalIngresadoM2 = x.Movimientos.Where(m => visibleDeposits.Contains(m.DepositoId) && m.Tipo == TipoMovimientoStock.Ingreso).Sum(m => (decimal?)m.CantidadM2) ?? 0,
                saldoM2 = x.Movimientos.Where(m => visibleDeposits.Contains(m.DepositoId)).Sum(m => (decimal?)(m.Tipo == TipoMovimientoStock.SalidaPorVenta || m.Tipo == TipoMovimientoStock.AjusteSalida ? -m.CantidadM2 : m.CantidadM2)) ?? 0
            }).ToListAsync(ct);
        return Results.Ok(new { items = rows, total, page, pageSize, totalPages = (int)Math.Ceiling(total / (double)pageSize) });
    }

    private static async Task<IResult> Detail(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var importacion = await db.Importaciones.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (importacion is null) return Results.NotFound();
        var visibleDeposits = SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), user).Select(x => x.Id);
        var balances = await db.MovimientosStock.AsNoTracking()
            .Where(x => x.ImportacionId == id && visibleDeposits.Contains(x.DepositoId))
            .GroupBy(x => new { x.DepositoId, Deposito = x.Deposito.Nombre, x.TipoCespedId, Producto = x.TipoCesped.Nombre })
            .Select(x => new
            {
                x.Key.DepositoId, depositoNombre = x.Key.Deposito, x.Key.TipoCespedId, productoNombre = x.Key.Producto,
                ingresadoM2 = x.Sum(m => m.Tipo == TipoMovimientoStock.Ingreso || m.Tipo == TipoMovimientoStock.Ajuste ? m.CantidadM2 : 0),
                vendidoM2 = x.Sum(m => m.Tipo == TipoMovimientoStock.SalidaPorVenta ? m.CantidadM2 : 0),
                saldoM2 = x.Sum(m => m.Tipo == TipoMovimientoStock.SalidaPorVenta || m.Tipo == TipoMovimientoStock.AjusteSalida ? -m.CantidadM2 : m.CantidadM2)
            })
            .OrderBy(x => x.depositoNombre).ThenBy(x => x.productoNombre).ToListAsync(ct);
        var lots = await db.LotesStock.AsNoTracking().Where(x => x.ImportacionId == id && visibleDeposits.Contains(x.DepositoId))
            .OrderBy(x => x.FechaIngreso).Select(x => new
            {
                x.Id, x.DepositoId, depositoNombre = x.Deposito.Nombre, x.TipoCespedId,
                productoNombre = x.TipoCesped.Nombre, x.Color, x.Estado,
                cantidadM2 = x.Rollos.Sum(r => r.CantidadM2),
                rollos = x.Rollos.OrderBy(r => r.Posicion).Select(r => new { r.Posicion, r.CodigoBarra, r.CantidadM2 }).ToList()
            }).ToListAsync(ct);
        var movements = await db.MovimientosStock.AsNoTracking()
            .Where(x => x.ImportacionId == id && visibleDeposits.Contains(x.DepositoId))
            .OrderByDescending(x => x.Fecha).Take(50)
            .Select(x => new { x.Id, x.DepositoId, depositoNombre = x.Deposito.Nombre, x.TipoCespedId, productoNombre = x.TipoCesped.Nombre, x.Tipo, x.CantidadM2, x.Fecha, x.Usuario, x.Observaciones })
            .ToListAsync(ct);
        return Results.Ok(new { importacion.Id, importacion.Codigo, importacion.NumeroDespacho, importacion.Fecha, importacion.Observaciones, saldos = balances, lotes = lots, movimientos = movements });
    }

    private static async Task<IResult> Receive(RecepcionImportacionRequest request, ClaimsPrincipal user,
        AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var branchId = user.SucursalId();
        if (!branchId.HasValue) errors["sucursalId"] = ["Elegí una sucursal activa."];
        if (request.Lineas is null || request.Lineas.Count == 0) errors["lineas"] = ["Agregá al menos una línea."];
        else if (request.Lineas.Count > 100) errors["lineas"] = ["La recepción admite hasta 100 líneas."];
        var observations = Clean(request.Observaciones);
        if (observations?.Length > 500) errors["observaciones"] = ["Las observaciones pueden tener hasta 500 caracteres."];
        if (!request.ImportacionId.HasValue)
        {
            if (request.Nueva is null) errors["nueva"] = ["Completá los datos de la nueva importación."];
            else foreach (var error in Validate(request.Nueva)) errors[$"nueva.{error.Key}"] = error.Value;
        }
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        if (!await SucursalDepositoService.CanUseDeposit(user, db, request.DepositoId, ct))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoId"] = ["Seleccioná un depósito de la sucursal activa."] });

        var importation = request.ImportacionId.HasValue
            ? await db.Importaciones.SingleOrDefaultAsync(x => x.Id == request.ImportacionId.Value, ct)
            : null;
        if (request.ImportacionId.HasValue && importation is null) return Results.NotFound(new { message = "La importación no pertenece a la sucursal activa." });

        var lines = request.Lineas!;
        var productIds = lines.Select(x => x.TipoCespedId).Distinct().ToArray();
        var products = await db.TiposCesped.Where(x => productIds.Contains(x.Id) && x.Activo &&
            (x.DepositoId == request.DepositoId || x.Depositos.Any(d => d.DepositoId == request.DepositoId)))
            .ToDictionaryAsync(x => x.Id, ct);
        var allCodes = new List<string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index]; var key = $"lineas[{index}]";
            if (!products.TryGetValue(line.TipoCespedId, out var product))
            { errors[$"{key}.tipoCespedId"] = ["Seleccioná un producto activo del depósito."]; continue; }
            if (!product.EsImportado)
            { errors[$"{key}.tipoCespedId"] = [$"{product.Nombre} no está marcado como importado. Marcalo en Productos antes de recibirlo."]; continue; }
            if (!product.ControlPorLotes)
            {
                if (line.CantidadM2 is null or <= 0) errors[$"{key}.cantidadM2"] = ["La cantidad debe ser mayor que cero."];
                if (line.Rollos is { Count: > 0 }) errors[$"{key}.rollos"] = ["Este producto no se controla por lotes."];
                continue;
            }
            var rollErrors = StockEndpoints.ValidateRolls(line.Rollos);
            foreach (var error in rollErrors) errors[$"{key}.{error.Key}"] = error.Value;
            var colors = System.Text.Json.JsonSerializer.Deserialize<string[]>(product.ColoresJson) ?? [];
            var color = line.Color?.Trim();
            if (colors.Length > 0 && (string.IsNullOrWhiteSpace(color) || !colors.Contains(color, StringComparer.OrdinalIgnoreCase)))
                errors[$"{key}.color"] = ["Seleccioná uno de los colores configurados para el producto."];
            if (line.Rollos is not null) allCodes.AddRange(line.Rollos.Where(x => !string.IsNullOrWhiteSpace(x.CodigoBarra)).Select(x => x.CodigoBarra.Trim()));
        }
        if (allCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != allCodes.Count)
            errors["lineas"] = ["Los códigos de barra no pueden repetirse dentro de la recepción."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);
        var existingCodes = await db.Rollos.Where(x => allCodes.Contains(x.CodigoBarra)).Select(x => x.CodigoBarra).ToListAsync(ct);
        if (StockEndpoints.HasPreviouslyUsedBarcode(allCodes, existingCodes))
            return Results.Conflict(new { message = "Uno o más códigos de barra ya están registrados en otro lote." });

        var strategy = db.Database.CreateExecutionStrategy();
        var attempt = 0; var total = 0m;
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                if (attempt++ > 0) db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
                if (!request.ImportacionId.HasValue)
                {
                    var data = request.Nueva!; var code = data.Codigo.Trim();
                    if (await db.Importaciones.AnyAsync(x => x.Codigo == code, ct))
                        throw new DuplicateImportationCodeException();
                    importation = new Importacion { SucursalId = branchId!.Value, Codigo = code,
                        NumeroDespacho = Clean(data.NumeroDespacho), Fecha = data.Fecha, Observaciones = Clean(data.Observaciones) };
                    db.Importaciones.Add(importation);
                }
                else importation = await db.Importaciones.SingleAsync(x => x.Id == request.ImportacionId.Value, ct);

                total = 0;
                foreach (var line in lines)
                {
                    var product = products[line.TipoCespedId];
                    if (!product.ControlPorLotes)
                    {
                        // Las líneas repetidas se conservan como ingresos separados para mantener el detalle de la recepción.
                        var quantity = line.CantidadM2!.Value; total += quantity;
                        db.MovimientosStock.Add(new MovimientoStock { DepositoId = request.DepositoId,
                            TipoCespedId = product.Id, ImportacionId = importation.Id, Tipo = TipoMovimientoStock.Ingreso,
                            CantidadM2 = quantity, Fecha = DateTime.UtcNow, Usuario = UserName(user), Observaciones = observations });
                    }
                    else
                    {
                        var registration = StockEndpoints.CreateLotRegistration(new IngresoLoteRequest(request.DepositoId,
                            product.Id, line.Color, line.Rollos, observations, importation.Id), UserName(user), DateTime.UtcNow);
                        total += registration.Movimiento.CantidadM2;
                        db.LotesStock.Add(registration.Lote); db.MovimientosStock.Add(registration.Movimiento);
                    }
                }
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
        }
        catch (DuplicateImportationCodeException) { return Results.Conflict(new { message = "Ya existe una importación con ese código." }); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "No se pudo guardar la recepción. Verificá que el código de importación y los códigos de barra no estén repetidos." }); }
        return Results.Created($"/api/importaciones/{importation!.Id}", new
        { importacionId = importation.Id, importation.Codigo, lineasIngresadas = lines.Count, totalM2 = total });
    }

    private static async Task<IResult> Create(ImportacionRequest request, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var branch = user.SucursalId();
        if (!branch.HasValue) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sucursalId"] = ["Elegí una sucursal activa."] });
        var code = request.Codigo.Trim();
        if (await db.Importaciones.AnyAsync(x => x.Codigo == code, ct))
            return Results.Conflict(new { message = "Ya existe una importación con ese código." });
        var entity = new Importacion { SucursalId = branch.Value, Codigo = code, NumeroDespacho = Clean(request.NumeroDespacho), Fecha = request.Fecha, Observaciones = Clean(request.Observaciones) };
        db.Add(entity);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Ya existe una importación con ese código." }); }
        return Results.Created($"/api/importaciones/{entity.Id}", entity);
    }

    private static async Task<IResult> Update(Guid id, ImportacionRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = Validate(request); if (errors.Count > 0) return Results.ValidationProblem(errors);
        var entity = await db.Importaciones.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Results.NotFound();
        var code = request.Codigo.Trim();
        if (!entity.Codigo.Equals(code, StringComparison.OrdinalIgnoreCase) &&
            await db.MovimientosStock.AnyAsync(x => x.ImportacionId == id && x.Tipo == TipoMovimientoStock.SalidaPorVenta, ct))
            return Results.Conflict(new { message = "No se puede cambiar el código porque la importación ya tiene ventas asociadas." });
        if (await db.Importaciones.AnyAsync(x => x.Id != id && x.Codigo == code, ct))
            return Results.Conflict(new { message = "Ya existe una importación con ese código." });
        entity.Codigo = code; entity.NumeroDespacho = Clean(request.NumeroDespacho); entity.Fecha = request.Fecha; entity.Observaciones = Clean(request.Observaciones);
        await db.SaveChangesAsync(ct); return Results.Ok(entity);
    }

    private static async Task<IResult> Delete(Guid id, AppDbContext db, CancellationToken ct)
    {
        var entity = await db.Importaciones.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) return Results.NotFound();
        if (await db.MovimientosStock.AnyAsync(x => x.ImportacionId == id, ct) || await db.LotesStock.AnyAsync(x => x.ImportacionId == id, ct))
            return Results.Conflict(new { message = "No se puede eliminar una importación que tiene movimientos o lotes asociados." });
        db.Remove(entity); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IResult> Saldos(Guid depositoId, Guid tipoCespedId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!await SucursalDepositoService.CanUseDeposit(user, db, depositoId, ct)) return Results.NotFound();
        var grouped = await db.MovimientosStock.AsNoTracking().Where(x => x.DepositoId == depositoId && x.TipoCespedId == tipoCespedId)
            .GroupBy(x => x.ImportacionId).Select(x => new { importacionId = x.Key, saldoM2 = x.Sum(m => m.Tipo == TipoMovimientoStock.SalidaPorVenta || m.Tipo == TipoMovimientoStock.AjusteSalida ? -m.CantidadM2 : m.CantidadM2) })
            .Where(x => x.saldoM2 > 0).ToListAsync(ct);
        var ids = grouped.Where(x => x.importacionId.HasValue).Select(x => x.importacionId!.Value).ToArray();
        var imports = await db.Importaciones.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var rows = grouped.Select(x => new { x.importacionId, codigo = x.importacionId.HasValue ? imports.GetValueOrDefault(x.importacionId.Value)?.Codigo : null, etiqueta = x.importacionId.HasValue ? imports.GetValueOrDefault(x.importacionId.Value)?.Codigo : "Stock anterior (sin código)", x.saldoM2, fecha = x.importacionId.HasValue ? imports.GetValueOrDefault(x.importacionId.Value)?.Fecha : DateOnly.MinValue })
            .OrderBy(x => x.fecha).ThenBy(x => x.codigo).ToList();
        return Results.Ok(rows);
    }

    private static Dictionary<string, string[]> Validate(ImportacionRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Codigo)) errors["codigo"] = ["Ingresá el código de importación."];
        else if (request.Codigo.Trim().Length > 50) errors["codigo"] = ["El código puede tener hasta 50 caracteres."];
        if (request.NumeroDespacho?.Trim().Length > 100) errors["numeroDespacho"] = ["El número de despacho puede tener hasta 100 caracteres."];
        if (request.Observaciones?.Trim().Length > 1000) errors["observaciones"] = ["Las observaciones pueden tener hasta 1000 caracteres."];
        return errors;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string UserName(ClaimsPrincipal user) => user.Identity?.Name ?? user.FindFirstValue("usuario") ?? "sistema";
    private sealed class DuplicateImportationCodeException : Exception;
}
