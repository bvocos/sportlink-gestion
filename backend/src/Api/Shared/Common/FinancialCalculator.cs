namespace Api.Shared.Common;

public static class FinancialCalculator
{
    public static decimal CalculateIva(decimal costoOperativo, decimal porcentajeIva) =>
        Math.Round(costoOperativo * porcentajeIva / 100m, 2, MidpointRounding.AwayFromZero);
}
