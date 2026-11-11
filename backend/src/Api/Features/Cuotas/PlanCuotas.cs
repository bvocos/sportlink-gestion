using Api.Shared.Database;

namespace Api.Features.Cuotas;

/// <summary>
/// Recálculo del plan de cuotas al cobrar o anular (solo San Francisco).
/// El total de la venta nunca cambia: si se cobra un importe distinto al de la cuota, esa cuota queda
/// cerrada por lo cobrado y el saldo restante se reparte en partes iguales entre las cuotas pendientes
/// (los centavos que sobran van a la última). Buenos Aires mantiene el cobro sin recálculo.
/// </summary>
internal static class PlanCuotas
{
    internal static bool UsaRecalculo(Guid sucursalId) => sucursalId == SeedData.SanFranciscoSucursalId;

    /// <summary>Cuota que todavía puede recibir saldo: sin pagos o pagada parcialmente.</summary>
    internal static bool EstaAbierta(Cuota c) => c.ImportePagado == 0 || c.ImportePagado < c.ImportePactado;

    internal static decimal Saldo(IEnumerable<Cuota> cuotas)
    {
        var lista = cuotas as IReadOnlyCollection<Cuota> ?? cuotas.ToList();
        return lista.Sum(c => c.ImportePactado) - lista.Sum(c => c.ImportePagado);
    }

    /// <summary>Reparte <paramref name="saldo"/> en partes iguales entre las cuotas abiertas.</summary>
    internal static void Repartir(IEnumerable<Cuota> abiertas, decimal saldo)
    {
        var ordenadas = abiertas.OrderBy(c => c.Numero).ToList();
        if (ordenadas.Count == 0) return;
        saldo = Math.Max(saldo, 0);
        var parte = Math.Floor(saldo / ordenadas.Count * 100m) / 100m;
        for (var i = 0; i < ordenadas.Count; i++)
        {
            var cuota = ordenadas[i];
            var importe = i == ordenadas.Count - 1 ? saldo - parte * (ordenadas.Count - 1) : parte;
            cuota.ImportePactado = cuota.ImportePagado + importe;
            cuota.Estado = importe <= 0 ? EstadoCuota.Pagada
                : cuota.ImportePagado > 0 ? EstadoCuota.PagadaParcial
                : EstadoCuota.Pendiente;
        }
    }

    /// <summary>
    /// Aplica un cobro sobre <paramref name="cuota"/> recalculando el resto del plan.
    /// Devuelve un mensaje de error si el importe no es válido (y en ese caso no modifica nada).
    /// </summary>
    internal static string? AplicarPago(Cuota cuota, IReadOnlyCollection<Cuota> cuotasVenta, decimal importe)
    {
        var otras = cuotasVenta.Where(c => !ReferenceEquals(c, cuota) && EstaAbierta(c)).ToList();
        if (otras.Count == 0)
        {
            // Última cuota pendiente: no hay dónde repartir; si pagan menos queda pagada parcial.
            var error = CuotaEndpoints.ValidatePayment(importe, cuota.ImportePactado - cuota.ImportePagado);
            if (error is not null) return error;
            cuota.ImportePagado += importe;
            cuota.Estado = cuota.ImportePagado >= cuota.ImportePactado ? EstadoCuota.Pagada : EstadoCuota.PagadaParcial;
            return null;
        }

        var total = cuotasVenta.Sum(c => c.ImportePactado);
        var saldoError = CuotaEndpoints.ValidatePayment(importe, Saldo(cuotasVenta));
        if (saldoError is not null) return saldoError;
        cuota.ImportePagado += importe;
        cuota.ImportePactado = cuota.ImportePagado;
        cuota.Estado = EstadoCuota.Pagada;
        Repartir(otras, total - cuotasVenta.Sum(c => c.ImportePagado));
        return null;
    }

    /// <summary>Anula el cobro de <paramref name="cuota"/> y vuelve a repartir el saldo entre las pendientes.</summary>
    internal static void AplicarAnulacion(Cuota cuota, IReadOnlyCollection<Cuota> cuotasVenta)
    {
        var total = cuotasVenta.Sum(c => c.ImportePactado);
        cuota.ImportePagado = 0;
        Repartir(cuotasVenta.Where(EstaAbierta), total - cuotasVenta.Sum(c => c.ImportePagado));
    }
}
