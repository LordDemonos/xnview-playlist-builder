using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class PostScanWorkProcessor
{
    public static PostScanWorkResult Process(
        ScanResult scanResult,
        IReadOnlyList<MediaEntry> existingEntries,
        bool allowDuplicates,
        IEnumerable<string> imageExtensions,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new WorkProgressReport
        {
            Status = "Merging playlist entries…",
            Detail = $"{scanResult.Entries.Count:N0} new file(s)"
        });
        cancellationToken.ThrowIfCancellationRequested();

        var merged = EntryMerge.Merge(existingEntries, scanResult.Entries, allowDuplicates);
        var scannedPaths = scanResult.Entries.Select(entry => entry.AbsolutePath).ToList();

        var asciiSummary = AsciiPathIssueAnalyzer.Analyze(scannedPaths, progress, cancellationToken);
        var healthReport = MediaFileHealthChecker.Analyze(
            scannedPaths,
            imageExtensions,
            progress,
            cancellationToken);

        return new PostScanWorkResult
        {
            ScanResult = scanResult,
            MergedEntries = merged,
            AsciiPathSummary = asciiSummary,
            HealthReport = healthReport
        };
    }
}
