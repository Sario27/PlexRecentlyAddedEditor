namespace PlexRecentlyAddedEditor.Models;

public sealed class ScanOptions
{
    public required string ServerUrl { get; init; }
    public required string PlexToken { get; init; }
    public required string TmdbApiKey { get; init; }
    public required List<PlexSection> SectionsToScan { get; init; }

    /// How many of the most-recently-added items to pull per library.
    public int ItemsPerLibrary { get; init; } = 50;

    /// A single addedAt day shared by at least this many items in a section is called
    /// out as a likely mass-rescan batch (a Tdarr/Sonarr pass touching a batch of files),
    /// shown as a hint - it never selects anything on its own.
    public int ClusterMinCount { get; init; } = 20;
}

public sealed class ScanProgress
{
    public string Status { get; init; } = "";
    public int Current { get; init; }
    public int Total { get; init; }
}

/// One row in the Recently Added review grid. The user decides what happens to it -
/// nothing is applied unless the row is highlighted in the grid (click / Ctrl+click /
/// Shift+click) and submitted via one of the action buttons.
public sealed class RecentlyAddedRow
{
    public required string LibraryName { get; init; }
    public required string SectionKey { get; init; }
    public required string RatingKey { get; init; }
    public required int TypeId { get; init; }
    public required string MediaKind { get; init; } // "Movie" or "Episode"
    public required string DisplayTitle { get; init; }
    public required DateTimeOffset CurrentAdded { get; init; }
    public string CurrentAddedDisplay => CurrentAdded.ToLocalTime().ToString("g");

    /// Why this row might be worth a look - purely informational.
    public string HintText { get; init; } = "";

    public string NewDateText { get; set; } = "";

    public bool Applied { get; set; }
    public string? ApplyError { get; set; }
}
