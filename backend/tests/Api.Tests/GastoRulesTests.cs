using Api.Features.Gastos;
using Xunit;

namespace Api.Tests;

public sealed class GastoRulesTests
{
    [Fact]
    public void Gasto_valido_no_genera_errores()
    {
        var errors = GastoEndpoints.Validate(new GastoRequest(new DateOnly(2026, 8, 12), "Servicios", "Internet", 15000, null));
        Assert.Empty(errors);
    }

    [Fact]
    public void Gasto_exige_importe_categoria_y_descripcion()
    {
        var errors = GastoEndpoints.Validate(new GastoRequest(new DateOnly(2026, 8, 12), " ", "", 0, null));
        Assert.Contains("importe", errors.Keys);
        Assert.Contains("categoria", errors.Keys);
        Assert.Contains("descripcion", errors.Keys);
    }
}
