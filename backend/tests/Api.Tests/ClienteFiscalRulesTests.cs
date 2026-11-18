using Api.Features.Clientes;
using Api.Shared.Common;
using Api.Shared.Database;

namespace Api.Tests;

public sealed class ClienteFiscalRulesTests
{
    [Fact]
    public void DocumentoFiscal_NormalizesValidatesAndFormats()
    {
        Assert.Equal("20123456786", DocumentoFiscal.Normalizar("20-12345678-6"));
        Assert.Equal("20123456", DocumentoFiscal.Normalizar(" 20.123.456 "));
        Assert.Null(DocumentoFiscal.Normalizar(""));
        Assert.True(DocumentoFiscal.EsCuitValido("20123456786"));
        Assert.False(DocumentoFiscal.EsCuitValido("20123456785"));
        Assert.True(DocumentoFiscal.EsDniValido("1234567"));
        Assert.True(DocumentoFiscal.EsDniValido("12345678"));
        Assert.False(DocumentoFiscal.EsDniValido("123456"));
        Assert.False(DocumentoFiscal.EsDniValido("123456789"));
        Assert.Equal("20-12345678-6", DocumentoFiscal.Formatear(TipoDocumentoCliente.Cuit, "20123456786"));
        Assert.Equal("12.345.678", DocumentoFiscal.Formatear(TipoDocumentoCliente.Dni, "12345678"));

        var remainderTen = Enumerable.Range(10_000_000, 90_000_000)
            .Select(x => $"20{x:00000000}0").First(HasRemainderTen);
        Assert.False(DocumentoFiscal.EsCuitValido(remainderTen));
    }

    [Fact]
    public void ArcaCodigos_MapsEveryEnumValue()
    {
        Assert.Equal(99, ArcaCodigos.DocTipoSinIdentificar);
        Assert.Equal(new[] { 96, 80, 86 }, Enum.GetValues<TipoDocumentoCliente>().Select(ArcaCodigos.DocTipo));
        Assert.Equal(new[] { 5, 1, 6, 4, 7, 13, 15 },
            Enum.GetValues<CondicionIvaCliente>().Select(ArcaCodigos.CondicionIvaReceptorId));
        Assert.All(Enum.GetValues<TipoDocumentoCliente>(), x => Assert.False(string.IsNullOrWhiteSpace(ArcaCodigos.Etiqueta(x))));
        Assert.All(Enum.GetValues<CondicionIvaCliente>(), x => Assert.False(string.IsNullOrWhiteSpace(ArcaCodigos.Etiqueta(x))));
    }

    [Theory]
    [MemberData(nameof(ValidationCases))]
    public void Validator_AppliesFiscalRules(CrearClienteCommand command, bool valid, string? property)
    {
        var result = new CrearClienteValidator().Validate(command);
        Assert.Equal(valid, result.IsValid);
        if (property is not null) Assert.Contains(result.Errors, x => x.PropertyName == property);
    }

    [Theory]
    [InlineData("2026-11-19T02:30:00+00:00", "2026-11-18")]
    [InlineData("2026-11-18T02:59:00+00:00", "2026-11-17")]
    [InlineData("2026-11-18T03:00:00+00:00", "2026-11-18")]
    public void FechaArgentina_UsesFixedUtcMinusThree(string instant, string expected) =>
        Assert.Equal(DateOnly.Parse(expected), FechaArgentina.Hoy(DateTimeOffset.Parse(instant)));

    public static TheoryData<CrearClienteCommand, bool, string?> ValidationCases() => new()
    {
        { Base(numero: "20123456786"), false, "TipoDocumento" },
        { Base(tipo: TipoDocumentoCliente.Cuit), false, "NumeroDocumento" },
        { Base(tipo: TipoDocumentoCliente.Cuit, numero: "20123456785"), false, "NumeroDocumento" },
        { Base(tipo: TipoDocumentoCliente.Dni, numero: "12345678", condicion: CondicionIvaCliente.ResponsableInscripto), false, "TipoDocumento" },
        { Base(tipo: TipoDocumentoCliente.Dni, numero: "12345678", condicion: CondicionIvaCliente.ConsumidorFinal), true, null },
        { Base(), true, null }
    };

    private static CrearClienteCommand Base(TipoDocumentoCliente? tipo = null, string? numero = null,
        CondicionIvaCliente? condicion = null) => new("Ana", "Pérez", "3515555555", null,
        "Córdoba", "Córdoba", null, null, TipoCliente.Particular, new DateOnly(2026, 1, 1), null,
        TipoDocumento: tipo, NumeroDocumento: numero, CondicionIva: condicion);

    private static bool HasRemainderTen(string digits)
    {
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = Enumerable.Range(0, 10).Sum(i => (digits[i] - '0') * weights[i]);
        return 11 - sum % 11 == 10;
    }
}
