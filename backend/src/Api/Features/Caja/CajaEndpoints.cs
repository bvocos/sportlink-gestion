using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Caja;

public record MovimientoRequest(TipoMovimiento Tipo, decimal Monto, string? Concepto, string? Usuario);

public static class CajaEndpoints
{
    public static void MapCajaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/caja").WithTags("Caja");
        group.MapGet("/", async (AppDbContext db, CancellationToken ct) => new
        {
            saldo = await db.MovimientosCaja.SumAsync(x => x.Tipo == TipoMovimiento.Ingreso ? x.Monto : -x.Monto, ct),
            movimientos = await db.MovimientosCaja.AsNoTracking().OrderByDescending(x => x.Fecha).Take(100).ToListAsync(ct)
        });
        group.MapPost("/movimientos", RegistrarMovimiento);
    }

    private static async Task<IResult> RegistrarMovimiento(MovimientoRequest request, AppDbContext db, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        var concepto = request.Concepto?.Trim();
        if (request.Monto <= 0) errors["monto"] = ["El monto debe ser mayor que cero."];
        if (string.IsNullOrWhiteSpace(concepto)) errors["concepto"] = ["La observación es obligatoria."];
        if (concepto?.Length > 500) errors["concepto"] = ["La observación no puede superar 500 caracteres."];
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var movement = new MovimientoCaja
        {
            Tipo = request.Tipo, Monto = request.Monto, Concepto = concepto!,
            Usuario = string.IsNullOrWhiteSpace(request.Usuario) ? "Administrador" : request.Usuario.Trim(),
            Fecha = DateTimeOffset.UtcNow
        };
        db.MovimientosCaja.Add(movement);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/caja/movimientos/{movement.Id}", movement);
    }
}
