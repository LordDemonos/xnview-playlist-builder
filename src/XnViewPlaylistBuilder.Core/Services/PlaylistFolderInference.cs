using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class PlaylistFolderInference
{
    public static IReadOnlyList<string> InferUniqueDirectories(IReadOnlyList<MediaEntry> entries)
    {
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var directory = GetDirectoryPath(entry);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                directories.Add(directory);
            }
        }

        return directories.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string? GetDirectoryPath(MediaEntry entry)
    {
        var path = entry.StoredPath ?? entry.AbsolutePath;
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var normalized = path.Replace('/', '\\');
        if (WildcardPathFormatter.IsWildcardPath(normalized))
        {
            var folderPart = normalized[..normalized.IndexOf('*', StringComparison.Ordinal)].TrimEnd('\\');
            return string.IsNullOrWhiteSpace(folderPart) ? null : folderPart;
        }

        var directory = Path.GetDirectoryName(normalized);
        return string.IsNullOrWhiteSpace(directory) ? null : directory;
    }
}
