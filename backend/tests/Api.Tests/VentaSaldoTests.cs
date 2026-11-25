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

    [Fact]
    public void Al_bajar_una_entrega_completa_activa_el_seguimiento_del_saldo()
    {
        Assert.True(VentaService.ShouldTrackBalance(FormaPago.Contado, 2_100m, 1_050m, 0m, false, FormaPago.Contado, 2_100m));
        Assert.True(VentaService.ShouldTrackBalance(FormaPago.Transferencia, 2_100m, 1_000m, 100m, false, FormaPago.Transferencia, 1_500m));
        Assert.False(VentaService.ShouldTrackBalance(FormaPago.Cuotas, 2_100m, 1_050m, 0m, true, FormaPago.Contado, 2_100m));
    }

    [Fact]
    public void Editar_una_venta_vieja_sin_bajar_entrega_no_activa_seguimiento()
    {
        Assert.False(VentaService.ShouldTrackBalance(FormaPago.Contado, 2_100m, 0m, 0m, false, FormaPago.Contado, 0m));
        Assert.True(VentaService.ShouldTrackBalance(FormaPago.Contado, 2_100m, 1_050m, 0m, false, FormaPago.Contado, 2_100m));
    }
}
