using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed record PathRenameOperation(string SourcePath, string TargetPath, bool IsDirectory);

public sealed class PathRenamePlan
{
    public required IReadOnlyList<PathRenameOperation> Operations { get; init; }
    public required IReadOnlyDictionary<string, string> FilePathMap { get; init; }
    public int AffectedEntryCount { get; init; }

    public int DirectoryOperationCount => Operations.Count(operation => operation.IsDirectory);

    public int FileOperationCount => Operations.Count - DirectoryOperationCount;

    public string FormatSummary()
    {
        var directoryCount = DirectoryOperationCount;
        var fileCount = FileOperationCount;

        return
            $"{AffectedEntryCount:N0} playlist entries will get updated paths in the saved .sld file. " +
            $"{directoryCount:N0} folder and {fileCount:N0} file rename(s) will run on disk for non-ASCII or mojibake names. " +
            "Only folder or file name segments with non-ASCII or mojibake characters are changed; path structure is preserved. " +
            "This cannot be undone automatically — review the list below.";
    }

    public IReadOnlyList<PathRenameOperation> PreviewOperations =>
        DirectoryOperationCount > 0
            ? Operations.Where(operation => operation.IsDirectory).ToList()
            : Operations;
}

public sealed class PathRenameExecutionResult
{
    public IReadOnlyList<PathRenameOperation> CompletedOperations { get; init; } = [];
    public IReadOnlyList<PathRenameSkip> SkippedOperations { get; init; } = [];
    public IReadOnlyList<PathRenameConflictResolution> ResolvedConflicts { get; init; } = [];

    public int CompletedCount => CompletedOperations.Count;
    public int SkippedCount => SkippedOperations.Count;
    public int ConflictResolvedCount => ResolvedConflicts.Count;
}

public sealed record PathRenameSkip(string SourcePath, string TargetPath, string Reason);

public sealed record PathRenameConflictResolution(
    string SourcePath,
    string PreferredTargetPath,
    string ResolvedTargetPath);

