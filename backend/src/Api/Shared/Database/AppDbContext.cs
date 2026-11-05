using Api.Shared.Common;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Api.Features.Auth;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

namespace Api.Shared.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null,
    ICurrentSucursal? currentSucursal = null) : DbContext(options), IDataProtectionKeyContext
{
    private Guid? CurrentSucursalId => currentSucursal?.SucursalId;
    public DbSet<Cliente> Clientes => Set<Cliente>(); public DbSet<Venta> Ventas => Set<Venta>(); public DbSet<Cuota> Cuotas => Set<Cuota>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>(); public DbSet<TipoCesped> TiposCesped => Set<TipoCesped>();
    public DbSet<AlicuotaIva> AlicuotasIva => Set<AlicuotaIva>(); public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<Deposito> Depositos => Set<Deposito>();
    public DbSet<TipoCespedDeposito> TiposCespedDepositos => Set<TipoCespedDeposito>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<SucursalLogo> SucursalesLogos => Set<SucursalLogo>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
    public DbSet<LoteStock> LotesStock => Set<LoteStock>();
    public DbSet<Rollo> Rollos => Set<Rollo>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();
    public DbSet<Gasto> Gastos => Set<Gasto>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>(); public DbSet<PresupuestoLinea> PresupuestoLineas => Set<PresupuestoLinea>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Cliente>().HasQueryFilter(x => CurrentSucursalId != null && x.SucursalId == CurrentSucursalId);
        b.Entity<Venta>().HasQueryFilter(x => CurrentSucursalId != null && x.SucursalId == CurrentSucursalId);
        b.Entity<Cuota>().HasQueryFilter(x => CurrentSucursalId != null && x.SucursalId == CurrentSucursalId);
        b.Entity<MovimientoCaja>().HasQueryFilter(x => CurrentSucursalId != null && x.SucursalId == CurrentSucursalId);
        b.Entity<Presupuesto>().HasQueryFilter(x => CurrentSucursalId != null && x.SucursalId == CurrentSucursalId);
        b.Entity<PresupuestoLinea>().HasQueryFilter(x => CurrentSucursalId != null && x.Presupuesto.SucursalId == CurrentSucursalId);
        b.Entity<Gasto>().HasQueryFilter(x => CurrentSucursalId != null && x.SucursalId == CurrentSucursalId);
        foreach (var t in new[] { typeof(Venta), typeof(Cuota), typeof(MovimientoCaja), typeof(MovimientoStock), typeof(LoteStock), typeof(Rollo), typeof(Gasto), typeof(TipoCesped), typeof(AlicuotaIva), typeof(Configuracion), typeof(Presupuesto), typeof(PresupuestoLinea) })
            foreach (var p in b.Entity(t).Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal))) p.SetColumnType("decimal(18,2)");
        b.Entity<Cliente>().Property(x => x.Tipo).HasConversion<string>(); b.Entity<Venta>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Venta>().Property(x => x.FormaPago).HasConversion<string>(); b.Entity<Cuota>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<MovimientoCaja>().Property(x => x.Tipo).HasConversion<string>(); b.Entity<Configuracion>().HasIndex(x => x.Clave).IsUnique();
        b.Entity<Deposito>().Property(x => x.Nombre).HasMaxLength(150);
        b.Entity<Deposito>().HasIndex(x => new { x.SucursalId, x.Nombre }).IsUnique();
        b.Entity<Deposito>().HasOne(x => x.Sucursal).WithMany(x => x.Depositos)
            .HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sucursal>().Property(x => x.Nombre).HasMaxLength(150);
        b.Entity<Sucursal>().Property(x => x.RazonSocial).HasMaxLength(200);
        b.Entity<Sucursal>().Property(x => x.Cuit).HasMaxLength(11);
        b.Entity<Sucursal>().Property(x => x.CondicionIva).HasMaxLength(50);
        b.Entity<Sucursal>().Property(x => x.DomicilioFiscal).HasMaxLength(250);
        b.Entity<Sucursal>().Property(x => x.Localidad).HasMaxLength(150);
        b.Entity<Sucursal>().Property(x => x.Telefono).HasMaxLength(50);
        b.Entity<Sucursal>().Property(x => x.Email).HasMaxLength(200);
        b.Entity<Sucursal>().Property(x => x.IngresosBrutos).HasMaxLength(50);
        b.Entity<Sucursal>().HasIndex(x => x.Nombre).IsUnique();
        b.Entity<Sucursal>().HasIndex(x => new { x.Cuit, x.PuntoVentaAfip }).IsUnique()
            .HasFilter("\"Cuit\" IS NOT NULL AND \"PuntoVentaAfip\" IS NOT NULL");
        b.Entity<Sucursal>().HasOne(x => x.DepositoPropio).WithMany(x => x.Sucursales)
            .HasForeignKey(x => x.DepositoPropioId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<SucursalLogo>().HasKey(x => x.SucursalId);
        b.Entity<SucursalLogo>().Property(x => x.ContentType).HasMaxLength(50);
        b.Entity<SucursalLogo>().HasOne(x => x.Sucursal).WithOne(x => x.Logo)
            .HasForeignKey<SucursalLogo>(x => x.SucursalId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Usuario>().HasIndex(x=>x.NombreUsuario).IsUnique();
        b.Entity<Usuario>().HasOne(x => x.Sucursal).WithMany(x => x.Usuarios)
            .HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cliente>().HasOne(x => x.Sucursal).WithMany()
            .HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cliente>().HasIndex(x => new { x.SucursalId, x.Apellido, x.Nombre });
        b.Entity<RegistroAuditoria>().HasIndex(x => x.FechaHora); b.Entity<RegistroAuditoria>().HasIndex(x => x.Modulo);
        b.Entity<RegistroAuditoria>().HasIndex(x => new { x.SucursalId, x.FechaHora });
        b.Entity<RegistroAuditoria>().HasOne<Sucursal>().WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Gasto>().HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Gasto>().HasIndex(x => new { x.SucursalId, x.Fecha });
        b.Entity<Presupuesto>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Presupuesto>().HasIndex(x => new { x.SucursalId, x.Numero }).IsUnique()
            .HasDatabaseName("IX_Presupuestos_SucursalId_Numero");
        b.Entity<Presupuesto>().HasIndex(x => new { x.SucursalId, x.Fecha });
        b.Entity<Presupuesto>().HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Presupuesto>().HasOne(x => x.Deposito).WithMany().HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Presupuesto>().HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Presupuesto>().HasMany(x => x.Lineas).WithOne(x => x.Presupuesto).HasForeignKey(x => x.PresupuestoId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Venta>().Property(x => x.Margen).HasColumnType("decimal(18,6)");
        b.Entity<Venta>().HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().HasOne(x => x.Deposito).WithMany().HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().HasIndex(x => new { x.SucursalId, x.FechaVenta });
        b.Entity<Venta>().HasMany(x => x.Cuotas).WithOne(x => x.Venta).HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Cuota>().HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cuota>().HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cuota>().HasOne(x => x.Deposito).WithMany().HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Cuota>().HasIndex(x => new { x.SucursalId, x.FechaVencimiento });
        b.Entity<MovimientoCaja>().HasOne<Venta>().WithMany().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne<Cuota>().WithMany().HasForeignKey(x => x.CuotaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasIndex(x => new { x.SucursalId, x.Fecha });
        b.Entity<MovimientoCaja>().HasIndex(x => new { x.SucursalId, x.NumeroRecibo }).IsUnique()
            .HasFilter("\"NumeroRecibo\" IS NOT NULL");
        b.Entity<MovimientoStock>().Property(x => x.Tipo).HasConversion<string>();
        b.Entity<MovimientoStock>().Property(x => x.Usuario).HasMaxLength(150);
        b.Entity<MovimientoStock>().Property(x => x.Observaciones).HasMaxLength(500);
        b.Entity<MovimientoStock>().HasIndex(x => new { x.DepositoId, x.Fecha });
        b.Entity<MovimientoStock>().HasIndex(x => new { x.DepositoId, x.TipoCespedId });
        b.Entity<MovimientoStock>().HasIndex(x => x.VentaId);
        b.Entity<MovimientoStock>().HasOne(x => x.Deposito).WithMany(x => x.MovimientosStock)
            .HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoStock>().HasOne(x => x.TipoCesped).WithMany()
            .HasForeignKey(x => x.TipoCespedId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoStock>().HasOne(x => x.Venta).WithMany()
            .HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LoteStock>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<LoteStock>().Property(x => x.Color).HasMaxLength(100);
        b.Entity<LoteStock>().Property(x => x.Usuario).HasMaxLength(150);
        b.Entity<LoteStock>().Property(x => x.Observaciones).HasMaxLength(500);
        b.Entity<LoteStock>().HasIndex(x => new { x.DepositoId, x.TipoCespedId, x.Estado });
        b.Entity<LoteStock>().HasOne(x => x.Deposito).WithMany().HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LoteStock>().HasOne(x => x.TipoCesped).WithMany().HasForeignKey(x => x.TipoCespedId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LoteStock>().HasOne(x => x.Venta).WithMany().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<LoteStock>().HasMany(x => x.Rollos).WithOne(x => x.LoteStock).HasForeignKey(x => x.LoteStockId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Rollo>().Property(x => x.Posicion).HasColumnType("char(1)");
        b.Entity<Rollo>().Property(x => x.CodigoBarra).HasMaxLength(150);
        b.Entity<Rollo>().HasIndex(x => x.CodigoBarra).IsUnique();
        b.Entity<Rollo>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Rollos_Posicion", "\"Posicion\" IN ('A','B','C')");
            t.HasCheckConstraint("CK_Rollos_CantidadM2", "\"CantidadM2\" > 0");
        });
        b.Entity<TipoCesped>().HasOne(x => x.Deposito).WithMany().HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<TipoCesped>().HasIndex(x => new { x.DepositoId, x.Nombre }).IsUnique();
        b.Entity<TipoCespedDeposito>().HasKey(x => new { x.TipoCespedId, x.DepositoId });
        b.Entity<TipoCespedDeposito>().HasOne(x => x.TipoCesped).WithMany(x => x.Depositos)
            .HasForeignKey(x => x.TipoCespedId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<TipoCespedDeposito>().HasOne(x => x.Deposito).WithMany(x => x.TiposCesped)
            .HasForeignKey(x => x.DepositoId).OnDelete(DeleteBehavior.Restrict);
        UseCaseInsensitiveText(b);
    }

    /// <summary>
    /// SQL Server (collation CI_AS) compara texto sin distinguir mayúsculas: búsquedas, logins y nombres únicos.
    /// En PostgreSQL se replica con el tipo citext en todas las columnas de texto del dominio.
    /// </summary>
    private static void UseCaseInsensitiveText(ModelBuilder b)
    {
        b.HasPostgresExtension("citext");
        foreach (var entity in b.Model.GetEntityTypes().Where(e => e.ClrType != typeof(DataProtectionKey)))
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(string) && p.GetColumnType() is null))
                property.SetColumnType("citext");
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        foreach (var e in ChangeTracker.Entries<IAuditableEntity>()) { if (e.State == EntityState.Added) e.Entity.CreatedAt = DateTimeOffset.UtcNow; if (e.State is EntityState.Added or EntityState.Modified) e.Entity.UpdatedAt = DateTimeOffset.UtcNow; }
        AddAuditEntries();
        return await base.SaveChangesAsync(ct);
    }

    private void AddAuditEntries()
    {
        var principal = httpContextAccessor?.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return;
        var userName = principal.FindFirstValue("usuario") ?? principal.Identity.Name ?? "sistema";
        var userId = Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : (Guid?)null;
        var entries = ChangeTracker.Entries().Where(e => e.Entity is not RegistroAuditoria && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in entries)
        {
            var changes = new Dictionary<string, object?>();
            if (entry.Entity is SucursalLogo logo)
            {
                changes["logo"] = entry.State == EntityState.Deleted ? "logo eliminado" : "logo actualizado";
                changes["tamañoBytes"] = entry.State == EntityState.Deleted
                    ? entry.OriginalValues[nameof(SucursalLogo.Contenido)] is byte[] old ? old.Length : 0
                    : logo.Contenido.Length;
            }
            foreach (var property in entry.Properties.Where(p => !IsSensitive(p.Metadata.Name)))
            {
                if (entry.Entity is SucursalLogo) continue;
                var before = entry.State == EntityState.Added ? null : property.OriginalValue;
                var after = entry.State == EntityState.Deleted ? null : property.CurrentValue;
                if (entry.State == EntityState.Modified && Equals(before, after)) continue;
                changes[property.Metadata.Name] = new { anterior = Printable(before), nuevo = Printable(after) };
            }
            if (changes.Count == 0) continue;
            var entityName = entry.Metadata.ClrType.Name;
            var key = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? p.OriginalValue?.ToString() ?? ""));
            RegistrosAuditoria.Add(new RegistroAuditoria { SucursalId = currentSucursal?.SucursalId, FechaHora = DateTimeOffset.UtcNow, UsuarioId = userId, Usuario = userName, Modulo = ModuleFor(entityName), Accion = entry.State switch { EntityState.Added => "Creación", EntityState.Modified => "Modificación", _ => "Eliminación" }, Entidad = entityName, EntidadId = key, DetalleJson = JsonSerializer.Serialize(changes) });
        }
    }

    private static bool IsSensitive(string name) => name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Hash", StringComparison.OrdinalIgnoreCase);

    private static object? Printable(object? value) => value is DateOnly date ? date.ToString("yyyy-MM-dd") : value;
    private static string ModuleFor(string entity) => entity switch { "Venta" => "Ventas", "Cuota" => "Cuotas", "MovimientoCaja" => "Caja", "MovimientoStock" or "LoteStock" or "Rollo" => "Stock", "Gasto" => "Gastos", "Presupuesto" or "PresupuestoLinea" => "Presupuestos", "Cliente" => "Clientes", "Usuario" => "Usuarios", "Deposito" or "Sucursal" or "SucursalLogo" or "TipoCesped" or "AlicuotaIva" or "Configuracion" => "Administración", _ => entity };
}

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Tras copiar o restaurar datos con Ids explícitos, los contadores automáticos deben quedar por encima.
        await IdentitySequences.AlignAsync(db);
        // Filas fijas que el sistema espera encontrar (sucursales base y los "Sin asignar" de datos históricos).
        // El esquema lo crean las migraciones; aquí solo se insertan los datos si faltan.
        await EnsureBranch(db, SanFranciscoSucursalId, SanFranciscoDepositoId, "San Francisco", activo: true);
        await EnsureBranch(db, BuenosAiresSucursalId, BuenosAiresDepositoId, "Buenos Aires", activo: true);
        await EnsureBranch(db, SinAsignarSucursalId, SinAsignarDepositoId, "Sin asignar (datos históricos)", activo: false);
        if (!await db.TiposCesped.IgnoreQueryFilters().AnyAsync(x => x.Id == SinAsignarTipoCespedId))
        {
            db.TiposCesped.Add(new TipoCesped
            {
                Id = SinAsignarTipoCespedId, DepositoId = SanFranciscoDepositoId, Nombre = "Sin asignar (stock histórico)",
                Descripcion = "Movimientos anteriores a la gestión de stock por producto.", ColoresJson = "[]", Activo = false
            });
            db.TiposCespedDepositos.Add(new TipoCespedDeposito { TipoCespedId = SinAsignarTipoCespedId, DepositoId = SanFranciscoDepositoId });
            await db.SaveChangesAsync();
        }
        if (!await db.AlicuotasIva.AnyAsync()) db.AlicuotasIva.AddRange(new AlicuotaIva { Nombre = "IVA 21%", Porcentaje = 21 }, new AlicuotaIva { Nombre = "IVA 10,5%", Porcentaje = 10.5m }, new AlicuotaIva { Nombre = "Exento", Porcentaje = 0 });
        if (!await db.TiposCesped.AnyAsync()) db.TiposCesped.AddRange(new TipoCesped { Nombre = "Decorativo 20 mm" }, new TipoCesped { Nombre = "Premium 35 mm" }, new TipoCesped { Nombre = "Deportivo 50 mm" });
        if (!await db.Configuraciones.AnyAsync()) db.Configuraciones.Add(new Configuracion { Clave = "UmbralMuyRentable", ValorDecimal = .30m });
        if (!await db.Usuarios.AnyAsync()) { var admin = new Usuario { Nombre = "Administrador", NombreUsuario = "admin", Rol = "Administrador", PermisosJson = "[]", DebeCambiarPassword = true }; var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<Usuario>>(); admin.PasswordHash = hasher.HashPassword(admin, "Admin123!"); db.Usuarios.Add(admin); }
        await db.SaveChangesAsync();
        var usuarios = await db.Usuarios.ToListAsync();
        var permisosActualizados = false;
        foreach (var usuario in usuarios)
        {
            using var permisosJson = JsonDocument.Parse(string.IsNullOrWhiteSpace(usuario.PermisosJson) ? "[]" : usuario.PermisosJson);
            if (permisosJson.RootElement.ValueKind != JsonValueKind.Array) continue;
            usuario.PermisosJson = JsonSerializer.Serialize(PermisosMatriz.DesdeJson(usuario.PermisosJson));
            permisosActualizados = true;
        }
        if (permisosActualizados) await db.SaveChangesAsync();
    }

    internal static readonly Guid SanFranciscoDepositoId = Guid.Parse("7a100000-0000-0000-0000-000000000001");
    internal static readonly Guid BuenosAiresDepositoId = Guid.Parse("7a100000-0000-0000-0000-000000000002");
    internal static readonly Guid SinAsignarDepositoId = Guid.Parse("7a100000-0000-0000-0000-ffffffffffff");
    internal static readonly Guid SanFranciscoSucursalId = Guid.Parse("7b100000-0000-0000-0000-000000000001");
    internal static readonly Guid BuenosAiresSucursalId = Guid.Parse("7b100000-0000-0000-0000-000000000002");
    internal static readonly Guid SinAsignarSucursalId = Guid.Parse("7b100000-0000-0000-0000-ffffffffffff");
    internal static readonly Guid SinAsignarTipoCespedId = Guid.Parse("7c100000-0000-0000-0000-ffffffffffff");

    private static async Task EnsureBranch(AppDbContext db, Guid sucursalId, Guid depositoId, string nombre, bool activo)
    {
        // Depósito y sucursal se referencian mutuamente: primero el depósito sin sucursal, luego la sucursal
        // y por último se enlaza el depósito con su sucursal.
        var deposito = await db.Depositos.SingleOrDefaultAsync(x => x.Id == depositoId)
                       ?? await db.Depositos.SingleOrDefaultAsync(x => x.SucursalId == null && x.Nombre == nombre);
        if (deposito is null && !await db.Sucursales.AnyAsync(x => x.Id == sucursalId || x.Nombre == nombre))
        {
            deposito = new Deposito { Id = depositoId, Nombre = nombre, Activo = activo };
            db.Depositos.Add(deposito);
            await db.SaveChangesAsync();
        }
        if (deposito is null) return;
        var sucursal = await db.Sucursales.SingleOrDefaultAsync(x => x.Id == sucursalId || x.Nombre == nombre);
        if (sucursal is null)
        {
            sucursal = new Sucursal { Id = sucursalId, Nombre = nombre, DepositoPropioId = deposito.Id, Activo = activo };
            db.Sucursales.Add(sucursal);
            await db.SaveChangesAsync();
        }
        if (deposito.SucursalId is null && sucursal.DepositoPropioId == deposito.Id)
        {
            deposito.SucursalId = sucursal.Id;
            await db.SaveChangesAsync();
        }
    }
}
