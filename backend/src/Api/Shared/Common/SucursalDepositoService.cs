using System.Security.Claims;
using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Shared.Common;

public static class SucursalDepositoService
{
    public static async Task<Guid?> GetOwnDepositId(ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        if (!Guid.TryParse(user.FindFirstValue("sucursal_id"), out var sucursalId)) return null;
        return await db.Sucursales.AsNoTracking().Where(x => x.Id == sucursalId && x.Activo)
            .Select(x => (Guid?)x.DepositoPropioId)
            .SingleOrDefaultAsync(ct);
    }

    public static bool CanSeeDeposit(ClaimsPrincipal user, Guid? ownDepositId, Guid depositId) =>
        ownDepositId.HasValue && ownDepositId.Value == depositId;

    public static IQueryable<Deposito> VisibleDeposits(IQueryable<Deposito> query, ClaimsPrincipal user, Guid? ownDepositId) =>
        ownDepositId.HasValue ? query.Where(x => x.Id == ownDepositId.Value) : query.Where(_ => false);
}
