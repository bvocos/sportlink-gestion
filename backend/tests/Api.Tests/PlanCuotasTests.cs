using Api.Features.Cuotas;
using Api.Shared.Database;

namespace Api.Tests;

/// <summary>Recálculo de cuotas al cobrar o anular (San Francisco).</summary>
public sealed class PlanCuotasTests
{
    private static List<Cuota> Plan(params decimal[] importes) =>
        importes.Select((importe, i) => new Cuota { Numero = i + 1, ImportePactado = importe, Estado = EstadoCuota.Pendiente }).ToList();

    [Fact]
    public void PagoMenor_CierraLaCuotaYReparteLaDiferencia()
    {
        var plan = Plan(100, 100, 100, 100);
        Assert.Null(PlanCuotas.AplicarPago(plan[0], plan, 70));
        Assert.Equal((70m, 70m, EstadoCuota.Pagada), (plan[0].ImportePactado, plan[0].ImportePagado, plan[0].Estado));
        Assert.All(plan.Skip(1), c => Assert.Equal(110m, c.ImportePactado));
        Assert.Equal(400m, plan.Sum(c => c.ImportePactado)); // el total de la venta no cambia
    }

    [Fact]
    public void PagoMayor_BajaLasCuotasPendientesEnPartesIguales()
    {
        var plan = Plan(100, 100, 100, 100);
        Assert.Null(PlanCuotas.AplicarPago(plan[0], plan, 160));
        Assert.Equal(160m, plan[0].ImportePactado);
        Assert.All(plan.Skip(1), c => Assert.Equal(80m, c.ImportePactado));
        Assert.Equal(400m, plan.Sum(c => c.ImportePactado));
    }

    [Fact]
    public void Centavos_VanALaUltimaCuota()
    {
        var plan = Plan(100, 100, 100, 100);
        Assert.Null(PlanCuotas.AplicarPago(plan[0], plan, 50));
        Assert.Equal(new[] { 116.66m, 116.66m, 116.68m }, plan.Skip(1).Select(c => c.ImportePactado));
        Assert.Equal(400m, plan.Sum(c => c.ImportePactado));
    }

    [Fact]
    public void PagoMayorQueElSaldoDeLaVenta_SeRechazaSinModificarNada()
    {
        var plan = Plan(100, 100, 100);
        Assert.NotNull(PlanCuotas.AplicarPago(plan[0], plan, 301));
        Assert.All(plan, c => Assert.Equal((100m, 0m), (c.ImportePactado, c.ImportePagado)));
    }

    [Fact]
    public void PagoDeTodoElSaldo_CancelaLasCuotasRestantesEnCero()
    {
        var plan = Plan(100, 100, 100);
        Assert.Null(PlanCuotas.AplicarPago(plan[0], plan, 300));
        Assert.All(plan.Skip(1), c => Assert.Equal((0m, EstadoCuota.Pagada), (c.ImportePactado, c.Estado)));
        Assert.Equal(0m, PlanCuotas.Saldo(plan));
    }

    [Fact]
    public void UltimaCuotaConPagoMenor_QuedaPagadaParcialSinTocarLasCerradas()
    {
        var plan = Plan(100, 100);
        plan[0].ImportePagado = 100; plan[0].Estado = EstadoCuota.Pagada;
        Assert.Null(PlanCuotas.AplicarPago(plan[1], plan, 40));
        Assert.Equal((100m, 40m, EstadoCuota.PagadaParcial), (plan[1].ImportePactado, plan[1].ImportePagado, plan[1].Estado));
        Assert.NotNull(PlanCuotas.AplicarPago(plan[1], plan, 61)); // no puede superar lo que falta
    }

    [Fact]
    public void Anulacion_VuelveARepartirElSaldoEntreLasPendientes()
    {
        var plan = Plan(100, 100, 100, 100);
        PlanCuotas.AplicarPago(plan[0], plan, 70); // 70 | 110 | 110 | 110
        PlanCuotas.AplicarAnulacion(plan[0], plan);
        Assert.All(plan, c => Assert.Equal((100m, 0m, EstadoCuota.Pendiente), (c.ImportePactado, c.ImportePagado, c.Estado)));
    }

    [Fact]
    public void Anulacion_ReabreCuotasQueHabianQuedadoEnCero()
    {
        var plan = Plan(100, 100, 100);
        PlanCuotas.AplicarPago(plan[0], plan, 300); // 300 | 0 | 0
        PlanCuotas.AplicarAnulacion(plan[0], plan);
        Assert.All(plan, c => Assert.Equal((100m, EstadoCuota.Pendiente), (c.ImportePactado, c.Estado)));
    }

    [Fact]
    public void SoloSanFranciscoUsaRecalculo()
    {
        Assert.True(PlanCuotas.UsaRecalculo(SeedData.SanFranciscoSucursalId));
        Assert.False(PlanCuotas.UsaRecalculo(SeedData.BuenosAiresSucursalId));
    }
}
