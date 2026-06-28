using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed class PlaylistService
{
    private readonly FolderScanner _scanner;
    private readonly SldWriterV2 _writer;
    private readonly SldReaderV2 _reader;

    public PlaylistService(
        FolderScanner? scanner = null,
        SldWriterV2? writer = null,
        SldReaderV2? reader = null)
    {
        _scanner = scanner ?? new FolderScanner();
        _writer = writer ?? new SldWriterV2();
        _reader = reader ?? new SldReaderV2();
    }

    public SldPlaylist LoadPlaylist(string filePath) => _reader.Read(filePath);

    public ScanResult ScanFolders(
        IReadOnlyList<FolderSource> sources,
        IProgress<ScanProgressReport>? progress = null,
        CancellationToken cancellationToken = default,
        bool allowDuplicates = false)
    {
        var scannable = sources.Where(source => !source.UseWildcardLine).ToArray();
        if (scannable.Length == 0)
        {
            throw new InvalidOperationException("No scannable folders. Uncheck Wildcard (*.*) or add folders without wildcard lines.");
        }

        return _scanner.Scan(scannable, progress, cancellationToken, allowDuplicates);
    }

    public IReadOnlyList<MediaEntry> MergeEntries(
        IReadOnlyList<MediaEntry> existing,
        IReadOnlyList<MediaEntry> scanned,
        bool allowDuplicates = false) =>
        EntryMerge.Merge(existing, scanned, allowDuplicates);

    public void SavePlaylist(
        string outputPath,
        SldOptionsV2 options,
        IReadOnlyList<MediaEntry> entries,
        PathPolicy pathPolicy,
        IReadOnlyList<FolderSource>? folderSources = null,
        string? anchorPath = null,
        bool useXnViewRelativePathsForUnicode = false)
    {
        AppLog.Info($"Saving playlist to {outputPath} with policy {pathPolicy}");
        var paths = BuildSavePaths(entries, folderSources, pathPolicy, outputPath, anchorPath, useXnViewRelativePathsForUnicode);
        SavePlaylist(outputPath, options, paths);
    }

    public void SavePlaylist(
        string outputPath,
        SldOptionsV2 options,
        IReadOnlyList<string> serializedPaths)
    {
        AppLog.Info($"Saving playlist to {outputPath} ({serializedPaths.Count} path(s))");
        _writer.Write(outputPath, options, serializedPaths);
    }

    public IReadOnlyList<string> BuildSavePaths(
        IReadOnlyList<MediaEntry> entries,
        IReadOnlyList<FolderSource>? folderSources,
        PathPolicy pathPolicy,
        string outputPath,
        string? anchorPath,
        bool useXnViewRelativePathsForUnicode = false)
    {
        var fileEntries = entries
            .Where(entry => !IsWildcardEntry(entry))
            .ToList();

        var paths = new List<string>();
        if (fileEntries.Count > 0)
        {
            paths.AddRange(_writer.SerializePaths(fileEntries, pathPolicy, outputPath, anchorPath, useXnViewRelativePathsForUnicode));
        }
        var wildcardLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries.Where(IsWildcardEntry))
        {
            wildcardLines.Add(entry.StoredPath ?? entry.AbsolutePath);
        }

        if (folderSources is not null)
        {
            foreach (var source in folderSources.Where(source => source.UseWildcardLine))
            {
                wildcardLines.Add(WildcardPathFormatter.ToWildcardLine(
                    source.AbsolutePath,
                    pathPolicy,
                    outputPath,
                    anchorPath));
            }
        }

        paths.AddRange(wildcardLines);
        if (paths.Count == 0)
        {
            throw new InvalidOperationException("No media paths available to write.");
        }

        return paths;
    }

    private static bool IsWildcardEntry(MediaEntry entry)
    {
        var path = entry.StoredPath ?? entry.AbsolutePath;
        return WildcardPathFormatter.IsWildcardPath(path);
    }
}
