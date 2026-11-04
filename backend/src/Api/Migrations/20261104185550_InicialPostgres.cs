using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class InicialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "AlicuotasIva",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "citext", nullable: false),
                    Porcentaje = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlicuotasIva", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuraciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Clave = table.Column<string>(type: "citext", nullable: false),
                    ValorDecimal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuraciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataProtectionKeys",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FriendlyName = table.Column<string>(type: "text", nullable: true),
                    Xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "citext", nullable: false),
                    Apellido = table.Column<string>(type: "citext", nullable: false),
                    Telefono = table.Column<string>(type: "citext", nullable: false),
                    Correo = table.Column<string>(type: "citext", nullable: true),
                    Localidad = table.Column<string>(type: "citext", nullable: false),
                    Provincia = table.Column<string>(type: "citext", nullable: false),
                    LocalidadId = table.Column<string>(type: "citext", nullable: true),
                    ProvinciaId = table.Column<string>(type: "citext", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    FechaPrimerContacto = table.Column<DateOnly>(type: "date", nullable: false),
                    Observaciones = table.Column<string>(type: "citext", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cuotas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    FechaVencimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaPago = table.Column<DateOnly>(type: "date", nullable: true),
                    ImportePactado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ImportePagado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MedioPago = table.Column<string>(type: "citext", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cuotas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Depositos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nombre = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Depositos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sucursales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    RazonSocial = table.Column<string>(type: "citext", maxLength: 200, nullable: true),
                    Cuit = table.Column<string>(type: "citext", maxLength: 11, nullable: true),
                    CondicionIva = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    DomicilioFiscal = table.Column<string>(type: "citext", maxLength: 250, nullable: true),
                    Localidad = table.Column<string>(type: "citext", maxLength: 150, nullable: true),
                    Telefono = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "citext", maxLength: 200, nullable: true),
                    InicioActividades = table.Column<DateOnly>(type: "date", nullable: true),
                    IngresosBrutos = table.Column<string>(type: "citext", maxLength: 50, nullable: true),
                    DepositoPropioId = table.Column<Guid>(type: "uuid", nullable: false),
                    PuntoVentaAfip = table.Column<int>(type: "integer", nullable: true),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sucursales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sucursales_Depositos_DepositoPropioId",
                        column: x => x.DepositoPropioId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TiposCesped",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "citext", nullable: false),
                    Descripcion = table.Column<string>(type: "citext", nullable: true),
                    DescripcionPresupuesto = table.Column<string>(type: "citext", nullable: true),
                    EspecificacionesPresupuesto = table.Column<string>(type: "citext", nullable: true),
                    FichaTecnicaUrl = table.Column<string>(type: "citext", nullable: true),
                    PrecioVentaM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrecioContadoM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrecioFinanciadoM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostoM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    ColoresJson = table.Column<string>(type: "citext", nullable: false),
                    ControlPorLotes = table.Column<bool>(type: "boolean", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposCesped", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TiposCesped_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Gastos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Categoria = table.Column<string>(type: "citext", nullable: false),
                    Descripcion = table.Column<string>(type: "citext", nullable: false),
                    Importe = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "citext", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gastos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gastos_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Presupuestos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    ValidezHasta = table.Column<DateOnly>(type: "date", nullable: false),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    DescuentoContadoPorcentaje = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IvaContadoPorcentaje = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    IvaFinanciadoPorcentaje = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    EntregaFinanciada = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Observaciones = table.Column<string>(type: "citext", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Presupuestos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Presupuestos_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegistrosAuditoria",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: true),
                    FechaHora = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: true),
                    Usuario = table.Column<string>(type: "citext", nullable: false),
                    Modulo = table.Column<string>(type: "citext", nullable: false),
                    Accion = table.Column<string>(type: "citext", nullable: false),
                    Entidad = table.Column<string>(type: "citext", nullable: false),
                    EntidadId = table.Column<string>(type: "citext", nullable: false),
                    DetalleJson = table.Column<string>(type: "citext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrosAuditoria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RegistrosAuditoria_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SucursalesLogos",
                columns: table => new
                {
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Contenido = table.Column<byte[]>(type: "bytea", nullable: false),
                    ContentType = table.Column<string>(type: "citext", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SucursalesLogos", x => x.SucursalId);
                    table.ForeignKey(
                        name: "FK_SucursalesLogos_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "citext", nullable: false),
                    NombreUsuario = table.Column<string>(type: "citext", nullable: false),
                    PasswordHash = table.Column<string>(type: "citext", nullable: false),
                    Rol = table.Column<string>(type: "citext", nullable: false),
                    PermisosJson = table.Column<string>(type: "citext", nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    DebeCambiarPassword = table.Column<bool>(type: "boolean", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false),
                    BloqueadoHasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Usuarios_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TiposCespedDepositos",
                columns: table => new
                {
                    TipoCespedId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TiposCespedDepositos", x => new { x.TipoCespedId, x.DepositoId });
                    table.ForeignKey(
                        name: "FK_TiposCespedDepositos_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TiposCespedDepositos_TiposCesped_TipoCespedId",
                        column: x => x.TipoCespedId,
                        principalTable: "TiposCesped",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ventas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCespedId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlicuotaIvaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Color = table.Column<string>(type: "citext", nullable: true),
                    LineasJson = table.Column<string>(type: "citext", nullable: true),
                    FechaVenta = table.Column<DateOnly>(type: "date", nullable: false),
                    FechaEntregaEstimada = table.Column<DateOnly>(type: "date", nullable: true),
                    CantidadM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrecioUnitario = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrecioTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MontoEntrega = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostoCompraUnitario = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostoCompraTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CostoEnvio = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    OtrosCostos = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Iva = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GananciaBruta = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    GananciaNeta = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Margen = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    FormaPago = table.Column<string>(type: "text", nullable: false),
                    CantidadCuotas = table.Column<int>(type: "integer", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    Observaciones = table.Column<string>(type: "citext", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ventas_AlicuotasIva_AlicuotaIvaId",
                        column: x => x.AlicuotaIvaId,
                        principalTable: "AlicuotasIva",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ventas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ventas_TiposCesped_TipoCespedId",
                        column: x => x.TipoCespedId,
                        principalTable: "TiposCesped",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PresupuestoLineas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PresupuestoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCespedId = table.Column<Guid>(type: "uuid", nullable: true),
                    Producto = table.Column<string>(type: "citext", nullable: false),
                    Descripcion = table.Column<string>(type: "citext", nullable: true),
                    DescripcionPresupuesto = table.Column<string>(type: "citext", nullable: true),
                    EspecificacionesPresupuesto = table.Column<string>(type: "citext", nullable: true),
                    FichaTecnicaUrl = table.Column<string>(type: "citext", nullable: true),
                    Color = table.Column<string>(type: "citext", nullable: true),
                    CantidadM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrecioContadoM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PrecioFinanciadoM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalContado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalFinanciado = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PresupuestoLineas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PresupuestoLineas_Presupuestos_PresupuestoId",
                        column: x => x.PresupuestoId,
                        principalTable: "Presupuestos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LotesStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCespedId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Color = table.Column<string>(type: "citext", maxLength: 100, nullable: true),
                    FechaIngreso = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Usuario = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: true),
                    Estado = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LotesStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LotesStock_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesStock_TiposCesped_TipoCespedId",
                        column: x => x.TipoCespedId,
                        principalTable: "TiposCesped",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LotesStock_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosCaja",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SucursalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Monto = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    NumeroRecibo = table.Column<int>(type: "integer", nullable: true),
                    Concepto = table.Column<string>(type: "citext", nullable: false),
                    Usuario = table.Column<string>(type: "citext", nullable: false),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CuotaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosCaja", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Cuotas_CuotaId",
                        column: x => x.CuotaId,
                        principalTable: "Cuotas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Sucursales_SucursalId",
                        column: x => x.SucursalId,
                        principalTable: "Sucursales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosCaja_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovimientosStock",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DepositoId = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoCespedId = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    CantidadM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Usuario = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    Observaciones = table.Column<string>(type: "citext", maxLength: 500, nullable: true),
                    VentaId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimientosStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Depositos_DepositoId",
                        column: x => x.DepositoId,
                        principalTable: "Depositos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_TiposCesped_TipoCespedId",
                        column: x => x.TipoCespedId,
                        principalTable: "TiposCesped",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimientosStock_Ventas_VentaId",
                        column: x => x.VentaId,
                        principalTable: "Ventas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Rollos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoteStockId = table.Column<Guid>(type: "uuid", nullable: false),
                    Posicion = table.Column<char>(type: "char(1)", nullable: false),
                    CodigoBarra = table.Column<string>(type: "citext", maxLength: 150, nullable: false),
                    CantidadM2 = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rollos", x => x.Id);
                    table.CheckConstraint("CK_Rollos_CantidadM2", "\"CantidadM2\" > 0");
                    table.CheckConstraint("CK_Rollos_Posicion", "\"Posicion\" IN ('A','B','C')");
                    table.ForeignKey(
                        name: "FK_Rollos_LotesStock_LoteStockId",
                        column: x => x.LoteStockId,
                        principalTable: "LotesStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_SucursalId_Apellido_Nombre",
                table: "Clientes",
                columns: new[] { "SucursalId", "Apellido", "Nombre" });

            migrationBuilder.CreateIndex(
                name: "IX_Configuraciones_Clave",
                table: "Configuraciones",
                column: "Clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_ClienteId",
                table: "Cuotas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_DepositoId",
                table: "Cuotas",
                column: "DepositoId");

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_SucursalId_FechaVencimiento",
                table: "Cuotas",
                columns: new[] { "SucursalId", "FechaVencimiento" });

            migrationBuilder.CreateIndex(
                name: "IX_Cuotas_VentaId",
                table: "Cuotas",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_Depositos_SucursalId_Nombre",
                table: "Depositos",
                columns: new[] { "SucursalId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Gastos_SucursalId_Fecha",
                table: "Gastos",
                columns: new[] { "SucursalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_DepositoId_TipoCespedId_Estado",
                table: "LotesStock",
                columns: new[] { "DepositoId", "TipoCespedId", "Estado" });

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_TipoCespedId",
                table: "LotesStock",
                column: "TipoCespedId");

            migrationBuilder.CreateIndex(
                name: "IX_LotesStock_VentaId",
                table: "LotesStock",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_CuotaId",
                table: "MovimientosCaja",
                column: "CuotaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_SucursalId_Fecha",
                table: "MovimientosCaja",
                columns: new[] { "SucursalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_SucursalId_NumeroRecibo",
                table: "MovimientosCaja",
                columns: new[] { "SucursalId", "NumeroRecibo" },
                unique: true,
                filter: "\"NumeroRecibo\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosCaja_VentaId",
                table: "MovimientosCaja",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_DepositoId_Fecha",
                table: "MovimientosStock",
                columns: new[] { "DepositoId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_DepositoId_TipoCespedId",
                table: "MovimientosStock",
                columns: new[] { "DepositoId", "TipoCespedId" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_TipoCespedId",
                table: "MovimientosStock",
                column: "TipoCespedId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimientosStock_VentaId",
                table: "MovimientosStock",
                column: "VentaId");

            migrationBuilder.CreateIndex(
                name: "IX_PresupuestoLineas_PresupuestoId",
                table: "PresupuestoLineas",
                column: "PresupuestoId");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_ClienteId",
                table: "Presupuestos",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_DepositoId",
                table: "Presupuestos",
                column: "DepositoId");

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_SucursalId_Fecha",
                table: "Presupuestos",
                columns: new[] { "SucursalId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Presupuestos_SucursalId_Numero",
                table: "Presupuestos",
                columns: new[] { "SucursalId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_FechaHora",
                table: "RegistrosAuditoria",
                column: "FechaHora");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_Modulo",
                table: "RegistrosAuditoria",
                column: "Modulo");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrosAuditoria_SucursalId_FechaHora",
                table: "RegistrosAuditoria",
                columns: new[] { "SucursalId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_Rollos_CodigoBarra",
                table: "Rollos",
                column: "CodigoBarra",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rollos_LoteStockId",
                table: "Rollos",
                column: "LoteStockId");

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_Cuit_PuntoVentaAfip",
                table: "Sucursales",
                columns: new[] { "Cuit", "PuntoVentaAfip" },
                unique: true,
                filter: "\"Cuit\" IS NOT NULL AND \"PuntoVentaAfip\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_DepositoPropioId",
                table: "Sucursales",
                column: "DepositoPropioId");

            migrationBuilder.CreateIndex(
                name: "IX_Sucursales_Nombre",
                table: "Sucursales",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposCesped_DepositoId_Nombre",
                table: "TiposCesped",
                columns: new[] { "DepositoId", "Nombre" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TiposCespedDepositos_DepositoId",
                table: "TiposCespedDepositos",
                column: "DepositoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_NombreUsuario",
                table: "Usuarios",
                column: "NombreUsuario",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_SucursalId",
                table: "Usuarios",
                column: "SucursalId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_AlicuotaIvaId",
                table: "Ventas",
                column: "AlicuotaIvaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_ClienteId",
                table: "Ventas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_DepositoId",
                table: "Ventas",
                column: "DepositoId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_SucursalId_FechaVenta",
                table: "Ventas",
                columns: new[] { "SucursalId", "FechaVenta" });

            migrationBuilder.CreateIndex(
                name: "IX_Ventas_TipoCespedId",
                table: "Ventas",
                column: "TipoCespedId");

            migrationBuilder.AddForeignKey(
                name: "FK_Clientes_Sucursales_SucursalId",
                table: "Clientes",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Depositos_DepositoId",
                table: "Cuotas",
                column: "DepositoId",
                principalTable: "Depositos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Sucursales_SucursalId",
                table: "Cuotas",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cuotas_Ventas_VentaId",
                table: "Cuotas",
                column: "VentaId",
                principalTable: "Ventas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Depositos_Sucursales_SucursalId",
                table: "Depositos",
                column: "SucursalId",
                principalTable: "Sucursales",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Depositos_Sucursales_SucursalId",
                table: "Depositos");

            migrationBuilder.DropTable(
                name: "Configuraciones");

            migrationBuilder.DropTable(
                name: "DataProtectionKeys");

            migrationBuilder.DropTable(
                name: "Gastos");

            migrationBuilder.DropTable(
                name: "MovimientosCaja");

            migrationBuilder.DropTable(
                name: "MovimientosStock");

            migrationBuilder.DropTable(
                name: "PresupuestoLineas");

            migrationBuilder.DropTable(
                name: "RegistrosAuditoria");

            migrationBuilder.DropTable(
                name: "Rollos");

            migrationBuilder.DropTable(
                name: "SucursalesLogos");

            migrationBuilder.DropTable(
                name: "TiposCespedDepositos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Cuotas");

            migrationBuilder.DropTable(
                name: "Presupuestos");

            migrationBuilder.DropTable(
                name: "LotesStock");

            migrationBuilder.DropTable(
                name: "Ventas");

            migrationBuilder.DropTable(
                name: "AlicuotasIva");

            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "TiposCesped");

            migrationBuilder.DropTable(
                name: "Sucursales");

            migrationBuilder.DropTable(
                name: "Depositos");
        }
    }
}
