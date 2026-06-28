using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed class FolderScanner
{
    private const int ProgressReportInterval = 50;

    private readonly IReadOnlySet<string> _extensions;

    public FolderScanner(IEnumerable<string>? extensions = null)
    {
        _extensions = new HashSet<string>(
            extensions ?? DefaultExtensions,
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<string> DefaultExtensions { get; } =
    [
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".tif", ".tiff"
    ];

    public ScanResult Scan(IReadOnlyList<FolderSource> sources) =>
        Scan(sources, progress: null, cancellationToken: CancellationToken.None);

    public ScanResult Scan(
        IReadOnlyList<FolderSource> sources,
        IProgress<ScanProgressReport>? progress,
        CancellationToken cancellationToken,
        bool allowDuplicates = false)
    {
        if (sources.Count == 0)
        {
            throw new ArgumentException("At least one folder source is required.", nameof(sources));
        }

        var started = DateTime.UtcNow;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<MediaEntry>();
        var duplicates = 0;
        var emptySkipped = 0;
        var skippedEmptyPaths = new List<string>();
        var directoriesScanned = 0;
        var filesSinceReport = 0;

        void Report(int rootIndex, string? currentPath)
        {
            progress?.Report(new ScanProgressReport
            {
                FilesFound = entries.Count,
                DuplicatesSkipped = duplicates,
                DirectoriesScanned = directoriesScanned,
                RootIndex = rootIndex + 1,
                TotalRoots = sources.Count,
                CurrentPath = currentPath
            });
            filesSinceReport = 0;
        }

        Report(0, null);

        for (var rootIndex = 0; rootIndex < sources.Count; rootIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var source = sources[rootIndex];
            if (!Directory.Exists(source.AbsolutePath))
            {
                AppLog.Warning($"Folder not found, skipping: {source.AbsolutePath}");
                continue;
            }

            AppLog.Info($"Scanning root [{rootIndex + 1}/{sources.Count}]: {source.AbsolutePath} (recursive={source.IncludeSubfolders})");

            var files = CollectFiles(source.AbsolutePath, source.IncludeSubfolders, cancellationToken, ref directoriesScanned, path =>
            {
                filesSinceReport++;
                if (filesSinceReport >= ProgressReportInterval)
                {
                    Report(rootIndex, path);
                }
            });

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = Path.GetFullPath(file);
                if (MediaFileHealthChecker.IsEmptyFile(fullPath))
                {
                    emptySkipped++;
                    skippedEmptyPaths.Add(fullPath);
                    continue;
                }

                if (!allowDuplicates)
                {
                    if (!seen.Add(fullPath))
                    {
                        duplicates++;
                        continue;
                    }
                }

                entries.Add(new MediaEntry
                {
                    AbsolutePath = fullPath,
                    SourceRootIndex = rootIndex
                });

                filesSinceReport++;
                if (filesSinceReport >= ProgressReportInterval)
                {
                    Report(rootIndex, fullPath);
                }
            }

            Report(rootIndex, source.AbsolutePath);
        }

        var duration = DateTime.UtcNow - started;
        AppLog.Info($"Scan complete: {entries.Count} files, {duplicates} duplicates skipped, {emptySkipped} empty skipped, {directoriesScanned} directories, {duration.TotalSeconds:F2}s");

        return new ScanResult
        {
            Entries = entries,
            DuplicatesSkipped = duplicates,
            EmptyFilesSkipped = emptySkipped,
            SkippedEmptyPaths = skippedEmptyPaths,
            DirectoriesScanned = directoriesScanned,
            Duration = duration
        };
    }

    private List<string> CollectFiles(
        string root,
        bool recursive,
        CancellationToken cancellationToken,
        ref int directoriesScanned,
        Action<string>? onDirectoryScanned)
    {
        var results = new List<string>();

        IEnumerable<string> directories = recursive
            ? Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).Prepend(root)
            : [root];

        foreach (var directory in directories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            directoriesScanned++;
            onDirectoryScanned?.Invoke(directory);

            try
            {
                var files = Directory.EnumerateFiles(directory)
                    .Where(IsSupportedImage)
                    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
                results.AddRange(files);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Failed to read directory: {directory}", ex);
            }
        }

        return results;
    }

    private bool IsSupportedImage(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension) && _extensions.Contains(extension);
    }
}
