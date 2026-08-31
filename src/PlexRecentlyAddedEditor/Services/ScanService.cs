using PlexRecentlyAddedEditor.Models;

namespace PlexRecentlyAddedEditor.Services;

/// Builds the Recently Added review list: the last N items per selected library,
/// each with a TMDb-suggested date pre-filled and an informational hint (already
/// watched / part of a same-day batch) to help the user decide - nothing here
/// selects or changes anything on its own, that's entirely up to the user.
public sealed class ScanService
{
    public async Task<List<RecentlyAddedRow>> LoadRecentlyAddedAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken ct = default)
    {
        var plex = new PlexClient(options.ServerUrl, options.PlexToken);
        var tmdb = new TmdbClient(options.TmdbApiKey);
        var rows = new List<RecentlyAddedRow>();
        var showTmdbCache = new Dictionary<string, string?>();

        foreach (var section in options.SectionsToScan)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new ScanProgress { Status = $"Loading '{section.Title}'...", Current = 0, Total = 0 });

            var items = await plex.GetRecentlyAddedAsync(section, options.ItemsPerLibrary, ct);
            var clusterDays = items
                .GroupBy(i => DateTimeOffset.FromUnixTimeSeconds(i.AddedAt).UtcDateTime.Date)
                .Where(g => g.Count() >= options.ClusterMinCount)
                .Select(g => g.Key)
                .ToHashSet();

            int done = 0;
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                done++;
                progress?.Report(new ScanProgress
                {
                    Status = $"Checking '{section.Title}': {item.Title}",
                    Current = done,
                    Total = items.Count
                });

                var added = DateTimeOffset.FromUnixTimeSeconds(item.AddedAt);
                var isCluster = clusterDays.Contains(added.UtcDateTime.Date);
                var hints = new List<string>();
                if (item.IsWatched) hints.Add("already watched");
                if (isCluster) hints.Add("part of a same-day batch");

                DateOnly? suggested = null;
                string displayTitle;

                if (section.Type == "movie")
                {
                    displayTitle = item.Year.HasValue ? $"{item.Title} ({item.Year})" : item.Title;
                    var tmdbId = item.TmdbId;
                    if (tmdbId is null)
                    {
                        var full = await plex.GetFullMetadataAsync(item.RatingKey, ct);
                        tmdbId = full?.TmdbId;
                    }
                    if (tmdbId is not null)
                        suggested = await tmdb.GetMovieReleaseDateAsync(tmdbId, ct);
                }
                else
                {
                    var season = item.ParentIndex ?? 0;
                    var episode = item.Index ?? 0;
                    displayTitle = $"{item.GrandparentTitle} - S{season:00}E{episode:00} - {item.Title}";
                    if (item.GrandparentRatingKey is not null && item.ParentIndex.HasValue && item.Index.HasValue)
                    {
                        if (!showTmdbCache.TryGetValue(item.GrandparentRatingKey, out var showTmdbId))
                        {
                            var showMeta = await plex.GetFullMetadataAsync(item.GrandparentRatingKey, ct);
                            showTmdbId = showMeta?.TmdbId;
                            showTmdbCache[item.GrandparentRatingKey] = showTmdbId;
                        }
                        if (showTmdbId is not null)
                            suggested = await tmdb.GetEpisodeAirDateAsync(showTmdbId, item.ParentIndex.Value, item.Index.Value, ct);
                    }
                }

                rows.Add(new RecentlyAddedRow
                {
                    LibraryName = section.Title,
                    SectionKey = section.Key,
                    RatingKey = item.RatingKey,
                    TypeId = PlexClient.TypeIds[section.Type == "movie" ? "movie" : "episode"],
                    MediaKind = section.Type == "movie" ? "Movie" : "Episode",
                    DisplayTitle = displayTitle,
                    CurrentAdded = added,
                    HintText = hints.Count > 0 ? string.Join(", ", hints) : "",
                    NewDateText = suggested?.ToString("yyyy-MM-dd") ?? ""
                });
            }
        }

        return rows.OrderByDescending(r => r.CurrentAdded).ToList();
    }

    public async Task ApplySelectedAsync(ScanOptions options, List<RecentlyAddedRow> selected, IProgress<ScanProgress>? progress, CancellationToken ct = default)
    {
        var plex = new PlexClient(options.ServerUrl, options.PlexToken);
        int done = 0;
        foreach (var row in selected)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            progress?.Report(new ScanProgress { Status = $"Updating '{row.DisplayTitle}'", Current = done, Total = selected.Count });

            if (!DateOnly.TryParse(row.NewDateText, out var newDate))
            {
                row.ApplyError = $"Could not parse date '{row.NewDateText}' (expected yyyy-MM-dd)";
                continue;
            }

            try
            {
                var target = new DateTimeOffset(newDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)).AddHours(12);
                await plex.SetAddedAtAsync(row.SectionKey, row.RatingKey, row.TypeId, target, ct);
                row.Applied = true;
            }
            catch (Exception ex)
            {
                row.ApplyError = ex.Message;
            }
        }
    }
}
