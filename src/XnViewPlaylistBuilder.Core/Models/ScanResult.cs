namespace XnViewPlaylistBuilder.Core.Models;

public sealed class ScanResult
{
    public required IReadOnlyList<MediaEntry> Entries { get; init; }
    public int DuplicatesSkipped { get; init; }
    public int EmptyFilesSkipped { get; init; }
    public IReadOnlyList<string> SkippedEmptyPaths { get; init; } = [];
    public int DirectoriesScanned { get; init; }
    public TimeSpan Duration { get; init; }
}
