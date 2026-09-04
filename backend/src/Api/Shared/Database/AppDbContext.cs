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
    public DbSet<Deposito> Depositos => Set<Deposito>();
    public DbSet<Sucursal> Sucursales => Set<Sucursal>();
    public DbSet<MovimientoStock> MovimientosStock => Set<MovimientoStock>();
    public DbSet<RegistroAuditoria> RegistrosAuditoria => Set<RegistroAuditoria>();
    public DbSet<Gasto> Gastos => Set<Gasto>();
    public DbSet<Presupuesto> Presupuestos => Set<Presupuesto>(); public DbSet<PresupuestoLinea> PresupuestoLineas => Set<PresupuestoLinea>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        foreach (var t in new[] { typeof(Venta), typeof(Cuota), typeof(MovimientoCaja), typeof(MovimientoStock), typeof(Gasto), typeof(TipoCesped), typeof(AlicuotaIva), typeof(Configuracion), typeof(Presupuesto), typeof(PresupuestoLinea) })
            foreach (var p in b.Entity(t).Metadata.GetProperties().Where(p => p.ClrType == typeof(decimal))) p.SetColumnType("decimal(18,2)");
        b.Entity<Cliente>().Property(x => x.Tipo).HasConversion<string>(); b.Entity<Venta>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Venta>().Property(x => x.FormaPago).HasConversion<string>(); b.Entity<Cuota>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<MovimientoCaja>().Property(x => x.Tipo).HasConversion<string>(); b.Entity<Configuracion>().HasIndex(x => x.Clave).IsUnique();
        b.Entity<Deposito>().Property(x => x.Nombre).HasMaxLength(150);
        b.Entity<Deposito>().HasIndex(x => x.Nombre).IsUnique();
        b.Entity<Sucursal>().Property(x => x.Nombre).HasMaxLength(150);
        b.Entity<Sucursal>().HasIndex(x => x.Nombre).IsUnique();
        b.Entity<Sucursal>().HasOne(x => x.DepositoPropio).WithMany(x => x.Sucursales)
            .HasForeignKey(x => x.DepositoPropioId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Usuario>().HasIndex(x=>x.NombreUsuario).IsUnique();
        b.Entity<Usuario>().HasOne(x => x.Sucursal).WithMany(x => x.Usuarios)
            .HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RegistroAuditoria>().HasIndex(x => x.FechaHora); b.Entity<RegistroAuditoria>().HasIndex(x => x.Modulo);
        b.Entity<Gasto>().HasIndex(x => x.Fecha);
        b.Entity<Presupuesto>().Property(x => x.Estado).HasConversion<string>();
        b.Entity<Presupuesto>().Property(x => x.Numero).UseIdentityColumn(); b.Entity<Presupuesto>().HasIndex(x => x.Numero).IsUnique(); b.Entity<Presupuesto>().HasIndex(x => x.Fecha);
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
        b.Entity<Cuota>().HasIndex(x => new { x.SucursalId, x.FechaVencimiento });
        b.Entity<MovimientoCaja>().HasOne<Venta>().WithMany().HasForeignKey(x => x.VentaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne<Cuota>().WithMany().HasForeignKey(x => x.CuotaId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasOne(x => x.Sucursal).WithMany().HasForeignKey(x => x.SucursalId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<MovimientoCaja>().HasIndex(x => new { x.SucursalId, x.Fecha });
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
    private static string ModuleFor(string entity) => entity switch { "Venta" => "Ventas", "Cuota" => "Cuotas", "MovimientoCaja" => "Caja", "MovimientoStock" => "Stock", "Gasto" => "Gastos", "Presupuesto" or "PresupuestoLinea" => "Presupuestos", "Cliente" => "Clientes", "Usuario" => "Usuarios", "Deposito" or "Sucursal" or "TipoCesped" or "AlicuotaIva" or "Configuracion" => "Administración", _ => entity };
}

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.Depositos', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Depositos(
                    Id UNIQUEIDENTIFIER NOT NULL,
                    Nombre NVARCHAR(150) NOT NULL,
                    Activo BIT NOT NULL CONSTRAINT DF_Depositos_Activo DEFAULT 1,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL,
                    UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_Depositos PRIMARY KEY (Id));
                CREATE UNIQUE INDEX IX_Depositos_Nombre ON dbo.Depositos(Nombre);
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Nombre = N'San Francisco')
                INSERT dbo.Depositos(Id, Nombre, Activo, CreatedAt)
                VALUES ('7a100000-0000-0000-0000-000000000001', N'San Francisco', 1, SYSDATETIMEOFFSET());
            IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Nombre = N'Buenos Aires')
                INSERT dbo.Depositos(Id, Nombre, Activo, CreatedAt)
                VALUES ('7a100000-0000-0000-0000-000000000002', N'Buenos Aires', 1, SYSDATETIMEOFFSET());

            IF OBJECT_ID(N'dbo.Sucursales', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Sucursales(
                    Id UNIQUEIDENTIFIER NOT NULL,
                    Nombre NVARCHAR(150) NOT NULL,
                    DepositoPropioId UNIQUEIDENTIFIER NOT NULL,
                    PuntoVentaAfip INT NULL,
                    Activo BIT NOT NULL CONSTRAINT DF_Sucursales_Activo DEFAULT 1,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL,
                    UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_Sucursales PRIMARY KEY (Id),
                    CONSTRAINT FK_Sucursales_Depositos_DepositoPropioId FOREIGN KEY (DepositoPropioId) REFERENCES dbo.Depositos(Id));
                CREATE UNIQUE INDEX IX_Sucursales_Nombre ON dbo.Sucursales(Nombre);
                CREATE INDEX IX_Sucursales_DepositoPropioId ON dbo.Sucursales(DepositoPropioId);
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Nombre = N'San Francisco')
                INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo, CreatedAt)
                SELECT '7b100000-0000-0000-0000-000000000001', N'San Francisco', Id, NULL, 1, SYSDATETIMEOFFSET()
                FROM dbo.Depositos WHERE Nombre = N'San Francisco';
            IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Nombre = N'Buenos Aires')
                INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo, CreatedAt)
                SELECT '7b100000-0000-0000-0000-000000000002', N'Buenos Aires', Id, NULL, 1, SYSDATETIMEOFFSET()
                FROM dbo.Depositos WHERE Nombre = N'Buenos Aires';

            IF COL_LENGTH('dbo.Usuarios', 'SucursalId') IS NULL
                ALTER TABLE dbo.Usuarios ADD SucursalId UNIQUEIDENTIFIER NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Usuarios') AND name = N'IX_Usuarios_SucursalId')
                CREATE INDEX IX_Usuarios_SucursalId ON dbo.Usuarios(SucursalId);
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Usuarios_Sucursales_SucursalId')
                ALTER TABLE dbo.Usuarios ADD CONSTRAINT FK_Usuarios_Sucursales_SucursalId
                    FOREIGN KEY (SucursalId) REFERENCES dbo.Sucursales(Id);
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.MovimientosStock', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MovimientosStock(
                    Id UNIQUEIDENTIFIER NOT NULL,
                    DepositoId UNIQUEIDENTIFIER NOT NULL,
                    Tipo NVARCHAR(30) NOT NULL,
                    CantidadM2 DECIMAL(18,2) NOT NULL,
                    Fecha DATETIME2(7) NOT NULL,
                    Usuario NVARCHAR(150) NOT NULL,
                    Observaciones NVARCHAR(500) NULL,
                    VentaId UNIQUEIDENTIFIER NULL,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL,
                    UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_MovimientosStock PRIMARY KEY (Id),
                    CONSTRAINT CK_MovimientosStock_CantidadM2 CHECK (CantidadM2 > 0),
                    CONSTRAINT FK_MovimientosStock_Depositos_DepositoId FOREIGN KEY (DepositoId) REFERENCES dbo.Depositos(Id),
                    CONSTRAINT FK_MovimientosStock_Ventas_VentaId FOREIGN KEY (VentaId) REFERENCES dbo.Ventas(Id));
                CREATE INDEX IX_MovimientosStock_DepositoId_Fecha ON dbo.MovimientosStock(DepositoId, Fecha DESC);
                CREATE INDEX IX_MovimientosStock_VentaId ON dbo.MovimientosStock(VentaId);
            END;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.MovimientosStock','TipoCespedId') IS NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM dbo.TiposCesped WHERE Id='7C100000-0000-0000-0000-FFFFFFFFFFFF')
                    INSERT dbo.TiposCesped(Id,Nombre,Descripcion,PrecioVentaM2,CostoM2,ColoresJson,Activo,CreatedAt)
                    VALUES('7C100000-0000-0000-0000-FFFFFFFFFFFF',N'Sin asignar (stock histórico)',
                        N'Movimientos anteriores a la gestión de stock por producto.',0,0,N'[]',0,SYSDATETIMEOFFSET());
                ALTER TABLE dbo.MovimientosStock ADD TipoCespedId UNIQUEIDENTIFIER NULL;
                UPDATE dbo.MovimientosStock SET TipoCespedId='7C100000-0000-0000-0000-FFFFFFFFFFFF';
                ALTER TABLE dbo.MovimientosStock ALTER COLUMN TipoCespedId UNIQUEIDENTIFIER NOT NULL;
                CREATE INDEX IX_MovimientosStock_DepositoId_TipoCespedId ON dbo.MovimientosStock(DepositoId,TipoCespedId);
                CREATE INDEX IX_MovimientosStock_TipoCespedId ON dbo.MovimientosStock(TipoCespedId);
                ALTER TABLE dbo.MovimientosStock ADD CONSTRAINT FK_MovimientosStock_TiposCesped_TipoCespedId
                    FOREIGN KEY(TipoCespedId) REFERENCES dbo.TiposCesped(Id);
            END;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM dbo.Depositos WHERE Id = '7A100000-0000-0000-0000-FFFFFFFFFFFF')
                INSERT dbo.Depositos(Id, Nombre, Activo, CreatedAt)
                VALUES ('7A100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (datos históricos)', 0, SYSDATETIMEOFFSET());
            IF NOT EXISTS (SELECT 1 FROM dbo.Sucursales WHERE Id = '7B100000-0000-0000-0000-FFFFFFFFFFFF')
                INSERT dbo.Sucursales(Id, Nombre, DepositoPropioId, PuntoVentaAfip, Activo, CreatedAt)
                VALUES ('7B100000-0000-0000-0000-FFFFFFFFFFFF', N'Sin asignar (datos históricos)',
                    '7A100000-0000-0000-0000-FFFFFFFFFFFF', NULL, 0, SYSDATETIMEOFFSET());

            IF COL_LENGTH('dbo.Ventas','SucursalId') IS NULL
            BEGIN
                ALTER TABLE dbo.Ventas ADD SucursalId UNIQUEIDENTIFIER NULL, DepositoId UNIQUEIDENTIFIER NULL;
                UPDATE dbo.Ventas SET SucursalId='7B100000-0000-0000-0000-FFFFFFFFFFFF', DepositoId='7A100000-0000-0000-0000-FFFFFFFFFFFF';
                ALTER TABLE dbo.Ventas ALTER COLUMN SucursalId UNIQUEIDENTIFIER NOT NULL;
                ALTER TABLE dbo.Ventas ALTER COLUMN DepositoId UNIQUEIDENTIFIER NOT NULL;
                CREATE INDEX IX_Ventas_DepositoId ON dbo.Ventas(DepositoId);
                CREATE INDEX IX_Ventas_SucursalId_FechaVenta ON dbo.Ventas(SucursalId, FechaVenta);
                ALTER TABLE dbo.Ventas ADD CONSTRAINT FK_Ventas_Sucursales_SucursalId FOREIGN KEY(SucursalId) REFERENCES dbo.Sucursales(Id);
                ALTER TABLE dbo.Ventas ADD CONSTRAINT FK_Ventas_Depositos_DepositoId FOREIGN KEY(DepositoId) REFERENCES dbo.Depositos(Id);
            END;
            IF COL_LENGTH('dbo.Cuotas','SucursalId') IS NULL
            BEGIN
                ALTER TABLE dbo.Cuotas ADD SucursalId UNIQUEIDENTIFIER NULL;
                UPDATE dbo.Cuotas SET SucursalId='7B100000-0000-0000-0000-FFFFFFFFFFFF';
                ALTER TABLE dbo.Cuotas ALTER COLUMN SucursalId UNIQUEIDENTIFIER NOT NULL;
                CREATE INDEX IX_Cuotas_SucursalId_FechaVencimiento ON dbo.Cuotas(SucursalId, FechaVencimiento);
                ALTER TABLE dbo.Cuotas ADD CONSTRAINT FK_Cuotas_Sucursales_SucursalId FOREIGN KEY(SucursalId) REFERENCES dbo.Sucursales(Id);
            END;
            IF COL_LENGTH('dbo.MovimientosCaja','SucursalId') IS NULL
            BEGIN
                ALTER TABLE dbo.MovimientosCaja ADD SucursalId UNIQUEIDENTIFIER NULL;
                UPDATE dbo.MovimientosCaja SET SucursalId='7B100000-0000-0000-0000-FFFFFFFFFFFF';
                ALTER TABLE dbo.MovimientosCaja ALTER COLUMN SucursalId UNIQUEIDENTIFIER NOT NULL;
                CREATE INDEX IX_MovimientosCaja_SucursalId_Fecha ON dbo.MovimientosCaja(SucursalId, Fecha);
                ALTER TABLE dbo.MovimientosCaja ADD CONSTRAINT FK_MovimientosCaja_Sucursales_SucursalId FOREIGN KEY(SucursalId) REFERENCES dbo.Sucursales(Id);
            END;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Usuarios','DebeCambiarPassword') IS NULL
            BEGIN
                ALTER TABLE dbo.Usuarios ADD DebeCambiarPassword BIT NOT NULL
                    CONSTRAINT DF_Usuarios_DebeCambiarPassword DEFAULT 0;
                EXEC(N'UPDATE dbo.Usuarios SET DebeCambiarPassword=1 WHERE NombreUsuario=N''admin''');
            END;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.Ventas','LineasJson') IS NULL
                ALTER TABLE dbo.Ventas ADD LineasJson NVARCHAR(MAX) NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.Gastos', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Gastos(
                    Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Gastos_Id DEFAULT NEWSEQUENTIALID(),
                    Fecha DATE NOT NULL, Categoria NVARCHAR(100) NOT NULL, Descripcion NVARCHAR(300) NOT NULL,
                    Importe DECIMAL(18,2) NOT NULL, Observaciones NVARCHAR(1000) NULL,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL CONSTRAINT DF_Gastos_CreatedAt DEFAULT SYSDATETIMEOFFSET(),
                    UpdatedAt DATETIMEOFFSET(7) NULL, CONSTRAINT PK_Gastos PRIMARY KEY CLUSTERED (Id),
                    CONSTRAINT CK_Gastos_Importe CHECK (Importe > 0));
                CREATE INDEX IX_Gastos_Fecha ON dbo.Gastos(Fecha DESC);
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
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.TiposCesped','ColoresJson') IS NULL
                ALTER TABLE dbo.TiposCesped ADD ColoresJson NVARCHAR(MAX) NOT NULL
                    CONSTRAINT DF_TiposCesped_ColoresJson DEFAULT N'[]' WITH VALUES;
            IF COL_LENGTH('dbo.Ventas','Color') IS NULL
                ALTER TABLE dbo.Ventas ADD Color NVARCHAR(100) NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.TiposCesped','PrecioContadoM2') IS NULL
            BEGIN
                ALTER TABLE dbo.TiposCesped ADD PrecioContadoM2 DECIMAL(18,2) NOT NULL
                    CONSTRAINT DF_TiposCesped_PrecioContadoM2 DEFAULT 0 WITH VALUES;
                EXEC(N'UPDATE dbo.TiposCesped SET PrecioContadoM2=PrecioVentaM2');
            END;
            IF COL_LENGTH('dbo.TiposCesped','PrecioFinanciadoM2') IS NULL
            BEGIN
                ALTER TABLE dbo.TiposCesped ADD PrecioFinanciadoM2 DECIMAL(18,2) NOT NULL
                    CONSTRAINT DF_TiposCesped_PrecioFinanciadoM2 DEFAULT 0 WITH VALUES;
                EXEC(N'UPDATE dbo.TiposCesped SET PrecioFinanciadoM2=PrecioVentaM2');
            END;
            IF COL_LENGTH('dbo.TiposCesped','DescripcionPresupuesto') IS NULL
                ALTER TABLE dbo.TiposCesped ADD DescripcionPresupuesto NVARCHAR(MAX) NULL;
            IF COL_LENGTH('dbo.TiposCesped','EspecificacionesPresupuesto') IS NULL
                ALTER TABLE dbo.TiposCesped ADD EspecificacionesPresupuesto NVARCHAR(MAX) NULL;
            IF COL_LENGTH('dbo.TiposCesped','FichaTecnicaUrl') IS NULL
                ALTER TABLE dbo.TiposCesped ADD FichaTecnicaUrl NVARCHAR(1000) NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'dbo.Presupuestos', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Presupuestos(Id UNIQUEIDENTIFIER NOT NULL, Numero INT IDENTITY(1,1) NOT NULL, ClienteId UNIQUEIDENTIFIER NOT NULL,
                    Fecha DATE NOT NULL, ValidezHasta DATE NOT NULL, Estado NVARCHAR(30) NOT NULL,
                    DescuentoContadoPorcentaje DECIMAL(18,2) NOT NULL, IvaContadoPorcentaje DECIMAL(18,2) NOT NULL,
                    IvaFinanciadoPorcentaje DECIMAL(18,2) NOT NULL, EntregaFinanciada DECIMAL(18,2) NOT NULL,
                    Observaciones NVARCHAR(2000) NULL, CreatedAt DATETIMEOFFSET(7) NOT NULL, UpdatedAt DATETIMEOFFSET(7) NULL,
                    CONSTRAINT PK_Presupuestos PRIMARY KEY(Id), CONSTRAINT FK_Presupuestos_Clientes FOREIGN KEY(ClienteId) REFERENCES dbo.Clientes(Id));
                CREATE UNIQUE INDEX IX_Presupuestos_Numero ON dbo.Presupuestos(Numero); CREATE INDEX IX_Presupuestos_Fecha ON dbo.Presupuestos(Fecha);
                CREATE TABLE dbo.PresupuestoLineas(Id UNIQUEIDENTIFIER NOT NULL, PresupuestoId UNIQUEIDENTIFIER NOT NULL, TipoCespedId UNIQUEIDENTIFIER NULL,
                    Producto NVARCHAR(200) NOT NULL, Descripcion NVARCHAR(2000) NULL, Color NVARCHAR(100) NULL,
                    CantidadM2 DECIMAL(18,2) NOT NULL, PrecioContadoM2 DECIMAL(18,2) NOT NULL, PrecioFinanciadoM2 DECIMAL(18,2) NOT NULL,
                    TotalContado DECIMAL(18,2) NOT NULL, TotalFinanciado DECIMAL(18,2) NOT NULL,
                    CreatedAt DATETIMEOFFSET(7) NOT NULL, UpdatedAt DATETIMEOFFSET(7) NULL, CONSTRAINT PK_PresupuestoLineas PRIMARY KEY(Id),
                    CONSTRAINT FK_PresupuestoLineas_Presupuestos FOREIGN KEY(PresupuestoId) REFERENCES dbo.Presupuestos(Id) ON DELETE CASCADE);
            END;
            IF COL_LENGTH('dbo.Presupuestos','CantidadCuotas') IS NOT NULL
                ALTER TABLE dbo.Presupuestos DROP COLUMN CantidadCuotas;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('dbo.PresupuestoLineas','PrecioContadoM2') IS NULL
            BEGIN
                ALTER TABLE dbo.PresupuestoLineas ADD PrecioContadoM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_PrecioContadoM2 DEFAULT 0 WITH VALUES;
                ALTER TABLE dbo.PresupuestoLineas ADD PrecioFinanciadoM2 DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_PrecioFinanciadoM2 DEFAULT 0 WITH VALUES;
                ALTER TABLE dbo.PresupuestoLineas ADD TotalContado DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_TotalContado DEFAULT 0 WITH VALUES;
                ALTER TABLE dbo.PresupuestoLineas ADD TotalFinanciado DECIMAL(18,2) NOT NULL CONSTRAINT DF_PresupuestoLineas_TotalFinanciado DEFAULT 0 WITH VALUES;
                IF COL_LENGTH('dbo.PresupuestoLineas','PrecioVentaM2') IS NOT NULL
                    EXEC(N'UPDATE dbo.PresupuestoLineas SET PrecioContadoM2=PrecioVentaM2, PrecioFinanciadoM2=PrecioVentaM2, TotalContado=Total, TotalFinanciado=Total');
            END;
            IF COL_LENGTH('dbo.PresupuestoLineas','PrecioVentaM2') IS NOT NULL
                ALTER TABLE dbo.PresupuestoLineas DROP COLUMN PrecioVentaM2;
            IF COL_LENGTH('dbo.PresupuestoLineas','Total') IS NOT NULL
                ALTER TABLE dbo.PresupuestoLineas DROP COLUMN Total;
            IF COL_LENGTH('dbo.PresupuestoLineas','DescripcionPresupuesto') IS NULL
                ALTER TABLE dbo.PresupuestoLineas ADD DescripcionPresupuesto NVARCHAR(MAX) NULL;
            IF COL_LENGTH('dbo.PresupuestoLineas','EspecificacionesPresupuesto') IS NULL
                ALTER TABLE dbo.PresupuestoLineas ADD EspecificacionesPresupuesto NVARCHAR(MAX) NULL;
            IF COL_LENGTH('dbo.PresupuestoLineas','FichaTecnicaUrl') IS NULL
                ALTER TABLE dbo.PresupuestoLineas ADD FichaTecnicaUrl NVARCHAR(1000) NULL;
            """);
        if (!await db.AlicuotasIva.AnyAsync()) db.AlicuotasIva.AddRange(new AlicuotaIva { Nombre="IVA 21%", Porcentaje=21 }, new AlicuotaIva { Nombre="IVA 10,5%", Porcentaje=10.5m }, new AlicuotaIva { Nombre="Exento", Porcentaje=0 });
        if (!await db.TiposCesped.AnyAsync()) db.TiposCesped.AddRange(new TipoCesped { Nombre="Decorativo 20 mm" }, new TipoCesped { Nombre="Premium 35 mm" }, new TipoCesped { Nombre="Deportivo 50 mm" });
        if (!await db.Configuraciones.AnyAsync()) db.Configuraciones.Add(new Configuracion { Clave="UmbralMuyRentable", ValorDecimal=.30m });
        if (!await db.Usuarios.AnyAsync()) { var admin=new Usuario{Nombre="Administrador",NombreUsuario="admin",Rol="Administrador",PermisosJson="[]",DebeCambiarPassword=true}; var hasher=scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<Usuario>>(); admin.PasswordHash=hasher.HashPassword(admin,"Admin123!"); db.Usuarios.Add(admin); }
        await db.SaveChangesAsync();
    }
}
