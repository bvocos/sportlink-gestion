using Api.Shared.Database;
using Api.Shared.Common;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Api.Features.Auditoria;

public static class AuditoriaEndpoints
{
    internal static IQueryable<RegistroAuditoria> VisibleQuery(IQueryable<RegistroAuditoria> query, ClaimsPrincipal user)
    {
        var sucursalId = user.SucursalId();
        return query.Where(x => x.SucursalId == null || x.SucursalId == sucursalId);
    }

    public static void MapAuditoriaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auditoria", List).WithTags("Auditoría").RequireAuthorization("Administrador");
    }

    private static async Task<IResult> List(string? modulo, string? accion, string? buscar, DateOnly? desde, DateOnly? hasta, int page, int pageSize, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);
        var query = VisibleQuery(db.RegistrosAuditoria.AsNoTracking(), user);
        if (!string.IsNullOrWhiteSpace(modulo)) query = query.Where(x => x.Modulo == modulo);
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(x => x.Accion == accion);
        if (desde.HasValue) query = query.Where(x => x.FechaHora >= new DateTimeOffset(desde.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (hasta.HasValue) query = query.Where(x => x.FechaHora < new DateTimeOffset(hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (!string.IsNullOrWhiteSpace(buscar)) query = query.Where(x => x.Usuario.Contains(buscar) || x.Entidad.Contains(buscar) || x.EntidadId.Contains(buscar) || x.DetalleJson.Contains(buscar));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.FechaHora).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { x.Id, x.SucursalId, historico = x.SucursalId == null, x.FechaHora, x.UsuarioId,
                x.Usuario, x.Modulo, x.Accion, x.Entidad, x.EntidadId, x.DetalleJson }).ToListAsync(ct);
        return Results.Ok(new { items, total, page, pageSize, modulos = new[] { "Ventas", "Cuotas", "Caja", "Gastos", "Clientes", "Usuarios", "Administración" } });
    }
}
