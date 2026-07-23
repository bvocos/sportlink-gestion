using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Api.Features.Cotizaciones;

public static class CotizacionEndpoints
{
    public static void MapCotizacionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cotizaciones/dolar-blue", async (DolarBlueService service, CancellationToken ct) =>
        {
            var quote = await service.GetAsync(ct);
            return quote is null
                ? Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Cotización temporalmente no disponible")
                : Results.Ok(quote);
        }).WithTags("Cotizaciones");
    }
}

public sealed class DolarBlueService(IHttpClientFactory clients, ILogger<DolarBlueService> logger)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DolarBlueResponse? last;
    private DateTimeOffset nextRefresh = DateTimeOffset.MinValue;

    public async Task<DolarBlueResponse?> GetAsync(CancellationToken ct)
    {
        if (last is not null && DateTimeOffset.UtcNow < nextRefresh) return last;
        await gate.WaitAsync(ct);
        try
        {
            if (last is not null && DateTimeOffset.UtcNow < nextRefresh) return last;
            try
            {
                var data = await clients.CreateClient("DolarApi").GetFromJsonAsync<DolarApiQuote>("v1/dolares/blue", ct);
                if (data is not null && data.Compra > 0 && data.Venta > 0)
                {
                    last = new DolarBlueResponse(data.Compra, data.Venta, data.FechaActualizacion, "Dólar Hoy vía DolarApi", false);
                    nextRefresh = DateTimeOffset.UtcNow.AddMinutes(2);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                logger.LogWarning(ex, "No se pudo actualizar la cotización del dólar blue");
                if (last is not null) last = last with { Desactualizada = true };
                nextRefresh = DateTimeOffset.UtcNow.AddSeconds(30);
            }
            return last;
        }
        finally { gate.Release(); }
    }
}

public sealed record DolarBlueResponse(decimal Compra, decimal Venta, DateTimeOffset FechaActualizacion, string Fuente, bool Desactualizada);
internal sealed record DolarApiQuote(
    [property: JsonPropertyName("compra")] decimal Compra,
    [property: JsonPropertyName("venta")] decimal Venta,
    [property: JsonPropertyName("fechaActualizacion")] DateTimeOffset FechaActualizacion);
