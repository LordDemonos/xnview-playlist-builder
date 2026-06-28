namespace XnViewPlaylistBuilder.Core.Models;

public sealed class MediaEntry
{
    public required string AbsolutePath { get; init; }
    public string? StoredPath { get; init; }
    public int SourceRootIndex { get; init; }

    public string DisplayPath => StoredPath ?? AbsolutePath;
}
