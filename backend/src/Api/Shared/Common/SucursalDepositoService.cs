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

    public static IQueryable<Deposito> VisibleDeposits(IQueryable<Deposito> query, ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sucursal_id"), out var sucursalId)
            ? query.Where(x => x.SucursalId == sucursalId)
            : query.Where(_ => false);

    public static Task<bool> CanUseDeposit(ClaimsPrincipal user, AppDbContext db, Guid depositId, CancellationToken ct) =>
        VisibleDeposits(db.Depositos.AsNoTracking(), user).AnyAsync(x => x.Id == depositId && x.Activo, ct);

    public static IQueryable<Deposito> VisibleDeposits(IQueryable<Deposito> query, ClaimsPrincipal user, Guid? ownDepositId) =>
        VisibleDeposits(query, user);

    public static IQueryable<TipoCesped> VisibleProducts(IQueryable<TipoCesped> query, ClaimsPrincipal user,
        IQueryable<Deposito> deposits)
    {
        var visibleDepositIds = VisibleDeposits(deposits, user).Select(x => x.Id);
        return query.Where(x => visibleDepositIds.Contains(x.DepositoId) ||
            x.Depositos.Any(d => visibleDepositIds.Contains(d.DepositoId)));
    }
}
