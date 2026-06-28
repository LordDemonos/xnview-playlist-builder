using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Core.Logging;

public sealed class PlaylistRemovalLogger : IDisposable
{
    private readonly string _batchFilePath;
    private readonly bool _enabled;
    private bool _disposed;

    public PlaylistRemovalLogger(bool enabled = true)
    {
        _enabled = enabled;
        _batchFilePath = ActionLog.BeginBatch(ActionLogCategory.Removals, "Playlist entry removal batch");
        LogFilePath = _batchFilePath;
    }

    public string LogFilePath { get; }

    public void WriteSection(MediaFileHealthIssue issue, IEnumerable<string> paths)
    {
        if (!_enabled)
        {
            return;
        }

        var pathList = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (pathList.Count == 0)
        {
            return;
        }

        ActionLog.AppendBatch(_batchFilePath, [
            $"## {DescribeIssue(issue)} ({pathList.Count:N0})",
            string.Empty,
            ..pathList,
            string.Empty
        ]);
    }

    public void WriteSuggestedRoots(IEnumerable<ScanRootSuggestion> suggestions)
    {
        if (!_enabled)
        {
            return;
        }

        var suggestionList = suggestions.ToList();
        if (suggestionList.Count == 0)
        {
            return;
        }

        var lines = new List<string>
        {
            "## Suggested rescan folders (for review — not added automatically)",
            string.Empty
        };

        foreach (var suggestion in suggestionList)
        {
            lines.Add(
                $"{suggestion.FolderPath}\t{suggestion.RemovedFileCount:N0} removed file(s)\t{suggestion.KindLabel}");
        }

        lines.Add(string.Empty);
        ActionLog.AppendBatch(_batchFilePath, lines);
    }

    public void WriteFooter(int totalRemoved, IReadOnlyList<string>? addedScanRoots)
    {
        if (!_enabled)
        {
            return;
        }

        var lines = new List<string>
        {
            $"# Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"# Total removed from playlist: {totalRemoved:N0}"
        };

        if (addedScanRoots is { Count: > 0 })
        {
            lines.Add($"# Scan folders added by user: {addedScanRoots.Count:N0}");
            lines.AddRange(addedScanRoots);
        }

        ActionLog.AppendBatch(_batchFilePath, lines);
    }

    public void Dispose()
    {
        if (_disposed || !_enabled)
        {
            return;
        }

        _disposed = true;
        ActionLog.Info(ActionLogCategory.Removals, $"Removal batch written to {LogFilePath}");
    }

    private static string DescribeIssue(MediaFileHealthIssue issue) =>
        issue switch
        {
            MediaFileHealthIssue.Missing => "Missing on disk",
            MediaFileHealthIssue.Empty => "Empty (0 bytes)",
            MediaFileHealthIssue.InvalidImageHeader => "Invalid image",
            _ => issue.ToString()
        };
}
