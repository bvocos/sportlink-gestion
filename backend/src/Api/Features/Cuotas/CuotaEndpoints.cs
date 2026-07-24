using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace Api.Features.Cuotas;
public record PagoRequest(decimal Importe,string MedioPago,DateOnly FechaPago);
public static class CuotaEndpoints
{
 internal static string? ValidatePayment(decimal importe, decimal saldoPendiente)
 {
  if(importe<=0)return "El importe debe ser mayor a cero.";
  if(importe>saldoPendiente)return $"El pago no puede superar el saldo pendiente de {saldoPendiente:C}.";
  return null;
 }
 public static void MapCuotaEndpoints(this IEndpointRouteBuilder app){var g=app.MapGroup("/api/cuotas").WithTags("Cuotas").RequireAuthorization("cuotas");g.MapGet("/pendientes",(AppDbContext db,CancellationToken ct)=>List(db,false,ct));g.MapGet("/abonadas",(AppDbContext db,CancellationToken ct)=>List(db,true,ct));g.MapPost("/{id:guid}/pagos",RegistrarPago);}
 static async Task<IResult> List(AppDbContext db,bool abonadas,CancellationToken ct){var q=db.Cuotas.AsNoTracking().Include(x=>x.Venta).ThenInclude(x=>x.Cliente).Include(x=>x.Venta).ThenInclude(x=>x.TipoCesped).Where(x=>abonadas?x.ImportePagado>=x.ImportePactado:x.ImportePagado<x.ImportePactado);var rows=await q.OrderByDescending(x=>abonadas?x.FechaPago:null).ThenBy(x=>x.FechaVencimiento).Select(x=>new{x.Id,x.VentaId,cliente=x.Venta.Cliente.Nombre+" "+x.Venta.Cliente.Apellido,fechaVenta=x.Venta.FechaVenta,tipoCesped=x.Venta.TipoCesped.Nombre,totalVenta=x.Venta.PrecioTotal,x.Numero,x.FechaVencimiento,x.FechaPago,fechaImpacto=x.UpdatedAt??x.CreatedAt,x.MedioPago,x.ImportePactado,x.ImportePagado,estado=abonadas?EstadoCuota.Pagada:x.FechaVencimiento<DateOnly.FromDateTime(DateTime.Today)&&x.Estado!=EstadoCuota.Pagada?EstadoCuota.Vencida:x.Estado}).ToListAsync(ct);return Results.Ok(rows);}
 static async Task<IResult> RegistrarPago(Guid id,PagoRequest r,ClaimsPrincipal currentUser,AppDbContext db,CancellationToken ct){var medioPago=r.MedioPago?.Trim();if(string.IsNullOrWhiteSpace(medioPago)||medioPago.Length>100)return Results.ValidationProblem(new Dictionary<string,string[]>{{"medioPago",["Seleccioná un medio de pago válido."]}});var c=await db.Cuotas.FindAsync([id],ct);if(c is null)return Results.NotFound();var saldoPendiente=c.ImportePactado-c.ImportePagado;var paymentError=ValidatePayment(r.Importe,saldoPendiente);if(paymentError is not null)return Results.ValidationProblem(new Dictionary<string,string[]>{{"importe",[paymentError]}});c.ImportePagado+=r.Importe;c.FechaPago=r.FechaPago;c.MedioPago=medioPago;c.Estado=c.ImportePagado>=c.ImportePactado?EstadoCuota.Pagada:EstadoCuota.PagadaParcial;db.MovimientosCaja.Add(new MovimientoCaja{Tipo=TipoMovimiento.Ingreso,Fecha=DateTimeOffset.UtcNow,Monto=r.Importe,Concepto=$"Pago cuota {c.Numero}",Usuario=currentUser.Identity?.Name??currentUser.FindFirstValue("usuario")??"sistema",CuotaId=c.Id,VentaId=c.VentaId});await db.SaveChangesAsync(ct);return Results.Ok(new{c.Id,c.ImportePagado,c.Estado});}
}
