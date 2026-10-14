using System.Security.Claims;

namespace Api.Shared.Common;

public static class SucursalQueryExtensions
{
    public static IQueryable<T> WhereVisibleParaUsuario<T>(this IQueryable<T> query,
        ClaimsPrincipal user) where T : ISucursalScoped
    {
        return Guid.TryParse(user.FindFirstValue("sucursal_id"), out var sucursalId)
            ? query.Where(x => x.SucursalId == sucursalId)
            : query.Where(_ => false);
    }

    public static IQueryable<T> WhereVisibleParaUsuario<T>(this IQueryable<T> query,
        ClaimsPrincipal user, Guid? sucursalSeleccionadaId) where T : ISucursalScoped
    {
        var visible = query.WhereVisibleParaUsuario(user);
        return visible;
    }

    public static Guid? SucursalId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sucursal_id"), out var sucursalId) ? sucursalId : null;
}
