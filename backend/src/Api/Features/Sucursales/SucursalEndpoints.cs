using Api.Features.Auth;
using Api.Shared.Common;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Features.Sucursales;

public record DepositoRequest(string Nombre, bool Activo = true);
public record SucursalRequest(string Nombre, Guid? DepositoPropioId, int? PuntoVentaAfip, bool Activo = true,
    string? NombreDeposito = null, string? RazonSocial = null, string? Cuit = null,
    string? CondicionIva = null, string? DomicilioFiscal = null, string? Localidad = null,
    string? Telefono = null, string? Email = null, DateOnly? InicioActividades = null,
    string? IngresosBrutos = null);
public record EstadoMaestroRequest(bool Activo);

public static class SucursalEndpoints
{
    internal static readonly Guid HistoricalSucursalId = Guid.Parse("7B100000-0000-0000-0000-FFFFFFFFFFFF");
    private static readonly HashSet<string> CondicionesIva = new(StringComparer.OrdinalIgnoreCase)
        { "Responsable Inscripto", "Monotributo", "Exento" };

    public static void MapSucursalEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/sucursales").WithTags("Administración - Sucursales");
        g.MapGet("/depositos", ListDepositos).RequireAuthorization("Administrador");
        g.MapPost("/depositos", CreateDeposito).RequireAuthorization("Administrador");
        g.MapPut("/depositos/{id:guid}", UpdateDeposito).RequireAuthorization("Administrador");
        g.MapPatch("/depositos/{id:guid}/estado", SetDepositoEstado).RequireAuthorization("Administrador");
        g.MapDelete("/depositos/{id:guid}", DeleteDeposito).RequireAuthorization("Administrador");
        g.MapGet("/", ListSucursales).RequireAuthorization("Administrador");
        g.MapPost("/", CreateSucursal).RequireAuthorization("Administrador");
        g.MapPut("/{id:guid}", UpdateSucursal).RequireAuthorization("Administrador");
        g.MapPatch("/{id:guid}/estado", SetSucursalEstado).RequireAuthorization("Administrador");
        g.MapDelete("/{id:guid}", DeleteSucursal).RequireAuthorization("Administrador");
    }

    private static object DepositoDto(Deposito x) => new { x.Id, x.SucursalId, x.Nombre, x.Activo };
    private static object SucursalDto(Sucursal x) => new {
        x.Id, x.Nombre, x.DepositoPropioId, depositoPropioNombre = x.DepositoPropio.Nombre,
        x.PuntoVentaAfip, x.Activo, x.RazonSocial, x.Cuit, x.CondicionIva, x.DomicilioFiscal,
        x.Localidad, x.Telefono, x.Email, x.InicioActividades, x.IngresosBrutos,
        usuariosAsignados = x.Usuarios.Count
    };
    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    internal static string? NormalizeCuit(string? value)
    {
        value = Clean(value);
        if (value is null) return null;
        return value.All(c => char.IsDigit(c) || c is ' ' or '-')
            ? new string(value.Where(char.IsDigit).ToArray()) : value;
    }
    internal static bool IsValidCuit(string value)
    {
        if (value.Length != 11 || !value.All(char.IsDigit)) return false;
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = value.Take(10).Select((c, i) => (c - '0') * weights[i]).Sum();
        var verifier = 11 - sum % 11;
        verifier = verifier == 11 ? 0 : verifier == 10 ? 9 : verifier;
        return verifier == value[10] - '0';
    }
    internal static Dictionary<string, string[]> Validate(SucursalRequest r)
    {
        var e = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(r.Nombre)) e["nombre"] = ["El nombre es obligatorio."];
        else if (r.Nombre.Trim().Length > 150) e["nombre"] = ["El nombre no puede superar 150 caracteres."];
        var cuit = NormalizeCuit(r.Cuit);
        if (cuit is not null && !IsValidCuit(cuit)) e["cuit"] = ["Ingresá un CUIT válido de 11 dígitos."];
        if (r.PuntoVentaAfip is < 1 or > 99999) e["puntoVentaAfip"] = ["El punto de venta debe estar entre 1 y 99999."];
        var condicion = Clean(r.CondicionIva);
        if (condicion is not null && !CondicionesIva.Contains(condicion)) e["condicionIva"] = ["Seleccioná una condición de IVA válida."];
        if (Clean(r.NombreDeposito)?.Length > 150) e["nombreDeposito"] = ["El nombre del depósito no puede superar 150 caracteres."];
        AddMax(e, "razonSocial", r.RazonSocial, 200);
        AddMax(e, "domicilioFiscal", r.DomicilioFiscal, 250);
        AddMax(e, "localidad", r.Localidad, 150);
        AddMax(e, "telefono", r.Telefono, 50);
        AddMax(e, "email", r.Email, 200);
        AddMax(e, "ingresosBrutos", r.IngresosBrutos, 50);
        return e;
    }
    private static void AddMax(Dictionary<string, string[]> errors, string field, string? value, int max)
    {
        if (Clean(value)?.Length > max) errors[field] = [$"No puede superar {max} caracteres."];
    }
    private static Dictionary<string, string[]>? ValidateName(string? n) =>
        string.IsNullOrWhiteSpace(n) ? new() { ["nombre"] = ["El nombre es obligatorio."] } :
        n.Trim().Length > 150 ? new() { ["nombre"] = ["El nombre no puede superar 150 caracteres."] } : null;
    private static void Apply(Sucursal x, SucursalRequest r)
    {
        x.Nombre = r.Nombre.Trim(); x.PuntoVentaAfip = r.PuntoVentaAfip; x.Activo = r.Activo;
        x.RazonSocial = Clean(r.RazonSocial); x.Cuit = NormalizeCuit(r.Cuit);
        x.CondicionIva = Clean(r.CondicionIva); x.DomicilioFiscal = Clean(r.DomicilioFiscal);
        x.Localidad = Clean(r.Localidad); x.Telefono = Clean(r.Telefono); x.Email = Clean(r.Email);
        x.InicioActividades = r.InicioActividades; x.IngresosBrutos = Clean(r.IngresosBrutos);
    }

    private static async Task<IReadOnlyList<object>> ListDepositos(ClaimsPrincipal u, AppDbContext db, CancellationToken ct) =>
        (await SucursalDepositoService.VisibleDeposits(db.Depositos.AsNoTracking(), u).OrderBy(x => x.Nombre).ToListAsync(ct)).Select(DepositoDto).ToList();
    private static async Task<IResult> CreateDeposito(DepositoRequest r, ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var e = ValidateName(r.Nombre); if (e is not null) return Results.ValidationProblem(e);
        var sid = u.SucursalId(); if (!sid.HasValue) return Results.ValidationProblem(new Dictionary<string, string[]> { ["sucursal"] = ["Seleccioná una sucursal activa."] });
        var name = r.Nombre.Trim();
        if (await db.Depositos.AnyAsync(x => x.SucursalId == sid && x.Nombre == name, ct)) return Results.Conflict(new { message = "Ya existe un depósito con ese nombre." });
        var x = new Deposito { SucursalId = sid, Nombre = name, Activo = r.Activo }; db.Add(x); await db.SaveChangesAsync(ct);
        return Results.Created($"/api/sucursales/depositos/{x.Id}", DepositoDto(x));
    }
    private static async Task<IResult> UpdateDeposito(Guid id, DepositoRequest r, ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var e = ValidateName(r.Nombre); if (e is not null) return Results.ValidationProblem(e);
        var x = await SucursalDepositoService.VisibleDeposits(db.Depositos, u).SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return Results.NotFound();
        var name = r.Nombre.Trim();
        if (await db.Depositos.AnyAsync(d => d.Id != id && d.SucursalId == x.SucursalId && d.Nombre == name, ct)) return Results.Conflict(new { message = "Ya existe un depósito con ese nombre." });
        if (!r.Activo && await db.Sucursales.AnyAsync(s => s.DepositoPropioId == id && s.Activo, ct)) return Results.Conflict(new { message = "El depósito pertenece a una sucursal activa." });
        x.Nombre = name; x.Activo = r.Activo; await db.SaveChangesAsync(ct); return Results.Ok(DepositoDto(x));
    }
    private static async Task<IResult> SetDepositoEstado(Guid id, EstadoMaestroRequest r, ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var x = await SucursalDepositoService.VisibleDeposits(db.Depositos, u).SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return Results.NotFound();
        if (!r.Activo && await db.Sucursales.AnyAsync(s => s.DepositoPropioId == id && s.Activo, ct)) return Results.Conflict(new { message = "El depósito pertenece a una sucursal activa." });
        x.Activo = r.Activo; await db.SaveChangesAsync(ct); return Results.Ok(DepositoDto(x));
    }
    private static async Task<IResult> DeleteDeposito(Guid id, ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var x = await SucursalDepositoService.VisibleDeposits(db.Depositos, u).SingleOrDefaultAsync(x => x.Id == id, ct); if (x is null) return Results.NotFound();
        if (await db.MovimientosStock.AnyAsync(m => m.DepositoId == id, ct) || await db.Sucursales.AnyAsync(s => s.DepositoPropioId == id, ct))
            return Results.Conflict(new { message = "El depósito tiene datos asociados. Podés desactivarlo en lugar de eliminarlo." });
        db.Remove(x); await db.SaveChangesAsync(ct); return Results.NoContent();
    }

    private static async Task<IReadOnlyList<object>> ListSucursales(AppDbContext db, CancellationToken ct) =>
        (await db.Sucursales.AsNoTracking().Include(x => x.DepositoPropio).Include(x => x.Usuarios).OrderBy(x => x.Nombre).ToListAsync(ct)).Select(SucursalDto).ToList();
    private static async Task<IResult?> CommonValidation(SucursalRequest r, Guid? except, AppDbContext db, CancellationToken ct)
    {
        var e = Validate(r); if (e.Count > 0) return Results.ValidationProblem(e);
        if (await db.Sucursales.AnyAsync(x => x.Id != except && x.Nombre == r.Nombre.Trim(), ct)) return Results.Conflict(new { message = "Ya existe una sucursal con ese nombre." });
        var cuit = NormalizeCuit(r.Cuit);
        if (cuit is not null && r.PuntoVentaAfip.HasValue &&
            await db.Sucursales.AnyAsync(x => x.Id != except && x.Cuit == cuit && x.PuntoVentaAfip == r.PuntoVentaAfip, ct))
            return Results.Conflict(new { message = "Ya existe una sucursal con ese CUIT y punto de venta." });
        return null;
    }
    private static async Task<IResult> CreateSucursal(SucursalRequest r, AppDbContext db, CancellationToken ct)
    {
        var invalid = await CommonValidation(r, null, db, ct); if (invalid is not null) return invalid;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                Deposito d;
                if (r.DepositoPropioId.HasValue)
                {
                    d = await db.Depositos.SingleOrDefaultAsync(x => x.Id == r.DepositoPropioId && x.Activo, ct) ?? null!;
                    if (d is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoPropioId"] = ["Seleccioná un depósito activo."] });
                    if (d.SucursalId.HasValue) return Results.Conflict(new { message = "El depósito seleccionado ya pertenece a otra sucursal." });
                }
                else
                {
                    d = new Deposito { Nombre = Clean(r.NombreDeposito) ?? r.Nombre.Trim(), Activo = true };
                    db.Add(d); await db.SaveChangesAsync(ct);
                }
                var x = new Sucursal { DepositoPropioId = d.Id, DepositoPropio = d }; Apply(x, r);
                db.Add(x); await db.SaveChangesAsync(ct); d.SucursalId = x.Id; await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct); return Results.Created($"/api/sucursales/{x.Id}", SucursalDto(x));
            }
            catch (DbUpdateException)
            {
                await tx.RollbackAsync(ct);
                return Results.Conflict(new { message = "Ya existe una sucursal o depósito con esos datos." });
            }
        });
    }
    private static async Task<IResult> UpdateSucursal(Guid id, SucursalRequest r, AppDbContext db, CancellationToken ct)
    {
        var x = await db.Sucursales.Include(s => s.DepositoPropio).Include(s => s.Usuarios).SingleOrDefaultAsync(s => s.Id == id, ct); if (x is null) return Results.NotFound();
        if (id == HistoricalSucursalId) return Results.Conflict(new { message = "La sucursal histórica no puede editarse fiscalmente." });
        var invalid = await CommonValidation(r, id, db, ct); if (invalid is not null) return invalid;
        if (r.DepositoPropioId.HasValue && r.DepositoPropioId != x.DepositoPropioId)
        {
            var d = await db.Depositos.SingleOrDefaultAsync(d => d.Id == r.DepositoPropioId && d.Activo, ct);
            if (d is null) return Results.ValidationProblem(new Dictionary<string, string[]> { ["depositoPropioId"] = ["Seleccioná un depósito activo."] });
            if (d.SucursalId.HasValue && d.SucursalId != id) return Results.Conflict(new { message = "El depósito seleccionado pertenece a otra sucursal." });
            d.SucursalId = id; x.DepositoPropioId = d.Id; x.DepositoPropio = d;
        }
        Apply(x, r);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { return Results.Conflict(new { message = "Ya existe una sucursal con ese CUIT, punto de venta o nombre." }); }
        return Results.Ok(SucursalDto(x));
    }
    private static async Task<IResult> SetSucursalEstado(Guid id, EstadoMaestroRequest r, AppDbContext db, CancellationToken ct)
    {
        var x = await db.Sucursales.Include(s => s.DepositoPropio).Include(s => s.Usuarios).SingleOrDefaultAsync(s => s.Id == id, ct); if (x is null) return Results.NotFound();
        if (id == HistoricalSucursalId) return Results.Conflict(new { message = "La sucursal histórica no puede modificarse." });
        if (r.Activo && !x.DepositoPropio.Activo) return Results.Conflict(new { message = "No se puede activar una sucursal cuyo depósito está inactivo." });
        x.Activo = r.Activo; await db.SaveChangesAsync(ct); return Results.Ok(SucursalDto(x));
    }
    private static async Task<IResult> DeleteSucursal(Guid id, AppDbContext db, CancellationToken ct)
    {
        var x = await db.Sucursales.Include(s => s.Depositos).SingleOrDefaultAsync(s => s.Id == id, ct); if (x is null) return Results.NotFound();
        if (id == HistoricalSucursalId) return Results.Conflict(new { message = "La sucursal histórica no puede eliminarse." });
        var depositIds = x.Depositos.Select(d => d.Id).ToList();
        var hasData = await db.Usuarios.AnyAsync(u => u.SucursalId == id, ct)
            || await db.MovimientosStock.AnyAsync(m => depositIds.Contains(m.DepositoId), ct)
            || await db.Clientes.IgnoreQueryFilters().AnyAsync(v => v.SucursalId == id, ct)
            || await db.Ventas.IgnoreQueryFilters().AnyAsync(v => v.SucursalId == id, ct)
            || await db.Cuotas.IgnoreQueryFilters().AnyAsync(v => v.SucursalId == id, ct)
            || await db.MovimientosCaja.IgnoreQueryFilters().AnyAsync(v => v.SucursalId == id, ct)
            || await db.Presupuestos.IgnoreQueryFilters().AnyAsync(v => v.SucursalId == id, ct)
            || await db.Gastos.IgnoreQueryFilters().AnyAsync(v => v.SucursalId == id, ct)
            || await db.RegistrosAuditoria.AnyAsync(v => v.SucursalId == id, ct);
        if (hasData) return Results.Conflict(new { message = "La sucursal tiene datos asociados. Podés desactivarla en lugar de eliminarla." });
        foreach (var d in x.Depositos) d.SucursalId = null;
        db.Remove(x); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
}
