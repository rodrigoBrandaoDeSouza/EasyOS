using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EasyOS.Infrastructure.Geocoding;

public sealed record GeoPoint(float Latitude, float Longitude);

/// <summary>Converte endereço em coordenadas (usadas pelo app mobile no check-in).</summary>
public interface IGeocoder
{
    /// <summary>Devolve null quando o endereço não é encontrado ou o serviço falha (o legado gravava 0,0).</summary>
    Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct = default);
}

public sealed class GeocodingOptions
{
    /// <summary>Com chave, usa a Geocoding API do Google; sem chave, o Nominatim (OpenStreetMap), gratuito.</summary>
    public string? GoogleApiKey { get; set; }
    public string NominatimUserAgent { get; set; } = "EasyOS/1.0 (contato@easyos.com.br)";
}

public sealed class Geocoder(HttpClient http, IOptions<GeocodingOptions> options, ILogger<Geocoder> logger) : IGeocoder
{
    public async Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        try
        {
            return string.IsNullOrWhiteSpace(options.Value.GoogleApiKey)
                ? await Nominatim(address, ct)
                : await Google(address, options.Value.GoogleApiKey!, ct);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "Falha ao geocodificar endereço");
            return null;
        }
    }

    private async Task<GeoPoint?> Google(string address, string key, CancellationToken ct)
    {
        var url = $"https://maps.googleapis.com/maps/api/geocode/json?region=br&address={Uri.EscapeDataString(address)}&key={Uri.EscapeDataString(key)}";
        using var doc = JsonDocument.Parse(await http.GetStringAsync(url, ct));
        var results = doc.RootElement.GetProperty("results");
        if (results.GetArrayLength() == 0) return null;
        var loc = results[0].GetProperty("geometry").GetProperty("location");
        return new GeoPoint((float)loc.GetProperty("lat").GetDouble(), (float)loc.GetProperty("lng").GetDouble());
    }

    private async Task<GeoPoint?> Nominatim(string address, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=br&q={Uri.EscapeDataString(address)}");
        request.Headers.UserAgent.ParseAdd(options.Value.NominatimUserAgent);
        using var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        var items = await response.Content.ReadFromJsonAsync<List<NominatimItem>>(ct);
        var first = items?.FirstOrDefault();
        if (first is null) return null;
        return new GeoPoint(
            float.Parse(first.lat, CultureInfo.InvariantCulture),
            float.Parse(first.lon, CultureInfo.InvariantCulture));
    }

    // Nomes iguais ao JSON do Nominatim.
    private sealed record NominatimItem(string lat, string lon);
}
