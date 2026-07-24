using Api.Shared.Common;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Api.Shared.Database;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor? httpContextAccessor = null) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>(); public DbSet<Venta> Ventas => Set<Venta>(); public DbSet<Cuota> Cuotas => Set<Cuota>();
    public DbSet<MovimientoCaja> MovimientosCaja => Set<MovimientoCaja>(); public DbSet<TipoCesped> TiposCesped => Set<TipoCesped>();
    public DbSet<AlicuotaIva> AlicuotasIva => Set<AlicuotaIva>(); public DbSet<Configuracion> Configuraciones => Set<Configuracion>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        foreach (var t in new[] { typeof(Venta), typeof(Cuota), typeof(MovimientoCaja), typeof(TipoCesped), typeof(AlicuotaIva), typeof(Configuracion) })
            foreach (var p in b.Entity(t).Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal))) p.SetColumnType("decimal(18,2)");
        b.Entity<Cliente>().Property(x => x.Tipo).HasConversion<string>(); b.Entity<Venta>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Venta>().Property(x => x.FormaPago).HasConversion<string>(); b.Entity<Cuota>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<MovimientoCaja>().Property(x => x.Tipo).HasConversion<string>(); b.Entity<Configuracion>().HasIndex(x => x.Clave).IsUnique();
        b.Entity<Usuario>().HasIndex(x=>x.NombreUsuario).IsUnique();
        b.Entity<RegistroAuditoria>().HasIndex(x => x.FechaHora); b.Entity<RegistroAuditoria>().HasIndex(x => x.Modulo);
        b.Entity<Venta>().Property(x => x.Margen).HasColumnType("decimal(18,6)");
        b.Entity<Venta>().HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Venta>().HasMany(x => x.Cuotas).WithOne(x => x.Venta).HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Cuota>().HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne<Venta>().WithMany().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne<Cuota>().WithMany().HasForeignKey(x => x.CuotaId).OnDelete(DeleteBehavior.Restrict);
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
            foreach (var property in entry.Properties.Where(p => !IsSensitive(p.Metadata.Name)))
            {
                var before = entry.State == EntityState.Added ? null : property.OriginalValue;
                var after = entry.State == EntityState.Deleted ? null : property.CurrentValue;
                if (entry.State == EntityState.Modified && Equals(before, after)) continue;
                changes[property.Metadata.Name] = new { anterior = Printable(before), nuevo = Printable(after) };
            }
            if (changes.Count == 0) continue;
            var entityName = entry.Metadata.ClrType.Name;
            var key = string.Join(",", entry.Properties.Where(p => p.Metadata.IsPrimaryKey()).Select(p => p.CurrentValue?.ToString() ?? p.OriginalValue?.ToString() ?? ""));
            RegistrosAuditoria.Add(new RegistroAuditoria { FechaHora = DateTimeOffset.UtcNow, UsuarioId = userId, Usuario = userName, Modulo = ModuleFor(entityName), Accion = entry.State switch { EntityState.Added => "Creación", EntityState.Modified => "Modificación", _ => "Eliminación" }, Entidad = entityName, EntidadId = key, DetalleJson = JsonSerializer.Serialize(changes) });
        }
    }

    private static bool IsSensitive(string name) => name.Contains("Password", StringComparison.OrdinalIgnoreCase) || name.Contains("Hash", StringComparison.OrdinalIgnoreCase);
    private static object? Printable(object? value) => value is DateOnly date ? date.ToString("yyyy-MM-dd") : value;
    private static string ModuleFor(string entity) => entity switch { "Venta" => "Ventas", "Cuota" => "Cuotas", "MovimientoCaja" => "Caja", "Cliente" => "Clientes", "Usuario" => "Usuarios", "TipoCesped" or "AlicuotaIva" or "Configuracion" => "Administración", _ => entity };
}

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Usuarios','DebeCambiarPassword') IS NULL
            BEGIN
                ALTER TABLE dbo.Usuarios ADD DebeCambiarPassword BIT NOT NULL
                    CONSTRAINT DF_Usuarios_DebeCambiarPassword DEFAULT 0;
                EXEC(N'UPDATE dbo.Usuarios SET DebeCambiarPassword=1 WHERE NombreUsuario=N''admin''');
            END;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Usuarios','IntentosFallidos') IS NULL
                ALTER TABLE dbo.Usuarios ADD IntentosFallidos INT NOT NULL
                    CONSTRAINT DF_Usuarios_IntentosFallidos DEFAULT 0;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Usuarios','BloqueadoHasta') IS NULL
                ALTER TABLE dbo.Usuarios ADD BloqueadoHasta DATETIMEOFFSET(7) NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Clientes','ProvinciaId') IS NULL
                ALTER TABLE dbo.Clientes ADD ProvinciaId NVARCHAR(20) NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Clientes','LocalidadId') IS NULL
                ALTER TABLE dbo.Clientes ADD LocalidadId NVARCHAR(20) NULL;
            """);
        if (!await db.AlicuotasIva.AnyAsync()) db.AlicuotasIva.AddRange(new AlicuotaIva { Nombre="IVA 21%", Porcentaje=21 }, new AlicuotaIva { Nombre="IVA 10,5%", Porcentaje=10.5m }, new AlicuotaIva { Nombre="Exento", Porcentaje=0 });
        if (!await db.TiposCesped.AnyAsync()) db.TiposCesped.AddRange(new TipoCesped { Nombre="Decorativo 20 mm" }, new TipoCesped { Nombre="Premium 35 mm" }, new TipoCesped { Nombre="Deportivo 50 mm" });
        if (!await db.Configuraciones.AnyAsync()) db.Configuraciones.Add(new Configuracion { Clave="UmbralMuyRentable", ValorDecimal=.30m });
        if (!await db.Usuarios.AnyAsync()) { var admin=new Usuario{Nombre="Administrador",NombreUsuario="admin",Rol="Administrador",PermisosJson="[]",DebeCambiarPassword=true}; var hasher=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<Usuario>>(); admin.PasswordHash=hasher.HashPassword(admin,"Admin123!"); db.Usuarios.Add(admin); }
        await db.SaveChangesAsync();
    }
}
