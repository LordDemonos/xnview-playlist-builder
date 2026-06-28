using XnViewPlaylistBuilder.Core.Logging;

namespace XnViewPlaylistBuilder.Core.Services;

public enum RenameUndoResolveStatus
{
    Ready,
    AlreadyRestored,
    CurrentNotFound,
    OriginalExists,
    ParentAlreadyCorrected,
    NotUnderAnchor,
    SameLeafName,
    AnchorNotFound
}

public sealed record PathSegmentSubstitution(string LogSegment, string DiskSegment);

public sealed class RenameUndoOptions
{
    public string? AnchorPath { get; init; }

    public IReadOnlyList<PathSegmentSubstitution> Substitutions { get; init; } = [];

    public bool FilterToAnchorChildren { get; init; } = true;

    public bool HideParentCorrectedRows { get; init; } = true;

    /// <summary>
    /// When false, grid preview resolves paths without touching the file system.
    /// Undo on disk always verifies paths.
    /// </summary>
    public bool VerifyDiskPaths { get; init; }
}

public sealed record RenameUndoResolution(
    RenameLogEntry Entry,
    string? CurrentPath,
    string? RestorePath,
    RenameUndoResolveStatus Status)
{
    public bool CanUndo => Status == RenameUndoResolveStatus.Ready;

    public string StatusLabel => Status switch
    {
        RenameUndoResolveStatus.Ready => "Ready",
        RenameUndoResolveStatus.AlreadyRestored => "Already restored",
        RenameUndoResolveStatus.CurrentNotFound => "Not found on disk",
        RenameUndoResolveStatus.OriginalExists => "Original already exists",
        RenameUndoResolveStatus.ParentAlreadyCorrected => "Parent already corrected",
        RenameUndoResolveStatus.NotUnderAnchor => "Outside anchor",
        RenameUndoResolveStatus.SameLeafName => "No leaf change",
        RenameUndoResolveStatus.AnchorNotFound => "Anchor not found",
        _ => Status.ToString()
    };

    public string ResolvedPathLabel =>
        CurrentPath is null || RestorePath is null
            ? string.Empty
            : $"{CurrentPath} → {RestorePath}";
}

public static class RenameUndoResolver
{
    public static IReadOnlyList<RenameUndoResolution> BuildVisibleRows(
        IReadOnlyList<RenameLogEntry> entries,
        RenameUndoOptions options,
        CancellationToken cancellationToken = default)
    {
        var previewOptions = new RenameUndoOptions
        {
            AnchorPath = options.AnchorPath,
            Substitutions = options.Substitutions,
            FilterToAnchorChildren = options.FilterToAnchorChildren,
            HideParentCorrectedRows = options.HideParentCorrectedRows,
            VerifyDiskPaths = false
        };

        var rows = new List<RenameUndoResolution>();
        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolution = Resolve(entries[index], previewOptions);
            if (ShouldShowInGrid(resolution, previewOptions))
            {
                rows.Add(resolution);
            }
        }

