using Api.Shared.Common;

namespace Api.Tests;

public sealed class SucursalLogoTests
{
    [Fact]
    public void AcceptsPngAndReadsDimensions()
    {
        var png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        png[19] = 100; png[23] = 50;
        Assert.Null(SucursalLogoValidator.Validate(png, out var type, out var width, out var height));
        Assert.Equal("image/png", type); Assert.Equal(100, width); Assert.Equal(50, height);
    }

    [Fact]
    public void AcceptsJpegAndReadsDimensions()
    {
        byte[] jpeg = [0xFF, 0xD8, 0xFF, 0xC0, 0, 8, 8, 0, 50, 0, 100, 1];
        Assert.Null(SucursalLogoValidator.Validate(jpeg, out var type, out var width, out var height));
        Assert.Equal("image/jpeg", type); Assert.Equal(100, width); Assert.Equal(50, height);
    }

    [Fact]
    public void RejectsFakePngSvgAndOversizedFiles()
    {
        Assert.NotNull(SucursalLogoValidator.Validate("not png"u8.ToArray(), out _, out _, out _));
        Assert.NotNull(SucursalLogoValidator.Validate("<svg/>"u8.ToArray(), out _, out _, out _));
        Assert.Contains("1 MB", SucursalLogoValidator.Validate(new byte[SucursalLogoValidator.MaxBytes + 1], out _, out _, out _));
    }

    [Fact]
    public void RejectsDimensionsAboveFourThousandPixels()
    {
        var png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        png[18] = 0x13; png[19] = 0x89; // 5001 px
        png[23] = 50;
        Assert.Contains("4000", SucursalLogoValidator.Validate(png, out _, out _, out _));
    }
}
