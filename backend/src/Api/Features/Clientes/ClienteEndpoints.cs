using Api.Features.Auth;
using Api.Shared.Common;
using Api.Shared.Database;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;

namespace Api.Features.Clientes;

public record CrearClienteCommand(string Nombre, string Apellido, string Telefono, string? Correo,
    string Localidad, string Provincia, string? LocalidadId, string? ProvinciaId, TipoCliente Tipo,
    DateOnly FechaPrimerContacto, string? Observaciones);
public record ClienteDto(Guid Id, Guid SucursalId, string Nombre, string Apellido, string NombreCompleto, string Telefono,
    string? Correo, string Localidad, string Provincia, string? LocalidadId, string? ProvinciaId,
    TipoCliente Tipo, DateOnly FechaPrimerContacto, string? Observaciones);

public sealed class CrearClienteValidator : AbstractValidator<CrearClienteCommand>
{
    public CrearClienteValidator()
    {
        RuleFor(x => x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Apellido).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Telefono).NotEmpty().Matches(@"^\+?[0-9\s()\-]{6,30}$")
            .WithMessage("Ingresá un teléfono válido usando números, espacios, paréntesis, + o -.");
        RuleFor(x => x.Correo).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Correo));
        RuleFor(x => x.Localidad).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Provincia).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ProvinciaId).NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.LocalidadId))
            .WithMessage("La provincia oficial es obligatoria cuando se selecciona una localidad oficial.");
        RuleFor(x => x.LocalidadId).NotEmpty().When(x => !string.IsNullOrWhiteSpace(x.ProvinciaId))
            .WithMessage("La localidad oficial es obligatoria cuando se selecciona una provincia oficial.");
        RuleFor(x => x.Observaciones).MaximumLength(1000);
    }
}

public static class CrearClienteHandler
{
    internal static string FormatPersonName(string value)
    {
        var culture = CultureInfo.GetCultureInfo("es-AR");
        var words = culture.TextInfo.ToTitleCase(value.Trim().ToLower(culture))
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var particles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "de", "del", "la", "las", "los", "y" };
        for (var i = 1; i < words.Length; i++)
            if (particles.Contains(words[i])) words[i] = words[i].ToLower(culture);
        return string.Join(' ', words);
    }

    internal static void Apply(Cliente cliente, CrearClienteCommand request)
    {
        cliente.Nombre = FormatPersonName(request.Nombre);
        cliente.Apellido = FormatPersonName(request.Apellido);
        cliente.Telefono = request.Telefono.Trim();
        cliente.Correo = string.IsNullOrWhiteSpace(request.Correo) ? null : request.Correo.Trim();
        cliente.Localidad = request.Localidad.Trim();
        cliente.Provincia = request.Provincia.Trim();
        cliente.LocalidadId = string.IsNullOrWhiteSpace(request.LocalidadId) ? null : request.LocalidadId.Trim();
        cliente.ProvinciaId = string.IsNullOrWhiteSpace(request.ProvinciaId) ? null : request.ProvinciaId.Trim();
        cliente.Tipo = request.Tipo;
        cliente.FechaPrimerContacto = request.FechaPrimerContacto;
        cliente.Observaciones = request.Observaciones?.Trim();
    }

    internal static ClienteDto ToDto(Cliente cliente) => new(cliente.Id, cliente.SucursalId, cliente.Nombre,
        cliente.Apellido, $"{cliente.Nombre} {cliente.Apellido}", cliente.Telefono, cliente.Correo,
        cliente.Localidad, cliente.Provincia, cliente.LocalidadId, cliente.ProvinciaId, cliente.Tipo,
        cliente.FechaPrimerContacto, cliente.Observaciones);
}

public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clientes").WithTags("Clientes");
        group.MapPost("/", Create).RequirePermiso("clientes", "crear");
        group.MapGet("/", List).RequirePermiso("clientes", "ver");
        group.MapPut("/{id:guid}", Update).RequirePermiso("clientes", "editar");
        group.MapDelete("/{id:guid}", Delete).RequirePermiso("clientes", "eliminar");
    }

    private static async Task<IResult> Create(CrearClienteCommand request, ClaimsPrincipal user, AppDbContext db,
        IValidator<CrearClienteCommand> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation);
        var sucursalId = user.SucursalId();
        if (!sucursalId.HasValue)
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["sucursal"] = ["Seleccioná una sucursal activa."] });
        var cliente = new Cliente { SucursalId = sucursalId.Value };
        CrearClienteHandler.Apply(cliente, request);
        db.Clientes.Add(cliente);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/clientes/{cliente.Id}", CrearClienteHandler.ToDto(cliente));
    }

    private static async Task<PaginatedResponse<ClienteDto>> List(ClaimsPrincipal user, AppDbContext db, int page = 1,
        int pageSize = 20, string? buscar = null, CancellationToken ct = default)
    {
        var query = db.Clientes.AsNoTracking().WhereVisibleParaUsuario(user);
        if (!string.IsNullOrWhiteSpace(buscar))
            query = query.Where(x => (x.Nombre + " " + x.Apellido).Contains(buscar));
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Apellido).ThenBy(x => x.Nombre)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(CrearClienteHandler.ToDto).ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize));
    }

    private static async Task<IResult> Update(Guid id, CrearClienteCommand request, ClaimsPrincipal user,
        AppDbContext db, IValidator<CrearClienteCommand> validator, CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid) return ValidationProblem(validation);
        var cliente = await db.Clientes.WhereVisibleParaUsuario(user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cliente is null) return Results.NotFound();
        CrearClienteHandler.Apply(cliente, request);
        await db.SaveChangesAsync(ct);
        return Results.Ok(CrearClienteHandler.ToDto(cliente));
    }

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var cliente = await db.Clientes.WhereVisibleParaUsuario(user).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (cliente is null) return Results.NotFound();
        if (await db.Ventas.WhereVisibleParaUsuario(user).AnyAsync(x => x.ClienteId == id, ct))
            return Results.Conflict(new { message = "No se puede eliminar un cliente que tiene ventas. Eliminá primero sus ventas." });
        db.Clientes.Remove(cliente);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static IResult ValidationProblem(FluentValidation.Results.ValidationResult validation) =>
        Results.ValidationProblem(validation.Errors.GroupBy(x => x.PropertyName)
            .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
}
