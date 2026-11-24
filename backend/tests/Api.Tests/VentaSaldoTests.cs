using Api.Features.Ventas;
using Api.Shared.Database;

namespace Api.Tests;

public class VentaSaldoTests
{
    [Fact]
    public void Calcula_entrega_cobros_y_anulados_sin_saldo_negativo()
    {
        var cobros = new[]
        {
            new CobroVenta { ImporteUsd = 250m },
            new CobroVenta { ImporteUsd = 900m, Anulado = true }
        };

        Assert.Equal(550m, VentaSaldo.Calcular(1_000m, 200m, cobros));
        Assert.Equal(0m, VentaSaldo.Calcular(100m, 80m, 50m));
        Assert.Equal(0m, VentaSaldo.Calcular(1_000m, 0m, cobros, false));
    }
}
