using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Core.Models;

public sealed class PostScanWorkResult
{
    public required ScanResult ScanResult { get; init; }
    public required IReadOnlyList<MediaEntry> MergedEntries { get; init; }
    public required AsciiPathIssueSummary AsciiPathSummary { get; init; }
    public required MediaFileHealthReport HealthReport { get; init; }
}
