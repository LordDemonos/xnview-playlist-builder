namespace XnViewPlaylistBuilder.Core.Logging;

public sealed record RenameLogEntry(
    int LineNumber,
    DateTime? Timestamp,
    string SourcePath,
    string TargetPath);
