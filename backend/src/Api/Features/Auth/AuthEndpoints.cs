using System.Security.Claims;
using System.Text.Json;
using Api.Shared.Database;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Auth;

public record LoginRequest(string Usuario, string Password);
public record CambiarPasswordRequest(string PasswordActual, string PasswordNueva, string Confirmacion);
public record UsuarioRequest(string Nombre, string NombreUsuario, string? Password, string Rol, string[] Permisos, bool Activo);

public static class AuthEndpoints
{
    private const int MaxIntentos = 5;
    private static readonly TimeSpan DuracionBloqueo = TimeSpan.FromMinutes(15);

    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");
        auth.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("login");
        auth.MapPost("/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync();
            return Results.NoContent();
        });
        auth.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(Current(user)));
        auth.MapPost("/cambiar-password", CambiarPassword);

        var users = app.MapGroup("/api/usuarios").WithTags("Usuarios").RequireAuthorization("Administrador");
        users.MapGet("/", List);
        users.MapPost("/", Create);
        users.MapPut("/{id:guid}", Update);
        users.MapDelete("/{id:guid}", Delete);
    }

    private static object Current(ClaimsPrincipal user) => new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        nombre = user.Identity?.Name,
        usuario = user.FindFirstValue("usuario"),
        rol = user.FindFirstValue(ClaimTypes.Role),
        permisos = user.FindAll("permiso").Select(x => x.Value),
        debeCambiarPassword = user.HasClaim("cambiar_password", "true")
    };

    private static ClaimsPrincipal BuildPrincipal(Usuario user)
    {
        var permisos = user.Rol == "Administrador" ? Permissions.All :
            JsonSerializer.Deserialize<string[]>(user.PermisosJson) ?? [];
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Nombre),
            new("usuario", user.NombreUsuario),
            new(ClaimTypes.Role, user.Rol),
            new("cambiar_password", user.DebeCambiarPassword ? "true" : "false")
        };
        claims.AddRange(permisos.Select(x => new Claim("permiso", x)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    private static Task SignIn(HttpContext context, ClaimsPrincipal principal) =>
        context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12) });

    private static async Task<IResult> Login(LoginRequest request, AppDbContext db,
        IPasswordHasher<Usuario> hasher, HttpContext context, CancellationToken ct)
    {
        var name = request.Usuario.Trim();
        var user = await db.Usuarios.SingleOrDefaultAsync(x => x.NombreUsuario == name && x.Activo, ct);
        if (user is null)
        {
            await Task.Delay(200, ct);
            return Results.Problem(statusCode: 401, title: "Usuario o contraseña incorrectos");
        }
        if (user.BloqueadoHasta > DateTimeOffset.UtcNow)
            return Results.Problem(statusCode: 429, title: "Cuenta temporalmente bloqueada",
                detail: "Esperá 15 minutos antes de volver a intentar.");

        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            user.IntentosFallidos++;
            if (user.IntentosFallidos >= MaxIntentos)
            {
                user.IntentosFallidos = 0;
                user.BloqueadoHasta = DateTimeOffset.UtcNow.Add(DuracionBloqueo);
            }
            await db.SaveChangesAsync(ct);
            return Results.Problem(statusCode: 401, title: "Usuario o contraseña incorrectos");
        }

        user.IntentosFallidos = 0;
        user.BloqueadoHasta = null;
        await db.SaveChangesAsync(ct);
        var principal = BuildPrincipal(user);
        await SignIn(context, principal);
        return Results.Ok(Current(principal));
    }

    private static async Task<IResult> CambiarPassword(CambiarPasswordRequest request, ClaimsPrincipal current,
        AppDbContext db, IPasswordHasher<Usuario> hasher, HttpContext context, CancellationToken ct)
    {
        if (!Guid.TryParse(current.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
            return Results.Unauthorized();
        var user = await db.Usuarios.FindAsync([id], ct);
        if (user is null || !user.Activo) return Results.Unauthorized();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.PasswordActual) == PasswordVerificationResult.Failed)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["passwordActual"] = ["La contraseña actual no es correcta."] });
        if (request.PasswordNueva.Length < 8)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["passwordNueva"] = ["La nueva contraseña debe tener al menos 8 caracteres."] });
        if (request.PasswordNueva != request.Confirmacion)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["confirmacion"] = ["Las contraseñas no coinciden."] });
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, request.PasswordNueva) != PasswordVerificationResult.Failed)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["passwordNueva"] = ["La nueva contraseña debe ser diferente de la actual."] });

        user.PasswordHash = hasher.HashPassword(user, request.PasswordNueva);
        user.DebeCambiarPassword = false;
        user.IntentosFallidos = 0;
        user.BloqueadoHasta = null;
        await db.SaveChangesAsync(ct);
        var principal = BuildPrincipal(user);
        await SignIn(context, principal);
        return Results.Ok(Current(principal));
    }

    private static async Task<IResult> List(AppDbContext db, CancellationToken ct)
    {
        var users = await db.Usuarios.AsNoTracking().OrderBy(x => x.Nombre).ToListAsync(ct);
        return Results.Ok(users.Select(x => new
        {
            x.Id, x.Nombre, x.NombreUsuario, x.Rol,
            permisos = JsonSerializer.Deserialize<string[]>(x.PermisosJson) ?? [],
            x.Activo, x.DebeCambiarPassword
        }));
    }

    private static async Task<IResult> Create(UsuarioRequest request, AppDbContext db,
        IPasswordHasher<Usuario> hasher, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.NombreUsuario))
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["usuario"] = ["Nombre y usuario son obligatorios."] });
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["password"] = ["La contraseña debe tener al menos 8 caracteres."] });
        if (await db.Usuarios.AnyAsync(x => x.NombreUsuario == request.NombreUsuario.Trim(), ct))
            return Results.Conflict(new { message = "El usuario ya existe." });
        var user = new Usuario
        {
            Nombre = request.Nombre.Trim(), NombreUsuario = request.NombreUsuario.Trim(),
            Rol = request.Rol, PermisosJson = JsonSerializer.Serialize(request.Permisos),
            Activo = request.Activo, DebeCambiarPassword = true
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Add(user);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/usuarios/{user.Id}", new { user.Id });
    }

    private static async Task<IResult> Update(Guid id, UsuarioRequest request, AppDbContext db,
        IPasswordHasher<Usuario> hasher, CancellationToken ct)
    {
        var user = await db.Usuarios.FindAsync([id], ct);
        if (user is null) return Results.NotFound();
        if (await db.Usuarios.AnyAsync(x => x.Id != id && x.NombreUsuario == request.NombreUsuario.Trim(), ct))
            return Results.Conflict(new { message = "El usuario ya existe." });
        user.Nombre = request.Nombre.Trim();
        user.NombreUsuario = request.NombreUsuario.Trim();
        user.Rol = request.Rol;
        user.PermisosJson = JsonSerializer.Serialize(request.Permisos);
        user.Activo = request.Activo;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            if (request.Password.Length < 8)
                return Results.BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            user.DebeCambiarPassword = true;
        }
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal current, AppDbContext db, CancellationToken ct)
    {
        if (current.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
            return Results.Conflict(new { message = "No podés eliminar tu propio usuario." });
        var user = await db.Usuarios.FindAsync([id], ct);
        if (user is null) return Results.NotFound();
        db.Remove(user);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}

public static class Permissions
{
    public static readonly string[] All =
        ["dashboard", "ventas", "entregas", "clientes", "cuotas", "caja", "rentabilidad", "administracion", "usuarios"];
}
