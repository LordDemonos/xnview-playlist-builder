namespace XnViewPlaylistBuilder.Core.Services;

public sealed record PathRenamePreviewRow(
    string SourcePath,
    string TargetPath,
    bool IsDirectory,
    bool SourceExists)
{
    public string Status => SourceExists ? "Ready" : "Not found on disk";
}

public sealed class PathRenamePreview
{
    public required PathRenamePlan OriginalPlan { get; init; }
    public required IReadOnlyList<PathRenamePreviewRow> Rows { get; init; }

    public int ReadyCount => Rows.Count(row => row.SourceExists);

    public int MissingCount => Rows.Count(row => !row.SourceExists);

    public IReadOnlyList<PathRenamePreviewRow> VisibleRows(bool includeMissing) =>
        includeMissing
            ? Rows
            : Rows.Where(row => row.SourceExists).ToList();

    public string FormatSummary()
    {
        var baseSummary = OriginalPlan.FormatSummary();
        if (MissingCount == 0)
        {
            return baseSummary;
        }

        return
            $"{baseSummary}{Environment.NewLine}{Environment.NewLine}" +
            $"{ReadyCount:N0} rename(s) are ready on disk. " +
            $"{MissingCount:N0} path(s) were not found — remove them with Check files, or hide them below. " +
            "Only ready paths will be renamed.";
    }

    public PathRenamePlan CreateExecutablePlan()
    {
        var missingSources = Rows
            .Where(row => !row.SourceExists)
            .Select(row => row.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var executableOperations = OriginalPlan.Operations
            .Where(operation => !missingSources.Contains(operation.SourcePath))
            .ToList();

        return new PathRenamePlan
        {
            Operations = executableOperations,
            FilePathMap = OriginalPlan.FilePathMap,
            AffectedEntryCount = OriginalPlan.AffectedEntryCount
        };
    }

    public static PathRenamePreview FromPlan(PathRenamePlan plan)
    {
        var previewOperations = plan.PreviewOperations;
        var rows = previewOperations
            .Select(operation => new PathRenamePreviewRow(
                operation.SourcePath,
                operation.TargetPath,
                operation.IsDirectory,
                MediaPathRenameService.PathExistsOnDisk(operation.SourcePath)))
            .ToList();

        return new PathRenamePreview
        {
            OriginalPlan = plan,
            Rows = rows
        };
    }
}
