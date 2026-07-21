using Api.Shared.Common;
using Api.Shared.Database;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.Features.Clientes;

public record CrearClienteCommand(string Nombre, string Apellido, string Telefono, string? Correo, string Localidad, string Provincia, TipoCliente Tipo, DateOnly FechaPrimerContacto, string? Observaciones) : IRequest<ClienteDto>;
public record ClienteDto(Guid Id, string NombreCompleto, string Telefono, string? Correo, string Localidad, string Provincia, TipoCliente Tipo);
public sealed class CrearClienteValidator : AbstractValidator<CrearClienteCommand> { public CrearClienteValidator() { RuleFor(x=>x.Nombre).NotEmpty().MaximumLength(100); RuleFor(x=>x.Apellido).NotEmpty().MaximumLength(100); RuleFor(x=>x.Telefono).NotEmpty(); RuleFor(x=>x.Correo).EmailAddress().When(x=>!string.IsNullOrWhiteSpace(x.Correo)); } }
public sealed class CrearClienteHandler(AppDbContext db) : IRequestHandler<CrearClienteCommand, ClienteDto>
{
    public async Task<ClienteDto> Handle(CrearClienteCommand r, CancellationToken ct) { var c=new Cliente { Nombre=r.Nombre.Trim(), Apellido=r.Apellido.Trim(), Telefono=r.Telefono, Correo=r.Correo, Localidad=r.Localidad, Provincia=r.Provincia, Tipo=r.Tipo, FechaPrimerContacto=r.FechaPrimerContacto, Observaciones=r.Observaciones }; db.Add(c); await db.SaveChangesAsync(ct); return new(c.Id,$"{c.Nombre} {c.Apellido}",c.Telefono,c.Correo,c.Localidad,c.Provincia,c.Tipo); }
}
public static class ClienteEndpoints
{
    public static void MapClienteEndpoints(this IEndpointRouteBuilder app) { var g=app.MapGroup("/api/clientes").WithTags("Clientes"); g.MapPost("/", async(CrearClienteCommand c,ISender s,CancellationToken ct)=>Results.Created("/api/clientes",await s.Send(c,ct))); g.MapGet("/", List); g.MapDelete("/{id:guid}", Delete); }
    static async Task<PaginatedResponse<ClienteDto>> List(AppDbContext db,int page=1,int pageSize=20,string? buscar=null,CancellationToken ct=default) { var q=db.Clientes.AsNoTracking(); if(!string.IsNullOrWhiteSpace(buscar)) q=q.Where(x=>(x.Nombre+" "+x.Apellido).Contains(buscar)); var total=await q.CountAsync(ct); var items=await q.OrderBy(x=>x.Apellido).Skip((page-1)*pageSize).Take(pageSize).Select(x=>new ClienteDto(x.Id,x.Nombre+" "+x.Apellido,x.Telefono,x.Correo,x.Localidad,x.Provincia,x.Tipo)).ToListAsync(ct); return new(items,page,pageSize,total,(int)Math.Ceiling(total/(double)pageSize)); }
    static async Task<IResult> Delete(Guid id,AppDbContext db,CancellationToken ct) { var cliente=await db.Clientes.FindAsync([id],ct); if(cliente is null)return Results.NotFound(); if(await db.Ventas.AnyAsync(x=>x.ClienteId==id,ct))return Results.Conflict(new{message="No se puede eliminar un cliente que tiene ventas. Eliminá primero sus ventas."}); db.Clientes.Remove(cliente);await db.SaveChangesAsync(ct);return Results.NoContent(); }
}
