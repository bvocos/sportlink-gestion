using System.Text.RegularExpressions;
using Api.Shared.Database;

namespace Api.Shared.Common;

public static partial class DocumentoFiscal
{
    public static string? Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var digits = NonDigitRegex().Replace(valor, "");
        return digits.Length == 0 ? null : digits;
    }

    public static bool EsCuitValido(string digitos)
    {
        if (digitos.Length != 11 || digitos.Any(x => !char.IsDigit(x))) return false;
        int[] weights = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];
        var sum = Enumerable.Range(0, 10).Sum(i => (digitos[i] - '0') * weights[i]);
        var remainder = 11 - sum % 11;
        if (remainder == 11) remainder = 0;
        if (remainder == 10) return false;
        return digitos[10] - '0' == remainder;
    }

    public static bool EsDniValido(string digitos) =>
        digitos.Length is 7 or 8 && digitos.All(char.IsDigit);

    public static string Formatear(TipoDocumentoCliente tipo, string digitos) => tipo switch
    {
        TipoDocumentoCliente.Cuit or TipoDocumentoCliente.Cuil when digitos.Length == 11 =>
            $"{digitos[..2]}-{digitos.Substring(2, 8)}-{digitos[10]}",
        TipoDocumentoCliente.Dni when digitos.Length is 7 or 8 =>
            long.Parse(digitos).ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("es-AR")),
        _ => digitos
    };

    [GeneratedRegex(@"\D")]
    private static partial Regex NonDigitRegex();
}
