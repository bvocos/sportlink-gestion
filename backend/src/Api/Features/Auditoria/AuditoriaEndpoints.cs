using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Auditoria;

public static class AuditoriaEndpoints
{
    public static void MapAuditoriaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auditoria", List).WithTags("Auditoría").RequireAuthorization("Administrador");
    }

    private static async Task<IResult> List(string? modulo, string? accion, string? buscar, DateOnly? desde, DateOnly? hasta, int page, int pageSize, AppDbContext db, CancellationToken ct)
    {
        page = Math.Max(page, 1); pageSize = Math.Clamp(pageSize == 0 ? 50 : pageSize, 1, 200);
        var query = db.RegistrosAuditoria.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(modulo)) query = query.Where(x => x.Modulo == modulo);
        if (!string.IsNullOrWhiteSpace(accion)) query = query.Where(x => x.Accion == accion);
        if (desde.HasValue) query = query.Where(x => x.FechaHora >= new DateTimeOffset(desde.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (hasta.HasValue) query = query.Where(x => x.FechaHora < new DateTimeOffset(hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        if (!string.IsNullOrWhiteSpace(buscar)) query = query.Where(x => x.Usuario.Contains(buscar) || x.Entidad.Contains(buscar) || x.EntidadId.Contains(buscar) || x.DetalleJson.Contains(buscar));
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.FechaHora).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return Results.Ok(new { items, total, page, pageSize, modulos = new[] { "Ventas", "Cuotas", "Caja", "Gastos", "Clientes", "Usuarios", "Administración" } });
    }
}
