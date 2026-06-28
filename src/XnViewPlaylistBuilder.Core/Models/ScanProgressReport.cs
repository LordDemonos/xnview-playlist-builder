namespace XnViewPlaylistBuilder.Core.Models;

public sealed class ScanProgressReport
{
    public int FilesFound { get; init; }
    public int DuplicatesSkipped { get; init; }
    public int DirectoriesScanned { get; init; }
    public int RootIndex { get; init; }
    public int TotalRoots { get; init; }
    public string? CurrentPath { get; init; }
}
