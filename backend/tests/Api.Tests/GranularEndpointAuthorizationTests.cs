using System.Security.Claims;
using Api.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class GranularEndpointAuthorizationTests
{
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
