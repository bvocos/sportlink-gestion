using System.Text.Json.Serialization;
using Api.Features.Caja;
using Api.Features.Auth;
using Api.Features.Auditoria;
using Api.Features.Clientes;
using Api.Features.Cuotas;
using Api.Features.Cotizaciones;
using Api.Features.Dashboard;
using Api.Features.Maestros;
using Api.Features.Rentabilidad;
using Api.Features.Ventas;
using Api.Features.Geografia;
using Api.Features.Gastos;
using Api.Shared.Behaviors;
using Api.Shared.Database;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<Program>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("DolarApi", client =>
{
    client.BaseAddress = new Uri("https://dolarapi.com/");
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Sportlink/1.0");
});
builder.Services.AddSingleton<DolarBlueService>();
builder.Services.AddHttpClient("Georef", client =>
{
    client.BaseAddress = new Uri("https://apis.datos.gob.ar/georef/api/v2.1/");
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Sportlink/1.0");
});
builder.Services.AddSingleton<GeografiaService>();
builder.Services.AddScoped<IPasswordHasher<Usuario>,PasswordHasher<Usuario>>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o=>{o.Cookie.Name="sportlink.session";o.Cookie.HttpOnly=true;o.Cookie.SameSite=SameSiteMode.Lax;o.Events.OnRedirectToLogin=c=>{c.Response.StatusCode=401;return Task.CompletedTask;};o.Events.OnRedirectToAccessDenied=c=>{c.Response.StatusCode=403;return Task.CompletedTask;};});
builder.Services.AddAuthorization(o=>
{
    o.FallbackPolicy=new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
    o.AddPolicy("Administrador",p=>p.RequireRole("Administrador"));
    foreach(var permiso in Permissions.All)
        o.AddPolicy(permiso,p=>p.RequireAssertion(context=>context.User.IsInRole("Administrador")||context.User.HasClaim("permiso",permiso)));
});
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:5173"]).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseExceptionHandler();
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    var requiereCambio = context.User.Identity?.IsAuthenticated == true &&
                         context.User.HasClaim("cambiar_password", "true");
    var rutaPermitida = context.Request.Path.StartsWithSegments("/api/auth/cambiar-password") ||
                        context.Request.Path.StartsWithSegments("/api/auth/logout") ||
                        context.Request.Path.StartsWithSegments("/api/auth/me");
    if (requiereCambio && !rutaPermitida)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Cambio de contraseña requerido",
            detail = "Debés cambiar tu contraseña antes de continuar."
        });
        return;
    }
    await next();
});
app.UseAuthorization();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndpoints();
app.MapAuditoriaEndpoints();
app.MapDashboardEndpoints();
app.MapClienteEndpoints();
app.MapVentaEndpoints();
app.MapCuotaEndpoints();
app.MapCotizacionEndpoints();
app.MapCajaEndpoints();
app.MapRentabilidadEndpoints();
app.MapMaestroEndpoints();
app.MapGeografiaEndpoints();
app.MapGastoEndpoints();
app.MapFallbackToFile("index.html").AllowAnonymous();
await SeedData.InitializeAsync(app.Services);
app.Run();
public partial class Program { }
