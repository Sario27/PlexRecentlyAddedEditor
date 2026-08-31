using System.Text.Json.Serialization;

namespace PlexRecentlyAddedEditor.Models;

public sealed class PlexContainerResponse<T>
{
    [JsonPropertyName("MediaContainer")]
    public PlexContainer<T>? MediaContainer { get; set; }
}

public sealed class PlexContainer<T>
{
    [JsonPropertyName("Directory")]
    public List<T>? Directory { get; set; }

    [JsonPropertyName("Metadata")]
    public List<T>? Metadata { get; set; }
}

public sealed class PlexSection
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "movie" or "show"
}

public sealed class PlexGuid
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

public sealed class PlexItem
{
    [JsonPropertyName("ratingKey")]
    public string RatingKey { get; set; } = "";

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = ""; // "movie", "show", "episode"

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("addedAt")]
    public long AddedAt { get; set; }

    [JsonPropertyName("originallyAvailableAt")]
    public string? OriginallyAvailableAt { get; set; }

    [JsonPropertyName("viewCount")]
    public int? ViewCount { get; set; }

    [JsonPropertyName("lastViewedAt")]
    public long? LastViewedAt { get; set; }

    [JsonPropertyName("grandparentTitle")]
    public string? GrandparentTitle { get; set; }

    [JsonPropertyName("grandparentRatingKey")]
    public string? GrandparentRatingKey { get; set; }

    [JsonPropertyName("parentIndex")]
    public int? ParentIndex { get; set; } // season number

    [JsonPropertyName("index")]
    public int? Index { get; set; } // episode number

    [JsonPropertyName("Guid")]
    public List<PlexGuid>? Guid { get; set; }

    public bool IsWatched => (ViewCount ?? 0) > 0 || LastViewedAt.HasValue;

    public string? TmdbId => ExtractId("tmdb");
    public string? ImdbId => ExtractId("imdb");
    public string? TvdbId => ExtractId("tvdb");

    private string? ExtractId(string provider)
    {
        var prefix = provider + "://";
        var match = Guid?.FirstOrDefault(g => g.Id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : match.Id[prefix.Length..];
    }
}
