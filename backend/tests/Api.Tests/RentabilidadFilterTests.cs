using Api.Features.Rentabilidad;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class RentabilidadFilterTests
{
    [Fact]
    public void DateRange_ExcludesSalesOutsideSelectedDates()
    {
        var ventas = new[]
        {
            Sale(new DateOnly(2026, 7, 31)),
            Sale(new DateOnly(2026, 8, 1)),
            Sale(new DateOnly(2026, 8, 15)),
            Sale(new DateOnly(2026, 9, 1))
        }.AsQueryable();

        var result = RentabilidadEndpoints.ApplyFilters(ventas, null,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, venta =>
            Assert.InRange(venta.FechaVenta, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)));
    }

    [Fact]
    public void DateRangeAndClientSearch_AreAppliedTogether()
    {
        var ventas = new[]
        {
            Sale(new DateOnly(2026, 8, 10), "Bruno", "Vocos"),
            Sale(new DateOnly(2026, 8, 10), "Jacobo", "Vocos"),
            Sale(new DateOnly(2026, 7, 10), "Bruno", "Vocos")
        }.AsQueryable();

        var result = RentabilidadEndpoints.ApplyFilters(ventas, "Bruno",
            new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)).ToList();

        Assert.Single(result);
        Assert.Equal("Bruno", result[0].Cliente.Nombre);
    }

    private static Venta Sale(DateOnly fecha, string nombre = "Cliente", string apellido = "Prueba") => new()
    {
        Id = Guid.NewGuid(),
        FechaVenta = fecha,
        Estado = EstadoVenta.Confirmada,
        Cliente = new Cliente { Nombre = nombre, Apellido = apellido }
    };
}
