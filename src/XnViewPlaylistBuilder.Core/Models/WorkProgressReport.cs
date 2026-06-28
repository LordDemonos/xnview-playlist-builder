namespace XnViewPlaylistBuilder.Core.Models;

public sealed class WorkProgressReport
{
    public required string Status { get; init; }
    public string? Detail { get; init; }
    public double? PercentComplete { get; init; }
}
