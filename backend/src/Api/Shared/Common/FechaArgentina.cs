namespace Api.Shared.Common;

public static class FechaArgentina
{
    private static readonly TimeSpan ArgentinaOffset = TimeSpan.FromHours(-3);

    public static DateOnly Hoy() => Hoy(DateTimeOffset.UtcNow);

    public static DateOnly Hoy(DateTimeOffset utcAhora) =>
        DateOnly.FromDateTime(utcAhora.ToOffset(ArgentinaOffset).DateTime);
}
