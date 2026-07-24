using Api.Shared.Common;
using Api.Shared.Database;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Clientes;

public record CrearClienteCommand(string Nombre, string Apellido, string Telefono, string? Correo,
    string Localidad, string Provincia, string? LocalidadId, string? ProvinciaId, TipoCliente Tipo,
    DateOnly FechaPrimerContacto, string? Observaciones) : IRequest<ClienteDto>;
public record ClienteDto(Guid Id, string Nombre, string Apellido, string NombreCompleto, string Telefono,
    string? Correo, string Localidad, string Provincia, string? LocalidadId, string? ProvinciaId,
    TipoCliente Tipo, DateOnly FechaPrimerContacto, string? Observaciones);
public sealed class CrearClienteValidator : AbstractValidator<CrearClienteCommand>
{
    public CrearClienteValidator()
    {
        RuleFor(x=>x.Nombre).NotEmpty().MaximumLength(100);
        RuleFor(x=>x.Apellido).NotEmpty().MaximumLength(100);
        RuleFor(x=>x.Telefono).NotEmpty().Matches(@"^\+?[0-9\s()\-]{6,30}$").WithMessage("Ingresá un teléfono válido usando números, espacios, paréntesis, + o -.");
        RuleFor(x=>x.Correo).MaximumLength(200).EmailAddress().When(x=>!string.IsNullOrWhiteSpace(x.Correo));
        RuleFor(x=>x.Localidad).NotEmpty().MaximumLength(100);
        RuleFor(x=>x.Provincia).NotEmpty().MaximumLength(100);
        RuleFor(x=>x.ProvinciaId).NotEmpty().When(x=>!string.IsNullOrWhiteSpace(x.LocalidadId))
            .WithMessage("La provincia oficial es obligatoria cuando se selecciona una localidad oficial.");
        RuleFor(x=>x.LocalidadId).NotEmpty().When(x=>!string.IsNullOrWhiteSpace(x.ProvinciaId))
            .WithMessage("La localidad oficial es obligatoria cuando se selecciona una provincia oficial.");
        RuleFor(x=>x.Observaciones).MaximumLength(1000);
    }
}
public sealed class CrearClienteHandler(AppDbContext db) : IRequestHandler<CrearClienteCommand, ClienteDto>
{
    public async Task<ClienteDto> Handle(CrearClienteCommand r, CancellationToken ct)
    {
        var c=new Cliente(); Apply(c,r); db.Add(c); await db.SaveChangesAsync(ct); return ToDto(c);
    }
    internal static void Apply(Cliente c,CrearClienteCommand r){c.Nombre=r.Nombre.Trim();c.Apellido=r.Apellido.Trim();c.Telefono=r.Telefono.Trim();c.Correo=string.IsNullOrWhiteSpace(r.Correo)?null:r.Correo.Trim();c.Localidad=r.Localidad.Trim();c.Provincia=r.Provincia.Trim();c.LocalidadId=string.IsNullOrWhiteSpace(r.LocalidadId)?null:r.LocalidadId.Trim();c.ProvinciaId=string.IsNullOrWhiteSpace(r.ProvinciaId)?null:r.ProvinciaId.Trim();c.Tipo=r.Tipo;c.FechaPrimerContacto=r.FechaPrimerContacto;c.Observaciones=r.Observaciones?.Trim();}
    internal static ClienteDto ToDto(Cliente c)=>new(c.Id,c.Nombre,c.Apellido,$"{c.Nombre} {c.Apellido}",c.Telefono,c.Correo,c.Localidad,c.Provincia,c.LocalidadId,c.ProvinciaId,c.Tipo,c.FechaPrimerContacto,c.Observaciones);
}
public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this IEndpointRouteBuilder app){var g=app.MapGroup("/api/clientes").WithTags("Clientes").RequireAuthorization("clientes");g.MapPost("/",async(CrearClienteCommand c,ISender s,CancellationToken ct)=>Results.Created("/api/clientes",await s.Send(c,ct)));g.MapGet("/",List);g.MapPut("/{id:guid}",Update);g.MapDelete("/{id:guid}",Delete);}
    static async Task<PaginatedResponse<ClienteDto>> List(AppDbContext db,int page=1,int pageSize=20,string? buscar=null,CancellationToken ct=default){var q=db.Clientes.AsNoTracking();if(!string.IsNullOrWhiteSpace(buscar))q=q.Where(x=>(x.Nombre+" "+x.Apellido).Contains(buscar));var total=await q.CountAsync(ct);var rows=await q.OrderBy(x=>x.Apellido).Skip((page-1)*pageSize).Take(pageSize).ToListAsync(ct);return new(rows.Select(CrearClienteHandler.ToDto).ToList(),page,pageSize,total,(int)Math.Ceiling(total/(double)pageSize));}
    static async Task<IResult> Update(Guid id,CrearClienteCommand request,AppDbContext db,IValidator<CrearClienteCommand> validator,CancellationToken ct){var validation=await validator.ValidateAsync(request,ct);if(!validation.IsValid)return Results.ValidationProblem(validation.Errors.GroupBy(x=>x.PropertyName).ToDictionary(x=>x.Key,x=>x.Select(e=>e.ErrorMessage).ToArray()));var cliente=await db.Clientes.FindAsync([id],ct);if(cliente is null)return Results.NotFound();CrearClienteHandler.Apply(cliente,request);await db.SaveChangesAsync(ct);return Results.Ok(CrearClienteHandler.ToDto(cliente));}
    static async Task<IResult> Delete(Guid id,AppDbContext db,CancellationToken ct){var cliente=await db.Clientes.FindAsync([id],ct);if(cliente is null)return Results.NotFound();if(await db.Ventas.AnyAsync(x=>x.ClienteId==id,ct))return Results.Conflict(new{message="No se puede eliminar un cliente que tiene ventas. Eliminá primero sus ventas."});db.Clientes.Remove(cliente);await db.SaveChangesAsync(ct);return Results.NoContent();}
}
