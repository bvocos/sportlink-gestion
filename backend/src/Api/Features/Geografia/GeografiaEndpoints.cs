using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;

namespace Api.Features.Geografia;

public sealed record OpcionGeografica(string Id, string Nombre);

internal sealed class GeorefItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
}

internal sealed class ProvinciasResponse
{
    [JsonPropertyName("provincias")] public List<GeorefItem> Provincias { get; set; } = [];
}

internal sealed class LocalidadesResponse
{
    [JsonPropertyName("localidades")] public List<GeorefItem> Localidades { get; set; } = [];
}

public sealed class GeografiaService(IHttpClientFactory clients, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(7);

    public Task<IReadOnlyList<OpcionGeografica>> GetProvincias(CancellationToken ct) =>
        cache.GetOrCreateAsync("georef:provincias", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var response = await clients.CreateClient("Georef")
                .GetFromJsonAsync<ProvinciasResponse>("provincias?campos=id,nombre&orden=nombre&max=100", ct);
            return (IReadOnlyList<OpcionGeografica>)(response?.Provincias
                .Select(x => new OpcionGeografica(x.Id, x.Nombre)).OrderBy(x => x.Nombre).ToList() ?? []);
        })!;

    public Task<IReadOnlyList<OpcionGeografica>> GetLocalidades(string provinciaId, CancellationToken ct) =>
        cache.GetOrCreateAsync($"georef:localidades:{provinciaId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var path = $"localidades?provincia={Uri.EscapeDataString(provinciaId)}&campos=id,nombre&orden=nombre&max=5000";
            var response = await clients.CreateClient("Georef")
                .GetFromJsonAsync<LocalidadesResponse>(path, ct);
            return (IReadOnlyList<OpcionGeografica>)(response?.Localidades
                .Select(x => new OpcionGeografica(x.Id, x.Nombre)).OrderBy(x => x.Nombre).ToList() ?? []);
        })!;
}

public static class GeografiaEndpoints
{
    public static void MapGeografiaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/geografia").WithTags("Geografía").RequireAuthorization("clientes");
        group.MapGet("/provincias", async (GeografiaService service, CancellationToken ct) =>
            Results.Ok(await service.GetProvincias(ct)));
        group.MapGet("/localidades", async (string provincia, GeografiaService service, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(provincia))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["provincia"] = ["Seleccioná una provincia."] });
            return Results.Ok(await service.GetLocalidades(provincia, ct));
        });
    }
}
