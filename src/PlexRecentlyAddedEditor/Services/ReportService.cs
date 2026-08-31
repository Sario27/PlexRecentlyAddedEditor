using System.Globalization;
using System.IO;
using System.Text;

namespace PlexRecentlyAddedEditor.Services;

using PlexRecentlyAddedEditor.Models;

public static class ReportService
{
    private static string DesktopPath =>
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public static string WriteCsv(List<RecentlyAddedRow> rows, string fileNamePrefix)
    {
        var path = Path.Combine(DesktopPath, $"{fileNamePrefix}-{Timestamp()}.csv");
        var sb = new StringBuilder();
        sb.AppendLine("Library,MediaKind,Title,CurrentAdded,SuggestedOrEnteredDate,Hint");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(',', new[]
            {
                Csv(r.LibraryName),
                Csv(r.MediaKind),
                Csv(r.DisplayTitle),
                Csv(r.CurrentAdded.ToLocalTime().ToString("u")),
                Csv(r.NewDateText),
                Csv(r.HintText)
            }));
        }
        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    public static string WriteSyncReport(List<RecentlyAddedRow> submitted)
    {
        var path = Path.Combine(DesktopPath, $"plex-recently-added-editor-report-{Timestamp()}.txt");
        var sb = new StringBuilder();
        var applied = submitted.Count(r => r.Applied);
        var failed = submitted.Count(r => !r.Applied);

        sb.AppendLine("Plex Recently Added Editor - Report");
        sb.AppendLine($"Run at: {DateTimeOffset.Now:u}");
        sb.AppendLine($"Total changes attempted: {submitted.Count}");
        sb.AppendLine($"Succeeded: {applied}");
        sb.AppendLine($"Failed: {failed}");
        sb.AppendLine();

        foreach (var r in submitted)
        {
            var status = r.Applied ? "OK" : $"FAILED ({r.ApplyError})";
            sb.AppendLine($"[{status}] {r.MediaKind}: {r.DisplayTitle}");
            sb.AppendLine($"    {r.CurrentAdded.ToLocalTime():g} -> {r.NewDateText}");
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        return path;
    }

    private static string Timestamp() => DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
