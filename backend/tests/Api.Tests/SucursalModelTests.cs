using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests;

public sealed class SucursalModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=SportlinkModelTests;Trusted_Connection=True;")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Nombres_de_deposito_y_sucursal_son_unicos()
    {
        using var db = CreateContext();
        var deposito = db.Model.FindEntityType(typeof(Deposito))!;
        var sucursal = db.Model.FindEntityType(typeof(Sucursal))!;

        Assert.Contains(deposito.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Deposito.Nombre));
        Assert.Contains(sucursal.GetIndexes(), index => index.IsUnique && index.Properties.Single().Name == nameof(Sucursal.Nombre));
    }

    [Fact]
    public void Usuario_puede_quedar_sin_sucursal_y_el_borrado_es_restringido()
    {
        using var db = CreateContext();
        var usuario = db.Model.FindEntityType(typeof(Usuario))!;
        var sucursalId = usuario.FindProperty(nameof(Usuario.SucursalId))!;
        var foreignKey = usuario.GetForeignKeys().Single(x => x.Properties.Contains(sucursalId));

        Assert.True(sucursalId.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(typeof(Sucursal), foreignKey.PrincipalEntityType.ClrType);
    }

    [Fact]
    public void Sucursal_requiere_un_deposito_y_no_lo_elimina_en_cascada()
    {
        using var db = CreateContext();
        var sucursal = db.Model.FindEntityType(typeof(Sucursal))!;
        var depositoId = sucursal.FindProperty(nameof(Sucursal.DepositoPropioId))!;
        var foreignKey = sucursal.GetForeignKeys().Single(x => x.Properties.Contains(depositoId));

        Assert.False(depositoId.IsNullable);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        Assert.Equal(typeof(Deposito), foreignKey.PrincipalEntityType.ClrType);
    }
}
