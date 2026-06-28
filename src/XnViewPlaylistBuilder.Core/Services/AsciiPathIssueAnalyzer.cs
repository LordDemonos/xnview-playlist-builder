namespace XnViewPlaylistBuilder.Core.Services;

using XnViewPlaylistBuilder.Core.Models;

public sealed class AsciiPathIssueSummary
{
    public static AsciiPathIssueSummary Empty { get; } = new();

    public int AffectedEntryCount { get; init; }
    public int AffectedDirectoryCount { get; init; }
    public int RenameOperationCount { get; init; }
    public IReadOnlyList<string> ExampleDirectories { get; init; } = [];

    public bool HasIssues => AffectedEntryCount > 0;

    public string FormatDetail(int maxExamples = 5, bool relativePathFallbackEnabled = false)
    {
        if (!HasIssues)
        {
            return string.Empty;
        }

        var lines = new List<string>
        {
            $"{AffectedEntryCount:N0} file(s) under {AffectedDirectoryCount:N0} folder(s) use non-ASCII or mojibake names.",
            "XnView MP may skip them when the playlist uses absolute paths."
        };

        if (relativePathFallbackEnabled)
        {
            lines.Add("Settings currently saves non-ASCII paths relative to the .sld file as a fallback.");
        }

        if (ExampleDirectories.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Affected folders:");
            foreach (var directory in ExampleDirectories.Take(maxExamples))
            {
                lines.Add(directory);
            }

            var remaining = ExampleDirectories.Count - maxExamples;
            if (remaining > 0)
            {
                lines.Add($"… and {remaining:N0} more folder(s)");
            }
        }

        lines.Add(string.Empty);
        lines.Add("Use Fix names to rename files and folders on disk to ASCII, then save the playlist.");

        return string.Join(Environment.NewLine, lines);
    }
}

public static class AsciiPathIssueAnalyzer
{
    public static AsciiPathIssueSummary Analyze(
        IEnumerable<string> absolutePaths,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new WorkProgressReport
        {
            Status = "Checking path names…"
        });

        var paths = absolutePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (paths.Count == 0)
        {
            return AsciiPathIssueSummary.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var plan = new MediaPathRenameService().BuildPlan(paths);
        if (plan.AffectedEntryCount == 0)
        {
            return AsciiPathIssueSummary.Empty;
        }

        var directories = plan.Operations
            .Where(operation => operation.IsDirectory)
            .Select(operation => operation.SourcePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AsciiPathIssueSummary
        {
            AffectedEntryCount = plan.AffectedEntryCount,
            AffectedDirectoryCount = directories.Count,
            RenameOperationCount = plan.Operations.Count,
            ExampleDirectories = directories
        };
    }
}
