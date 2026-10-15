using System.Security.Claims;

namespace Api.Shared.Common;

public interface ICurrentSucursal
{
    Guid? SucursalId { get; }
}

public sealed class HttpCurrentSucursal(IHttpContextAccessor httpContextAccessor) : ICurrentSucursal
{
    public Guid? SucursalId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue("sucursal_id"), out var sucursalId)
            ? sucursalId
            : null;
}
