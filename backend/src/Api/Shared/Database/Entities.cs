using Api.Shared.Common;

namespace Api.Shared.Database;

public enum TipoCliente { Club, Particular, Empresa, Constructor, Revendedor, Otro }
public enum FormaPago { Cuotas, Contado, Transferencia, Cheque, Otros }
public enum EstadoVenta { Confirmada, Futura, Entregada, Cancelada }
public enum EstadoCuota { Pendiente, Pagada, PagadaParcial, Vencida }
public enum TipoMovimiento { Ingreso, Retiro }
public sealed class Usuario : AuditableEntity { public string Nombre { get; set; }=""; public string NombreUsuario { get; set; }=""; public string PasswordHash { get; set; }=""; public string Rol { get; set; }="Usuario"; public string PermisosJson { get; set; }="[]"; public bool Activo { get; set; }=true; public bool DebeCambiarPassword { get; set; }=true; public int IntentosFallidos { get; set; } public DateTimeOffset? BloqueadoHasta { get; set; } }
public sealed class RegistroAuditoria
{
    public long Id { get; set; }
    public DateTimeOffset FechaHora { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Usuario { get; set; } = "sistema";
    public string Modulo { get; set; } = "";
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
    public string DetalleJson { get; set; } = "{}";
}

public sealed class Cliente : AuditableEntity
{
    public string Nombre { get; set; } = ""; public string Apellido { get; set; } = "";
    public string Telefono { get; set; } = ""; public string? Correo { get; set; }
    public string Localidad { get; set; } = ""; public string Provincia { get; set; } = "";
    public string? LocalidadId { get; set; } public string? ProvinciaId { get; set; }
    public TipoCliente Tipo { get; set; } public DateOnly FechaPrimerContacto { get; set; } public string? Observaciones { get; set; }
}
public sealed class TipoCesped : AuditableEntity { public string Nombre { get; set; } = ""; public string? Descripcion { get; set; } public decimal PrecioVentaM2 { get; set; } public decimal CostoM2 { get; set; } public bool Activo { get; set; } = true; }
public sealed class AlicuotaIva : AuditableEntity { public string Nombre { get; set; } = ""; public decimal Porcentaje { get; set; } }
public sealed class Configuracion : AuditableEntity { public string Clave { get; set; } = ""; public decimal ValorDecimal { get; set; } }
public sealed class Venta : AuditableEntity
{
    public Guid ClienteId { get; set; } public Cliente Cliente { get; set; } = null!;
    public Guid TipoCespedId { get; set; } public TipoCesped TipoCesped { get; set; } = null!;
    public Guid AlicuotaIvaId { get; set; } public AlicuotaIva AlicuotaIva { get; set; } = null!;
    public DateOnly FechaVenta { get; set; } public DateOnly? FechaEntregaEstimada { get; set; }
    public decimal CantidadM2 { get; set; } public decimal PrecioUnitario { get; set; } public decimal PrecioTotal { get; set; }
    public decimal MontoEntrega { get; set; }
    public decimal CostoCompraUnitario { get; set; } public decimal CostoCompraTotal { get; set; }
    public decimal CostoEnvio { get; set; } public decimal OtrosCostos { get; set; } public decimal Iva { get; set; }
    public decimal GananciaBruta { get; set; } public decimal GananciaNeta { get; set; } public decimal Margen { get; set; }
    public FormaPago FormaPago { get; set; } public int? CantidadCuotas { get; set; } public EstadoVenta Estado { get; set; }
    public string? Observaciones { get; set; } public List<Cuota> Cuotas { get; set; } = [];
}
public sealed class Cuota : AuditableEntity
{
    public Guid VentaId { get; set; } public Venta Venta { get; set; } = null!; public Guid ClienteId { get; set; }
    public int Numero { get; set; } public DateOnly FechaVencimiento { get; set; } public DateOnly? FechaPago { get; set; }
    public decimal ImportePactado { get; set; } public decimal ImportePagado { get; set; } public string? MedioPago { get; set; }
    public EstadoCuota Estado { get; set; }
}
public sealed class MovimientoCaja : AuditableEntity
{
    public TipoMovimiento Tipo { get; set; } public DateTimeOffset Fecha { get; set; } public decimal Monto { get; set; }
    public string Concepto { get; set; } = ""; public string Usuario { get; set; } = "sistema"; public Guid? VentaId { get; set; } public Guid? CuotaId { get; set; }
}
