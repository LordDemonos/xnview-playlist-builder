namespace XnViewPlaylistBuilder.Core.Models;

public sealed class SldPlaylist
{
    public required string SourcePath { get; init; }
    public required SldOptionsV2 Options { get; init; }
    public required IReadOnlyList<MediaEntry> Entries { get; init; }
}
