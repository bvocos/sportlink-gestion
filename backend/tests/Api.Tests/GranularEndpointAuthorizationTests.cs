using System.Security.Claims;
using Api.Features.Auth;
using Api.Features.Sucursales;
using Api.Shared.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class GranularEndpointAuthorizationTests
{
    public static TheoryData<string, string> AdminOnlySucursalEndpoints => new()
    {
        { "GET", "/api/sucursales/" },
        { "POST", "/api/sucursales/" },
        { "PUT", "/api/sucursales/{id:guid}" },
        { "PATCH", "/api/sucursales/{id:guid}/estado" },
        { "DELETE", "/api/sucursales/{id:guid}" },
        { "PUT", "/api/sucursales/{id:guid}/logo" },
        { "DELETE", "/api/sucursales/{id:guid}/logo" },
        { "GET", "/api/sucursales/depositos" },
        { "POST", "/api/sucursales/depositos" },
        { "PUT", "/api/sucursales/depositos/{id:guid}" },
        { "PATCH", "/api/sucursales/depositos/{id:guid}/estado" },
        { "DELETE", "/api/sucursales/depositos/{id:guid}" }
    };

    [Fact]
    public async Task VentasVer_DoesNotAuthorize_Delete()
    {
        var result = await Execute("ventas", "eliminar", "ventas.ver");
        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.False(result.EndpointExecuted);
    }

    [Fact]
    public async Task CuotasVer_CanList_ButCannotRegisterPayment()
    {
        var list = await Execute("cuotas", "ver", "cuotas.ver");
        var pay = await Execute("cuotas", "registrarPago", "cuotas.ver");
        Assert.True(list.EndpointExecuted);
        Assert.Equal(StatusCodes.Status403Forbidden, pay.StatusCode);
        Assert.False(pay.EndpointExecuted);
    }

    [Fact]
    public async Task CajaCrear_CanCreate_ButCannotEditObservation()
    {
        var create = await Execute("caja", "crear", "caja.crear");
        var edit = await Execute("caja", "editar", "caja.crear");
        Assert.True(create.EndpointExecuted);
        Assert.Equal(StatusCodes.Status403Forbidden, edit.StatusCode);
        Assert.False(edit.EndpointExecuted);
    }

    [Fact]
    public async Task StockCrear_CannotAdjust_WithoutStockEditPermission()
    {
        var create = await Execute("stock", "crear", "stock.crear");
        var adjust = await Execute("stock", "editar", "stock.crear");
        var allowedAdjust = await Execute("stock", "editar", "stock.editar");

        Assert.True(create.EndpointExecuted);
        Assert.Equal(StatusCodes.Status403Forbidden, adjust.StatusCode);
        Assert.False(adjust.EndpointExecuted);
        Assert.True(allowedAdjust.EndpointExecuted);
    }

    [Theory]
    [MemberData(nameof(AdminOnlySucursalEndpoints))]
    public async Task SucursalesYDepositos_RequireAdministrator_EvenWithFullAdministrationPermissions(
        string method, string route)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddScoped<AppDbContext>(_ => null!);
        await using var app = builder.Build();
        app.MapSucursalEndpoints();
        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>().Single(x => x.RoutePattern.RawText == route &&
                x.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Contains(method));
        Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            authorization => authorization.Policy == "Administrador");

        var permissionsUser = await ExecuteAdministratorPolicy(false,
            "administracion.ver", "administracion.crear", "administracion.editar");
        var administrator = await ExecuteAdministratorPolicy(true);

        Assert.Equal(StatusCodes.Status403Forbidden, permissionsUser.StatusCode);
        Assert.False(permissionsUser.EndpointExecuted);
        Assert.True(administrator.EndpointExecuted, $"Administrador no accedió a {method} {route}");
    }

    private static async Task<(int StatusCode, bool EndpointExecuted)> Execute(string modulo, string accion, params string[] claims)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationHandler, PermisoHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, StatusResultHandler>();
        await using var provider = services.BuildServiceProvider();

        var executed = false;
        RequestDelegate next = _ => { executed = true; return Task.CompletedTask; };
        var policy = new AuthorizationPolicyBuilder().AddRequirements(new PermisoRequirement(modulo, accion)).Build();
        var middleware = new AuthorizationMiddleware(next, new StaticPolicyProvider(policy));
        var context = new DefaultHttpContext { RequestServices = provider };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims.Select(x => new Claim("permiso", x)), "Test"));
        await middleware.Invoke(context);
        return (context.Response.StatusCode, executed);
    }

    private static async Task<(int StatusCode, bool EndpointExecuted)> ExecuteAdministratorPolicy(
        bool isAdministrator, params string[] claims)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, StatusResultHandler>();
        await using var provider = services.BuildServiceProvider();

        var executed = false;
        RequestDelegate next = _ => { executed = true; return Task.CompletedTask; };
        var policy = new AuthorizationPolicyBuilder().RequireRole("Administrador").Build();
        var middleware = new AuthorizationMiddleware(next, new StaticPolicyProvider(policy));
        var context = new DefaultHttpContext { RequestServices = provider };
        var identityClaims = claims.Select(x => new Claim("permiso", x)).ToList();
        if (isAdministrator) identityClaims.Add(new Claim(ClaimTypes.Role, "Administrador"));
        context.User = new ClaimsPrincipal(new ClaimsIdentity(identityClaims, "Test"));
        await middleware.Invoke(context);
        return (context.Response.StatusCode, executed);
    }

    private sealed class StaticPolicyProvider(AuthorizationPolicy policy) : IAuthorizationPolicyProvider
    {
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) => Task.FromResult<AuthorizationPolicy?>(policy);
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(policy);
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => Task.FromResult<AuthorizationPolicy?>(policy);
    }

    private sealed class StatusResultHandler : IAuthorizationMiddlewareResultHandler
    {
        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            if (authorizeResult.Succeeded) await next(context);
            else context.Response.StatusCode = authorizeResult.Forbidden ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized;
        }
    }
}
