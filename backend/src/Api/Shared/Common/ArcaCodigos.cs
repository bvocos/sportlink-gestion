using Api.Shared.Database;

namespace Api.Shared.Common;

/// <summary>
/// Códigos según las tablas de parámetros de ARCA (FEParamGetTiposDoc y
/// FEParamGetCondicionIvaReceptor). Verificar contra el servicio al implementar la facturación.
/// </summary>
public static class ArcaCodigos
{
    public const int DocTipoSinIdentificar = 99;

    public static int DocTipo(TipoDocumentoCliente tipo) => tipo switch
    {
        TipoDocumentoCliente.Cuit => 80,
        TipoDocumentoCliente.Cuil => 86,
        TipoDocumentoCliente.Dni => 96,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null)
    };

    public static int CondicionIvaReceptorId(CondicionIvaCliente condicion) => condicion switch
    {
        CondicionIvaCliente.ResponsableInscripto => 1,
        CondicionIvaCliente.Exento => 4,
        CondicionIvaCliente.ConsumidorFinal => 5,
        CondicionIvaCliente.Monotributista => 6,
        CondicionIvaCliente.SujetoNoCategorizado => 7,
        CondicionIvaCliente.MonotributistaSocial => 13,
        CondicionIvaCliente.IvaNoAlcanzado => 15,
        _ => throw new ArgumentOutOfRangeException(nameof(condicion), condicion, null)
    };

    public static string Etiqueta(CondicionIvaCliente condicion) => condicion switch
    {
        CondicionIvaCliente.ConsumidorFinal => "Consumidor final",
        CondicionIvaCliente.ResponsableInscripto => "Responsable inscripto",
        CondicionIvaCliente.Monotributista => "Monotributista",
        CondicionIvaCliente.Exento => "Exento",
        CondicionIvaCliente.SujetoNoCategorizado => "Sujeto no categorizado",
        CondicionIvaCliente.MonotributistaSocial => "Monotributista social",
        CondicionIvaCliente.IvaNoAlcanzado => "IVA no alcanzado",
        _ => throw new ArgumentOutOfRangeException(nameof(condicion), condicion, null)
    };

    public static string Etiqueta(TipoDocumentoCliente tipo) => tipo switch
    {
        TipoDocumentoCliente.Dni => "DNI",
        TipoDocumentoCliente.Cuit => "CUIT",
        TipoDocumentoCliente.Cuil => "CUIL",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null)
    };
}
