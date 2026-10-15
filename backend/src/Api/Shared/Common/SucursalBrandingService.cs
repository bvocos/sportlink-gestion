using Api.Shared.Database;
using Microsoft.EntityFrameworkCore;

namespace Api.Shared.Common;

public sealed record SucursalBranding(Guid SucursalId, string RazonSocial, string Domicilio,
    string Localidad, string Telefono, string Email, string Cuit, byte[]? Logo,
    string? LogoContentType);

public sealed class SucursalBrandingService(AppDbContext db)
{
    public async Task<SucursalBranding?> GetAsync(Guid sucursalId, CancellationToken ct) =>
        await db.Sucursales.AsNoTracking().Where(x => x.Id == sucursalId)
            .Select(x => new SucursalBranding(x.Id,
                string.IsNullOrWhiteSpace(x.RazonSocial) ? x.Nombre : x.RazonSocial,
                x.DomicilioFiscal ?? "", x.Localidad ?? "", x.Telefono ?? "", x.Email ?? "",
                x.Cuit ?? "", x.Logo == null ? null : x.Logo.Contenido,
                x.Logo == null ? null : x.Logo.ContentType))
            .SingleOrDefaultAsync(ct);
}

public static class SucursalLogoValidator
{
    public const int MaxBytes = 1024 * 1024;
    public const int MaxDimension = 4000;

    public static string? Validate(byte[] bytes, out string? contentType, out int width, out int height)
    {
        contentType = null; width = height = 0;
        if (bytes.Length == 0) return "Seleccioná una imagen PNG o JPEG.";
        if (bytes.Length > MaxBytes) return "El logo no puede superar 1 MB.";
        if (TryPng(bytes, out width, out height)) contentType = "image/png";
        else if (TryJpeg(bytes, out width, out height)) contentType = "image/jpeg";
        else return "El archivo no es una imagen PNG o JPEG válida.";
        if (width <= 0 || height <= 0) return "No se pudieron leer las dimensiones de la imagen.";
        if (width > MaxDimension || height > MaxDimension)
            return "El logo no puede superar 4000 píxeles de ancho ni de alto.";
        return null;
    }

    private static bool TryPng(byte[] b, out int width, out int height)
    {
        width = height = 0;
        byte[] signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (b.Length < 24 || !b.AsSpan(0, 8).SequenceEqual(signature)) return false;
        width = ReadBigEndianInt(b, 16); height = ReadBigEndianInt(b, 20);
        return width > 0 && height > 0;
    }

    private static bool TryJpeg(byte[] b, out int width, out int height)
    {
        width = height = 0;
        if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return false;
        var i = 2;
        while (i + 8 <= b.Length)
        {
            if (b[i] != 0xFF) { i++; continue; }
            var marker = b[i + 1]; i += 2;
            if (marker is 0xD8 or 0xD9) continue;
            if (i + 2 > b.Length) return false;
            var length = (b[i] << 8) | b[i + 1];
            if (length < 2 || i + length > b.Length) return false;
            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                height = (b[i + 3] << 8) | b[i + 4];
                width = (b[i + 5] << 8) | b[i + 6];
                return width > 0 && height > 0;
            }
            i += length;
        }
        return false;
    }

    private static int ReadBigEndianInt(byte[] b, int i) =>
        (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
}
