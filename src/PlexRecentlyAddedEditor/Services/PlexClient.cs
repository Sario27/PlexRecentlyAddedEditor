using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using PlexRecentlyAddedEditor.Models;

namespace PlexRecentlyAddedEditor.Services;

public sealed class PlexClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _token;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false
    };

    public static readonly Dictionary<string, int> TypeIds = new()
    {
        ["movie"] = 1,
        ["show"] = 2,
        ["season"] = 3,
        ["episode"] = 4
    };

    public PlexClient(string baseUrl, string token, HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token = token;
        _http = httpClient ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path)
    {
        var sep = path.Contains('?') ? '&' : '?';
        var uri = $"{_baseUrl}{path}{sep}X-Plex-Token={Uri.EscapeDataString(_token)}";
        var req = new HttpRequestMessage(method, uri);
        req.Headers.Add("Accept", "application/json");
        return req;
    }

    public async Task<bool> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var sections = await GetSectionsAsync(ct);
            return sections.Count >= 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<PlexSection>> GetSectionsAsync(CancellationToken ct = default)
    {
        using var resp = await _http.SendAsync(BuildRequest(HttpMethod.Get, "/library/sections"), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<PlexContainerResponse<PlexSection>>(JsonOptions, ct);
        return (data?.MediaContainer?.Directory ?? new())
            .Where(s => s.Type is "movie" or "show")
            .ToList();
    }

    /// Fetches the N most-recently-added items in a library, sorted newest first
    /// (movies for a movie library, episodes for a show library - this is what
    /// Plex's own "Recently Added" hub shows for each library type).
    public async Task<List<PlexItem>> GetRecentlyAddedAsync(PlexSection section, int count, CancellationToken ct = default)
    {
        var path = $"/library/sections/{section.Key}/recentlyAdded" +
                   $"?X-Plex-Container-Start=0&X-Plex-Container-Size={count}";
        using var resp = await _http.SendAsync(BuildRequest(HttpMethod.Get, path), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<PlexContainerResponse<PlexItem>>(JsonOptions, ct);
        return data?.MediaContainer?.Metadata ?? new();
    }

    public async Task<PlexItem?> GetFullMetadataAsync(string ratingKey, CancellationToken ct = default)
    {
        using var resp = await _http.SendAsync(BuildRequest(HttpMethod.Get, $"/library/metadata/{ratingKey}"), ct);
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<PlexContainerResponse<PlexItem>>(JsonOptions, ct);
        return data?.MediaContainer?.Metadata?.FirstOrDefault();
    }

    public async Task SetAddedAtAsync(string sectionKey, string ratingKey, int typeId, DateTimeOffset value, CancellationToken ct = default)
    {
        var unix = value.ToUnixTimeSeconds();
        var path = $"/library/sections/{sectionKey}/all?id={ratingKey}&type={typeId}" +
                   $"&addedAt.value={unix}&addedAt.locked=1";
        using var resp = await _http.SendAsync(BuildRequest(HttpMethod.Put, path), ct);
        resp.EnsureSuccessStatusCode();
    }
}
