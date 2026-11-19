using Api.Shared.Database;

namespace Api.Shared.Common;

public static class MonedaCajaService
{
    public const string CotizacionRequerida = "Ingresá la cotización del dólar con la que se cobró en pesos.";

    public static decimal ImporteCobrado(decimal montoUsd, MonedaCaja moneda, decimal? cotizacion) => moneda switch
    {
        MonedaCaja.USD => Round(montoUsd),
        MonedaCaja.ARS when cotizacion is > 0 => Round(montoUsd * cotizacion.Value),
        MonedaCaja.ARS => throw new ArgumentOutOfRangeException(nameof(cotizacion), CotizacionRequerida),
        _ => throw new ArgumentOutOfRangeException(nameof(moneda))
    };

    public static decimal MontoUsd(decimal importeEnMoneda, MonedaCaja moneda, decimal? cotizacion) => moneda switch
    {
        MonedaCaja.USD => Round(importeEnMoneda),
        MonedaCaja.ARS when cotizacion is > 0 => Round(importeEnMoneda / cotizacion.Value),
        MonedaCaja.ARS => throw new ArgumentOutOfRangeException(nameof(cotizacion), CotizacionRequerida),
        _ => throw new ArgumentOutOfRangeException(nameof(moneda))
    };

    public static string? Validate(MonedaCaja moneda, decimal? cotizacion) =>
        moneda == MonedaCaja.ARS && cotizacion is null or <= 0 ? CotizacionRequerida : null;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
