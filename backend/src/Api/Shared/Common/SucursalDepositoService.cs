using System.Security.Claims;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Shared.Common;

public static class SucursalDepositoService
{
    public static async Task<Guid?> GetOwnDepositId(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (user.IsInRole("Administrador")) return null;
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return null;
        return await db.Usuarios.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => x.SucursalId.HasValue ? (Guid?)x.Sucursal!.DepositoPropioId : null)
            .SingleOrDefaultAsync(ct);
    }

    public static bool CanSeeDeposit(ClaimsPrincipal user, Guid? ownDepositId, Guid depositId) =>
        user.IsInRole("Administrador") || ownDepositId.HasValue && ownDepositId.Value == depositId;

    public static IQueryable<Deposito> VisibleDeposits(IQueryable<Deposito> query, ClaimsPrincipal user, Guid? ownDepositId) =>
        user.IsInRole("Administrador") ? query : ownDepositId.HasValue ? query.Where(x => x.Id == ownDepositId.Value) : query.Where(_ => false);
}
