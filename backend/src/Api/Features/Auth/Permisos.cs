using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Api.Features.Auth;

[JsonConverter(typeof(PermisosMatrizJsonConverter))]
public sealed class PermisosMatriz
{
    public Dictionary<string, Dictionary<string, bool>> Modulos { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, bool> Montos { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool Puede(string modulo, string accion) =>
        Modulos.TryGetValue(modulo, out var acciones) &&
        acciones.TryGetValue(accion, out var permitido) && permitido;

    public bool PuedeVerMontos(string modulo) => Montos.TryGetValue(modulo, out var permitido) && permitido;

    public IEnumerable<string> PermisosActivos()
    {
        foreach (var (modulo, acciones) in Modulos)
            foreach (var (accion, permitido) in acciones)
                if (permitido) yield return $"{modulo}.{accion}";
        foreach (var (modulo, permitido) in Montos)
            if (permitido) yield return $"{modulo}.verMontos";
    }

    public Dictionary<string, string[]> Validar()
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var (modulo, acciones) in Modulos)
        {
            if (!Permissions.Catalogo.TryGetValue(modulo, out var validas))
            {
                errors[$"permisos.{modulo}"] = ["El módulo no existe en el catálogo de permisos."];
                continue;
            }
            var invalidas = acciones.Keys.Where(x => !validas.Contains(x, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (invalidas.Length > 0)
                errors[$"permisos.{modulo}"] = [$"Acciones inválidas: {string.Join(", ", invalidas)}."];
        }
        foreach (var modulo in Montos.Keys.Where(x => !Permissions.ModulosConMontos.Contains(x, StringComparer.OrdinalIgnoreCase)))
            errors[$"permisos.{modulo}.verMontos"] = ["Este módulo no admite el permiso verMontos."];
        return errors;
    }

    public static PermisosMatriz Todos()
    {
        var result = new PermisosMatriz();
        foreach (var (modulo, acciones) in Permissions.Catalogo)
            result.Modulos[modulo] = acciones.ToDictionary(x => x, _ => true, StringComparer.OrdinalIgnoreCase);
        foreach (var modulo in Permissions.ModulosConMontos) result.Montos[modulo] = true;
        return result;
    }

    public static PermisosMatriz DesdeModulosLegacy(IEnumerable<string> modulos)
    {
        var result = new PermisosMatriz();
        foreach (var modulo in modulos.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!Permissions.Catalogo.TryGetValue(modulo, out var acciones)) continue;
            result.Modulos[modulo] = acciones.ToDictionary(x => x, _ => true, StringComparer.OrdinalIgnoreCase);
            if (Permissions.ModulosConMontos.Contains(modulo, StringComparer.OrdinalIgnoreCase)) result.Montos[modulo] = true;
        }
        return result;
    }

    public static PermisosMatriz DesdeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
            return DesdeModulosLegacy(document.RootElement.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!));
        return JsonSerializer.Deserialize<PermisosMatriz>(json) ?? new();
    }

    public static PermisosMatriz DesdeClaims(ClaimsPrincipal user)
    {
        if (user.IsInRole("Administrador")) return Todos();
        var result = new PermisosMatriz();
        foreach (var value in user.FindAll("permiso").Select(x => x.Value))
        {
            var separator = value.IndexOf('.');
            if (separator <= 0) continue;
            var modulo = value[..separator];
            var accion = value[(separator + 1)..];
            if (accion.Equals("verMontos", StringComparison.OrdinalIgnoreCase)) result.Montos[modulo] = true;
            else
            {
                if (!result.Modulos.TryGetValue(modulo, out var acciones)) result.Modulos[modulo] = acciones = new(StringComparer.OrdinalIgnoreCase);
                acciones[accion] = true;
            }
        }
        return result;
    }
}

public sealed class PermisosMatrizJsonConverter : JsonConverter<PermisosMatriz>
{
    public override PermisosMatriz Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind == JsonValueKind.Array)
            return PermisosMatriz.DesdeModulosLegacy(document.RootElement.EnumerateArray().Select(x => x.GetString() ?? ""));
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("La matriz de permisos debe ser un objeto.");
        var result = new PermisosMatriz();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Name.EndsWith(".verMontos", StringComparison.OrdinalIgnoreCase))
            {
                result.Montos[property.Name[..^".verMontos".Length]] = property.Value.ValueKind == JsonValueKind.True;
                continue;
            }
            if (property.Value.ValueKind != JsonValueKind.Object) throw new JsonException($"El permiso {property.Name} debe contener acciones.");
            result.Modulos[property.Name] = property.Value.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.ValueKind == JsonValueKind.True, StringComparer.OrdinalIgnoreCase);
        }
        return result;
    }

    public override void Write(Utf8JsonWriter writer, PermisosMatriz value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (modulo, acciones) in value.Modulos.OrderBy(x => x.Key))
        {
            writer.WritePropertyName(modulo); writer.WriteStartObject();
            foreach (var (accion, permitido) in acciones) writer.WriteBoolean(accion, permitido);
            writer.WriteEndObject();
        }
        foreach (var (modulo, permitido) in value.Montos.OrderBy(x => x.Key)) writer.WriteBoolean($"{modulo}.verMontos", permitido);
        writer.WriteEndObject();
    }
}

public static class Permissions
{
    public static readonly IReadOnlyDictionary<string, string[]> Catalogo = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["dashboard"] = ["ver"], ["ventas"] = ["ver", "crear", "editar", "eliminar"], ["entregas"] = ["ver"],
        ["presupuestos"] = ["ver", "crear", "editar", "eliminar"], ["clientes"] = ["ver", "crear", "editar", "eliminar"],
        ["cuotas"] = ["ver", "editar", "registrarPago", "anularPago"], ["caja"] = ["ver", "crear", "editar"],
        ["gastos"] = ["ver", "crear", "editar", "eliminar"], ["rentabilidad"] = ["ver"], ["stock"] = ["ver", "crear"],
        ["administracion"] = ["ver", "crear", "editar"]
    };
    public static readonly string[] ModulosConMontos = ["ventas", "cuotas"];
    public static IEnumerable<string> All => Catalogo.Keys;
}

public sealed record PermisoRequirement(string Modulo, string Accion) : IAuthorizationRequirement;

public sealed class PermisoHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermisoRequirement requirement)
    {
        if (context.User.IsInRole("Administrador") || context.User.HasClaim("permiso", $"{requirement.Modulo}.{requirement.Accion}"))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

public sealed class PermisoPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var configured = await base.GetPolicyAsync(policyName);
        if (configured is not null) return configured;
        if (Permissions.Catalogo.ContainsKey(policyName))
            return new AuthorizationPolicyBuilder().AddRequirements(new PermisoRequirement(policyName, "ver")).Build();
        return null;
    }
}

public static class PermisoEndpointExtensions
{
    public static RouteHandlerBuilder RequirePermiso(this RouteHandlerBuilder builder, string modulo, string accion)
    {
        if (!Permissions.Catalogo.TryGetValue(modulo, out var acciones) || !acciones.Contains(accion, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Permiso inválido: {modulo}.{accion}");
        return builder.RequireAuthorization(policy => policy.AddRequirements(new PermisoRequirement(modulo, accion)));
    }
}