        return rows;
    }

    public static RenameUndoResolution Resolve(RenameLogEntry entry, RenameUndoOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.AnchorPath))
        {
            return ResolveRelativeToAnchor(entry, options);
        }

        return ResolveFullPath(entry, options);
    }

    public static bool ShouldShowInGrid(RenameUndoResolution resolution, RenameUndoOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.AnchorPath))
        {
            if (!options.HideParentCorrectedRows)
            {
                return true;
            }

            return resolution.Status is not (
                RenameUndoResolveStatus.ParentAlreadyCorrected or
                RenameUndoResolveStatus.SameLeafName);
        }

        if (options.FilterToAnchorChildren &&
            resolution.Status == RenameUndoResolveStatus.NotUnderAnchor)
        {
            return false;
        }

        if (options.HideParentCorrectedRows &&
            resolution.Status is RenameUndoResolveStatus.ParentAlreadyCorrected or RenameUndoResolveStatus.SameLeafName)
        {
            return false;
        }

        return true;
    }

    public static string ApplySegmentSubstitutions(
        string absolutePath,
        IReadOnlyList<PathSegmentSubstitution> substitutions)
    {
        if (substitutions.Count == 0)
        {
            return NormalizeExistingPath(absolutePath);
        }

        var fullPath = NormalizeExistingPath(absolutePath);
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var relative = fullPath[root.Length..].TrimStart('\\', '/');
        if (string.IsNullOrEmpty(relative))
        {
            return fullPath;
        }

        var segments = relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        var map = substitutions
            .Where(substitution => !string.IsNullOrWhiteSpace(substitution.LogSegment))
            .GroupBy(substitution => substitution.LogSegment, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Last().DiskSegment,
                StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < segments.Length; index++)
        {
            if (map.TryGetValue(segments[index], out var replacement) &&
                !string.IsNullOrWhiteSpace(replacement))
            {
                segments[index] = replacement;
            }
        }

        return segments.Length == 0
            ? root.TrimEnd('\\', '/')
            : Path.Combine(new[] { root.TrimEnd('\\', '/') }.Concat(segments).ToArray());
    }

    private static RenameUndoResolution ResolveRelativeToAnchor(RenameLogEntry entry, RenameUndoOptions options)
    {
        var anchor = NormalizeDirectory(options.AnchorPath!);
        if (options.VerifyDiskPaths && !Directory.Exists(anchor))
        {
            return new RenameUndoResolution(
                entry,
                null,
                null,
                RenameUndoResolveStatus.AnchorNotFound);
        }

        if (!IsDirectChildOfAnchor(entry.SourcePath, anchor, options.Substitutions))
        {
            return new RenameUndoResolution(
                entry,
                null,
                null,
                RenameUndoResolveStatus.NotUnderAnchor);
        }

        var leafSource = GetLeafName(entry.SourcePath);
        var leafTarget = GetLeafName(entry.TargetPath);
        if (string.Equals(leafSource, leafTarget, StringComparison.OrdinalIgnoreCase))
        {
            return new RenameUndoResolution(
                entry,
                null,
                null,
                RenameUndoResolveStatus.SameLeafName);
        }

        var currentPath = Path.Combine(anchor, leafTarget);
        var restorePath = Path.Combine(anchor, leafSource);
        return BuildResolution(entry, currentPath, restorePath, options);
    }

    private static RenameUndoResolution ResolveFullPath(RenameLogEntry entry, RenameUndoOptions options)
    {
        var currentPath = ApplySegmentSubstitutions(entry.TargetPath, options.Substitutions);
        var restorePath = ApplySegmentSubstitutions(entry.SourcePath, options.Substitutions);

        if (!string.Equals(GetLeafName(entry.SourcePath), GetLeafName(entry.TargetPath), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(currentPath, restorePath, StringComparison.OrdinalIgnoreCase))
        {
            return new RenameUndoResolution(
                entry,
                currentPath,
                restorePath,
                RenameUndoResolveStatus.ParentAlreadyCorrected);
        }

        return BuildResolution(entry, currentPath, restorePath, options);
    }

    private static RenameUndoResolution BuildResolution(
        RenameLogEntry entry,
        string currentPath,
        string restorePath,
        RenameUndoOptions options)
    {
        currentPath = NormalizeExistingPath(currentPath);
        restorePath = NormalizeExistingPath(restorePath);

        if (string.Equals(currentPath, restorePath, StringComparison.OrdinalIgnoreCase))
        {
            return new RenameUndoResolution(
                entry,
                currentPath,
                restorePath,
                RenameUndoResolveStatus.AlreadyRestored);
        }

        if (!options.VerifyDiskPaths)
        {
            return new RenameUndoResolution(
                entry,
                currentPath,
                restorePath,
                RenameUndoResolveStatus.Ready);
        }

        var currentKind = MediaPathRenameService.InferPathKind(currentPath);
        if (currentKind is null)
        {
            return new RenameUndoResolution(
                entry,
                currentPath,
                restorePath,
                RenameUndoResolveStatus.CurrentNotFound);
        }

        if (MediaPathRenameService.InferPathKind(restorePath) is not null)
        {
            return new RenameUndoResolution(
                entry,
                currentPath,
                restorePath,
                RenameUndoResolveStatus.OriginalExists);
        }

        return new RenameUndoResolution(
            entry,
            currentPath,
            restorePath,
            RenameUndoResolveStatus.Ready);
    }

    internal static bool IsDirectChildOfAnchor(
        string sourcePath,
        string anchorPath,
        IReadOnlyList<PathSegmentSubstitution> substitutions)
    {
        var anchor = NormalizeDirectory(anchorPath);
        var adjustedSource = ApplySegmentSubstitutions(sourcePath, substitutions);
        var parent = Path.GetDirectoryName(NormalizeDirectory(adjustedSource));
        return !string.IsNullOrWhiteSpace(parent) &&
               string.Equals(NormalizeDirectory(parent), anchor, StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetLeafName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var leaf = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(leaf) ? trimmed : leaf;
    }

    private static string NormalizeDirectory(string path) =>
        PathKeyNormalizer.Normalize(path);

    private static string NormalizeExistingPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return PathKeyNormalizer.Normalize(path);
        }
    }
}
