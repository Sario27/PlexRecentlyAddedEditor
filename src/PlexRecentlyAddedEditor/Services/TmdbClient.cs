using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PlexRecentlyAddedEditor.Services;

public sealed class TmdbClient
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.themoviedb.org/3";

    public TmdbClient(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey;
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    private sealed class MovieResponse
    {
        [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
    }

    private sealed class EpisodeResponse
    {
        [JsonPropertyName("air_date")] public string? AirDate { get; set; }
    }

    public async Task<DateOnly?> GetMovieReleaseDateAsync(string tmdbMovieId, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/movie/{tmdbMovieId}?api_key={Uri.EscapeDataString(_apiKey)}";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var data = await resp.Content.ReadFromJsonAsync<MovieResponse>(cancellationToken: ct);
        return ParseDate(data?.ReleaseDate);
    }

    public async Task<DateOnly?> GetEpisodeAirDateAsync(string tmdbShowId, int season, int episode, CancellationToken ct = default)
    {
        var url = $"{BaseUrl}/tv/{tmdbShowId}/season/{season}/episode/{episode}?api_key={Uri.EscapeDataString(_apiKey)}";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var data = await resp.Content.ReadFromJsonAsync<EpisodeResponse>(cancellationToken: ct);
        return ParseDate(data?.AirDate);
    }

    public async Task<bool> ValidateApiKeyAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/configuration?api_key={Uri.EscapeDataString(_apiKey)}";
            using var resp = await _http.GetAsync(url, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static DateOnly? ParseDate(string? s) =>
        !string.IsNullOrWhiteSpace(s) && DateOnly.TryParse(s, out var d) ? d : null;
}
