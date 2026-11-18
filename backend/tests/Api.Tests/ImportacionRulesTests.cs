using Api.Features.Stock;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class ImportacionRulesTests
{
    [Fact]
    public void BucketBalances_SeparateNullAndImportCodes()
    {
        var import = Guid.NewGuid();
        var balances = StockEndpoints.BucketBalances([
            new MovimientoStock { ImportacionId = null, Tipo = TipoMovimientoStock.Ingreso, CantidadM2 = 10 },
            new MovimientoStock { ImportacionId = null, Tipo = TipoMovimientoStock.AjusteSalida, CantidadM2 = 3 },
            new MovimientoStock { ImportacionId = import, Tipo = TipoMovimientoStock.Ingreso, CantidadM2 = 20 },
            new MovimientoStock { ImportacionId = import, Tipo = TipoMovimientoStock.SalidaPorVenta, CantidadM2 = 4 }
        ]);
        Assert.Equal(7, balances.Single(x => x.ImportacionId is null).Saldo);
        Assert.Equal(16, balances.Single(x => x.ImportacionId == import).Saldo);
        Assert.Equal(23, balances.Sum(x => x.Saldo));
    }

    [Fact]
    public void OrderBuckets_RemovesNonPositiveAndOrdersByDateThenCode()
    {
        var rows = StockEndpoints.OrderBuckets([
            (Guid.NewGuid(), "B", new DateOnly(2026, 2, 1), 2),
            (Guid.NewGuid(), "C", new DateOnly(2026, 1, 1), 0),
            (Guid.NewGuid(), "A", new DateOnly(2026, 2, 1), 1),
            (null, null, DateOnly.MinValue, 3)
        ]).ToList();
        Assert.Null(rows[0].Id);
        Assert.Equal(new[] { null, "A", "B" }, rows.Select(x => x.Codigo));
    }
}