public sealed class MediaPathRenameService
{
    public PathRenamePlan BuildPlan(
        IEnumerable<string> absolutePaths,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var uniquePaths = absolutePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filePathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var operations = new Dictionary<string, PathRenameOperation>(StringComparer.OrdinalIgnoreCase);

        const int reportInterval = 500;
        for (var index = 0; index < uniquePaths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = uniquePaths[index];

            if (index == 0 || (index + 1) % reportInterval == 0 || index == uniquePaths.Count - 1)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Analyzing paths…",
                    Detail = uniquePaths.Count > 0 ? $"{index + 1:N0} of {uniquePaths.Count:N0}" : null,
                    PercentComplete = uniquePaths.Count > 0 ? (index + 1) * 100.0 / uniquePaths.Count : null
                });
            }

            var targetPath = AsciiPathNormalizer.ToAsciiPath(sourcePath);
            filePathMap[sourcePath] = targetPath;

            if (!AsciiPathNormalizer.NeedsNormalization(sourcePath) ||
                string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddOperations(operations, sourcePath, targetPath);
        }

        return new PathRenamePlan
        {
            Operations = OrderOperations(operations.Values),
            FilePathMap = filePathMap,
            AffectedEntryCount = filePathMap.Count(pair =>
                !string.Equals(pair.Key, pair.Value, StringComparison.OrdinalIgnoreCase))
        };
    }

    public PathRenameExecutionResult ExecutePlan(
        PathRenamePlan plan,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var completed = new List<PathRenameOperation>();
        var skipped = new List<PathRenameSkip>();
        var resolvedConflicts = new List<PathRenameConflictResolution>();
        var targetAdjustments = new List<PathRenameOperation>();
        var reservedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var operations = plan.Operations;
        var orderedCompleted = Array.Empty<PathRenameOperation>();
        var orderedCompletedCount = -1;
        const int reportInterval = 100;

        void RefreshOrderedCompleted()
        {
            if (completed.Count == orderedCompletedCount)
            {
                return;
            }

            orderedCompleted = OrderForPathResolution(completed).ToArray();
            orderedCompletedCount = completed.Count;
        }

        for (var index = 0; index < operations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var operation = operations[index];

            if (index == 0 || (index + 1) % reportInterval == 0 || index == operations.Count - 1)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Renaming paths on disk…",
                    Detail = operations.Count > 0 ? $"{index + 1:N0} of {operations.Count:N0}" : null,
                    PercentComplete = operations.Count > 0 ? (index + 1) * 100.0 / operations.Count : null
                });
            }

            RefreshOrderedCompleted();
            var sourcePath = ResolvePath(operation.SourcePath, orderedCompleted, ordered: true);
            var plannedTargetPath = ApplyTargetAdjustments(
                ResolvePath(operation.TargetPath, orderedCompleted, ordered: true),
                targetAdjustments);

            if (string.Equals(sourcePath, plannedTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var sourceExists = OccupiesPath(sourcePath);
            var preferredExists = OccupiesPath(plannedTargetPath);

            if (!sourceExists && preferredExists)
            {
                reservedTargets.Add(plannedTargetPath);
                ActionLog.Info(ActionLogCategory.Rename, $"Rename skipped (already at target): {operation.SourcePath} -> {plannedTargetPath}");
                completed.Add(operation with { SourcePath = sourcePath, TargetPath = plannedTargetPath });
                continue;
            }

            if (!sourceExists)
            {
                skipped.Add(new PathRenameSkip(sourcePath, plannedTargetPath, "Source path not found"));
                ActionLog.Warning(ActionLogCategory.FixNames, $"Rename skipped (source missing): {sourcePath}");
                ActionLog.Warning(ActionLogCategory.Rename, $"Rename skipped (source missing): {sourcePath}");
                continue;
            }

            var targetPath = FindUniqueTargetPath(plannedTargetPath, operation.IsDirectory, reservedTargets);
            if (!string.Equals(targetPath, plannedTargetPath, StringComparison.OrdinalIgnoreCase))
            {
                resolvedConflicts.Add(new PathRenameConflictResolution(
                    sourcePath,
                    plannedTargetPath,
                    targetPath));

                if (operation.IsDirectory)
                {
                    targetAdjustments.Add(new PathRenameOperation(
                        plannedTargetPath,
                        targetPath,
                        IsDirectory: true));
                }

                ActionLog.Info(ActionLogCategory.Rename, $"Rename conflict resolved: {plannedTargetPath} -> {targetPath}");
            }

            reservedTargets.Add(targetPath);

            var parent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            if (operation.IsDirectory)
            {
                Directory.Move(sourcePath, targetPath);
            }
            else
            {
                File.Move(sourcePath, targetPath);
            }

            ActionLog.Info(ActionLogCategory.Rename, $"Renamed: {sourcePath} -> {targetPath}");
            ActionLog.Info(ActionLogCategory.FixNames, $"Renamed: {sourcePath} -> {targetPath}");
            completed.Add(operation with { SourcePath = sourcePath, TargetPath = targetPath });
        }

        return new PathRenameExecutionResult
        {
            CompletedOperations = completed,
            SkippedOperations = skipped,
            ResolvedConflicts = resolvedConflicts
        };
    }

    /// <summary>
    /// Reverses rename operations logged as <c>Renamed: source -&gt; target</c>.
    /// Uses <see cref="RenameUndoOptions"/> for anchor-relative or substitution-aware resolution.
    /// </summary>
    public PathRenameExecutionResult UndoRenames(
        IReadOnlyList<RenameLogEntry> entries,
        RenameUndoOptions? options = null,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RenameUndoOptions();
        var verifyOptions = new RenameUndoOptions
        {
            AnchorPath = options.AnchorPath,
            Substitutions = options.Substitutions,
            FilterToAnchorChildren = options.FilterToAnchorChildren,
            HideParentCorrectedRows = options.HideParentCorrectedRows,
            VerifyDiskPaths = true
        };
        var completed = new List<PathRenameOperation>();
        var skipped = new List<PathRenameSkip>();
        const int reportInterval = 100;

        var resolutions = entries
            .Select(entry => RenameUndoResolver.Resolve(entry, verifyOptions))
            .ToList();

        foreach (var resolution in resolutions.Where(item => !item.CanUndo))
        {
            skipped.Add(new PathRenameSkip(
                resolution.CurrentPath ?? resolution.Entry.TargetPath,
                resolution.RestorePath ?? resolution.Entry.SourcePath,
                resolution.StatusLabel));
        }

        var orderedResolutions = resolutions
            .Where(resolution => resolution.CanUndo)
            .OrderBy(resolution => MediaPathRenameService.InferPathKind(resolution.CurrentPath!) == true ? 1 : 0)
            .ThenByDescending(resolution => resolution.CurrentPath!.Length)
            .ThenByDescending(resolution => resolution.Entry.LineNumber)
            .ToList();

        for (var index = 0; index < orderedResolutions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolution = orderedResolutions[index];

            if (index == 0 || (index + 1) % reportInterval == 0 || index == orderedResolutions.Count - 1)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Undoing renames on disk…",
                    Detail = orderedResolutions.Count > 0 ? $"{index + 1:N0} of {orderedResolutions.Count:N0}" : null,
                    PercentComplete = orderedResolutions.Count > 0 ? (index + 1) * 100.0 / orderedResolutions.Count : null
                });
            }

            var currentPath = resolution.CurrentPath!;
            var restorePath = resolution.RestorePath!;
            var isDirectory = MediaPathRenameService.InferPathKind(currentPath);
            if (isDirectory is null)
            {
                skipped.Add(new PathRenameSkip(currentPath, restorePath, "Current path not found"));
                AppLog.Warning($"Undo skipped (current path missing): {currentPath}");
                continue;
            }

            if (MediaPathRenameService.InferPathKind(restorePath) is not null)
            {
                skipped.Add(new PathRenameSkip(currentPath, restorePath, "Original path already exists"));
                AppLog.Warning($"Undo skipped (original path exists): {restorePath}");
                continue;
            }

            var parent = Path.GetDirectoryName(restorePath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            try
            {
                if (isDirectory.Value)
                {
                    Directory.Move(currentPath, restorePath);
                }
                else
                {
                    File.Move(currentPath, restorePath);
                }
            }
            catch (Exception ex)
            {
                skipped.Add(new PathRenameSkip(currentPath, restorePath, ex.Message));
                AppLog.Warning($"Undo skipped ({ex.Message}): {currentPath} -> {restorePath}");
                continue;
            }

            AppLog.Info($"Undo rename: {currentPath} -> {restorePath}");
            completed.Add(new PathRenameOperation(currentPath, restorePath, isDirectory.Value));
        }

        return new PathRenameExecutionResult
        {
            CompletedOperations = completed,
            SkippedOperations = skipped
        };
    }

    internal static IReadOnlyList<RenameLogEntry> OrderForUndo(IReadOnlyList<RenameLogEntry> entries) =>
        entries
            .OrderBy(entry => InferPathKind(entry.TargetPath) == true ? 1 : 0)
            .ThenByDescending(entry => entry.TargetPath.Length)
            .ThenByDescending(entry => entry.LineNumber)
            .ToList();

    internal static bool? InferPathKind(string path)
    {
        if (Directory.Exists(path))
        {
            return true;
        }

        if (File.Exists(path))
        {
            return false;
        }

        return null;
    }

    internal static string FindUniqueTargetPath(
        string preferredPath,
        bool isDirectory,
        ISet<string> reservedTargets)
    {
        var normalizedPreferred = Path.GetFullPath(preferredPath);
        if (!OccupiesPath(normalizedPreferred) && !reservedTargets.Contains(normalizedPreferred))
        {
            return normalizedPreferred;
        }

        var parent = Path.GetDirectoryName(normalizedPreferred);
        if (string.IsNullOrWhiteSpace(parent))
        {
            parent = Path.GetPathRoot(normalizedPreferred) ?? string.Empty;
        }

        var name = Path.GetFileName(normalizedPreferred.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name))
        {
            throw new IOException($"Could not resolve target name conflict for: {preferredPath}");
        }

        for (var index = 1; index <= 9999; index++)
        {
            var candidate = Path.Combine(parent, AppendConflictSuffix(name, index, isDirectory));
            if (!OccupiesPath(candidate) && !reservedTargets.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not resolve target name conflict for: {preferredPath}");
    }

    internal static string AppendConflictSuffix(string name, int index, bool isDirectory)
    {
        if (isDirectory || string.IsNullOrEmpty(Path.GetExtension(name)))
        {
            return $"{name} ({index})";
        }

        var extension = Path.GetExtension(name);
        var baseName = Path.GetFileNameWithoutExtension(name);
        return $"{baseName} ({index}){extension}";
    }

    internal static string ApplyTargetAdjustments(
        string path,
        IReadOnlyList<PathRenameOperation> targetAdjustments)
    {
        if (targetAdjustments.Count == 0)
        {
            return Path.GetFullPath(path);
        }

        var resolved = path;
        foreach (var adjustment in targetAdjustments
                     .OrderByDescending(adjustment => adjustment.SourcePath.Length)
                     .ThenBy(adjustment => adjustment.SourcePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!resolved.StartsWith(adjustment.SourcePath, StringComparison.OrdinalIgnoreCase) ||
                resolved.Length != adjustment.SourcePath.Length &&
                resolved[adjustment.SourcePath.Length] is not ('\\' or '/'))
            {
                continue;
            }

            resolved = adjustment.TargetPath + resolved[adjustment.SourcePath.Length..];
        }

        return Path.GetFullPath(resolved);
    }

    public static bool PathExistsOnDisk(string path) => OccupiesPath(path);

    private static bool OccupiesPath(string path) =>
        File.Exists(path) || Directory.Exists(path);

    internal static IReadOnlyList<PathRenameOperation> OrderForPathResolution(
        IReadOnlyList<PathRenameOperation> completed) =>
        completed
            .OrderBy(operation => operation.SourcePath.Length)
            .ThenBy(operation => operation.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static string ResolvePath(string path, IReadOnlyList<PathRenameOperation> completed) =>
        ResolvePath(path, OrderForPathResolution(completed));

    internal static string ResolvePath(string path, IReadOnlyList<PathRenameOperation> orderedCompleted, bool ordered)
    {
        if (!ordered)
        {
            return ResolvePath(path, orderedCompleted);
        }

        if (orderedCompleted.Count == 0)
        {
            return Path.GetFullPath(path);
        }

        var resolved = path;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var operation in orderedCompleted)
            {
                if (!resolved.StartsWith(operation.SourcePath, StringComparison.OrdinalIgnoreCase) ||
                    resolved.Length != operation.SourcePath.Length &&
                    resolved[operation.SourcePath.Length] is not ('\\' or '/'))
                {
                    continue;
                }

                var next = operation.TargetPath + resolved[operation.SourcePath.Length..];
                if (string.Equals(next, resolved, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                resolved = next;
                changed = true;
            }
        }

        return Path.GetFullPath(resolved);
    }

    internal sealed class PathRenameLookup
    {
        private readonly PathRenameOperation[] _orderedOperations;
        private readonly Dictionary<string, string> _exactFileTargets;

        public PathRenameLookup(IReadOnlyList<PathRenameOperation> completedOperations)
        {
            _orderedOperations = OrderForPathResolution(completedOperations).ToArray();
            _exactFileTargets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var operation in completedOperations)
            {
                if (operation.IsDirectory)
                {
                    continue;
                }

                _exactFileTargets[Path.GetFullPath(operation.SourcePath)] = operation.TargetPath;
            }
        }

        public string MapPath(string path)
        {
            var fullPath = Path.GetFullPath(path);
            if (_exactFileTargets.TryGetValue(fullPath, out var exactTarget))
            {
                return exactTarget;
            }

            return ResolvePath(fullPath, _orderedOperations, ordered: true);
        }
    }

    public static IReadOnlyList<MediaEntry> ApplyRenamedPaths(
        IReadOnlyList<MediaEntry> entries,
        PathRenameExecutionResult result,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var completed = result.CompletedOperations;
        if (completed.Count == 0)
        {
            return entries.ToList();
        }

        var lookup = new PathRenameLookup(completed);
        var updated = new List<MediaEntry>(entries.Count);
        const int reportInterval = 5000;

        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[index];

            if (index == 0 || (index + 1) % reportInterval == 0 || index == entries.Count - 1)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Updating playlist paths…",
                    Detail = $"{index + 1:N0} of {entries.Count:N0}",
                    PercentComplete = (index + 1) * 100.0 / entries.Count
                });
            }

            var mapped = lookup.MapPath(entry.AbsolutePath);
            if (string.Equals(mapped, entry.AbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                updated.Add(entry);
                continue;
            }

            updated.Add(new MediaEntry
            {
                AbsolutePath = mapped,
                StoredPath = null,
                SourceRootIndex = entry.SourceRootIndex
            });
        }

        return updated;
    }

    public static IReadOnlyList<MediaEntry> ApplyRenamedPaths(
        IEnumerable<MediaEntry> entries,
        PathRenameExecutionResult result)
    {
        var entryList = entries as IReadOnlyList<MediaEntry> ?? entries.ToList();
        return ApplyRenamedPaths(entryList, result);
    }

    public static IReadOnlyList<MediaEntry> ApplyFilePathMap(
        IEnumerable<MediaEntry> entries,
        IReadOnlyDictionary<string, string> filePathMap)
    {
        return entries
            .Select(entry =>
            {
                if (!filePathMap.TryGetValue(entry.AbsolutePath, out var mapped) ||
                    string.Equals(mapped, entry.AbsolutePath, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }

                return new MediaEntry
                {
                    AbsolutePath = mapped,
                    StoredPath = null,
                    SourceRootIndex = entry.SourceRootIndex
                };
            })
            .ToList();
    }

    public static IReadOnlyList<FolderSource> ApplyDirectoryMap(
        IEnumerable<FolderSource> folderSources,
        IReadOnlyList<PathRenameOperation> operations)
    {
        var directoryMap = operations
            .Where(operation => operation.IsDirectory)
            .ToDictionary(
                operation => operation.SourcePath,
                operation => operation.TargetPath,
                StringComparer.OrdinalIgnoreCase);

        return folderSources
            .Select(source => MapFolderSource(source, directoryMap))
            .ToList();
    }

    internal static void AddOperations(
        IDictionary<string, PathRenameOperation> operations,
        string sourcePath,
        string targetPath)
    {
        var (sourceRoot, sourceSegments) = SplitPath(sourcePath);
        var (targetRoot, targetSegments) = SplitPath(targetPath);

        for (var index = 0; index < sourceSegments.Length; index++)
        {
            if (index >= targetSegments.Length ||
                string.Equals(sourceSegments[index], targetSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isFile = index == sourceSegments.Length - 1;
            if (isFile)
            {
                operations[sourcePath] = new PathRenameOperation(sourcePath, targetPath, IsDirectory: false);
                continue;
            }

            var sourcePartial = Path.Combine([sourceRoot.TrimEnd('\\'), .. sourceSegments[..(index + 1)]]);
            var targetPartial = Path.Combine([targetRoot.TrimEnd('\\'), .. targetSegments[..(index + 1)]]);
            operations[sourcePartial] = new PathRenameOperation(sourcePartial, targetPartial, IsDirectory: true);
        }
    }

    internal static IReadOnlyList<PathRenameOperation> OrderOperations(IEnumerable<PathRenameOperation> operations) =>
        operations
            .OrderBy(operation => operation.IsDirectory ? 0 : 1)
            .ThenBy(operation => operation.SourcePath.Length)
            .ThenBy(operation => operation.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static (string Root, string[] Segments) SplitPath(string path)
    {
        var fullPath = Path.GetFullPath(AsciiPathNormalizer.NormalizeSeparators(path));
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var relative = fullPath[root.Length..].TrimStart('\\', '/');
        var segments = AsciiPathNormalizer.SplitPathSegments(relative);
        return (root, segments);
    }

    private static FolderSource MapFolderSource(
        FolderSource source,
        IReadOnlyDictionary<string, string> directoryMap)
    {
        var mapped = directoryMap
            .OrderByDescending(pair => pair.Key.Length)
            .FirstOrDefault(pair =>
                source.AbsolutePath.Equals(pair.Key, StringComparison.OrdinalIgnoreCase) ||
                source.AbsolutePath.StartsWith(pair.Key + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

        if (mapped.Key is null)
        {
            return source;
        }

        var suffix = source.AbsolutePath[mapped.Key.Length..].TrimStart('\\', '/');
        var newPath = string.IsNullOrEmpty(suffix)
            ? mapped.Value
            : Path.Combine(mapped.Value, suffix);

        if (string.Equals(newPath, source.AbsolutePath, StringComparison.OrdinalIgnoreCase))
        {
            return source;
        }

        return new FolderSource
        {
            AbsolutePath = newPath,
            IncludeSubfolders = source.IncludeSubfolders,
            UseWildcardLine = source.UseWildcardLine
        };
    }
}
