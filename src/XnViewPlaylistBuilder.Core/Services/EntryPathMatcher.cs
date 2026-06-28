using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class EntryPathMatcher
{
    public static bool IsUnderFolder(MediaEntry entry, string folderPath) =>
        IsUnderFolder(entry, folderPath, folderPathPreNormalized: false);

    public static bool IsUnderFolder(MediaEntry entry, string folderPath, bool folderPathPreNormalized)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return false;
        }

        var normalizedFolder = folderPathPreNormalized
            ? folderPath
            : NormalizeDirectory(folderPath);

        foreach (var candidate in GetPathCandidates(entry))
        {
            if (IsUnderDirectory(candidate, normalizedFolder))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<MediaEntry> CollectUnderFolders(
        IEnumerable<MediaEntry> entries,
        IEnumerable<string> folderPaths)
    {
        var normalizedFolders = folderPaths
            .Select(NormalizeDirectory)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedFolders.Length == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<MediaEntry>();

        foreach (var entry in entries)
        {
            if (!IsUnderAnyFolder(entry, normalizedFolders))
            {
                continue;
            }

            if (seen.Add(EntryMerge.EntryKey(entry)))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static bool IsUnderAnyFolder(MediaEntry entry, string[] normalizedFolders)
    {
        foreach (var folder in normalizedFolders)
        {
            if (IsUnderFolder(entry, folder, folderPathPreNormalized: true))
            {
                return true;
            }
        }

        return false;
    }

    public static IReadOnlyList<MediaEntry> FilterUnderFolder(
        IEnumerable<MediaEntry> entries,
        string folderPath) =>
        entries.Where(entry => IsUnderFolder(entry, folderPath)).ToArray();

    private static IEnumerable<string> GetPathCandidates(MediaEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.StoredPath))
        {
            yield return entry.StoredPath;
        }

        if (!string.IsNullOrWhiteSpace(entry.AbsolutePath))
        {
            yield return entry.AbsolutePath;
        }
    }

    private static bool IsUnderDirectory(string path, string normalizedFolder)
    {
        var normalizedPath = path.Replace('/', '\\');
        if (WildcardPathFormatter.IsWildcardPath(normalizedPath))
        {
            var folderPart = normalizedPath[..normalizedPath.IndexOf('*', StringComparison.Ordinal)].TrimEnd('\\');
            return PathsEqualOrNested(normalizedFolder, NormalizeDirectory(folderPart));
        }

        if (Path.IsPathRooted(normalizedPath))
        {
            try
            {
                var trimmed = normalizedPath.TrimEnd('\\', '/');
                if (PathsEqualOrNested(normalizedFolder, NormalizeDirectory(trimmed)))
                {
                    return true;
                }

                var parent = Path.GetDirectoryName(trimmed);
                return parent is not null &&
                       PathsEqualOrNested(normalizedFolder, NormalizeDirectory(parent));
            }
            catch
            {
                return false;
            }
        }

        return normalizedPath.StartsWith(normalizedFolder.TrimStart('\\'), StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.Contains(normalizedFolder, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqualOrNested(string rootFolder, string candidateFolder) =>
        candidateFolder.Equals(rootFolder, StringComparison.OrdinalIgnoreCase) ||
        candidateFolder.StartsWith(rootFolder + "\\", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeDirectory(string path) =>
        PathKeyNormalizer.Normalize(path);
}
