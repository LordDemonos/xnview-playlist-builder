namespace XnViewPlaylistBuilder.Core.Services;

public static class BrowseFolderHelper
{
    public static string GetNextBrowseDirectory(IEnumerable<string> addedFolderPaths)
    {
        var folders = addedFolderPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();

        if (folders.Length == 0)
        {
            throw new ArgumentException("At least one folder path is required.", nameof(addedFolderPaths));
        }

        var parents = folders
            .Select(Path.GetDirectoryName)
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (parents.Length == 1)
        {
            return parents[0];
        }

        return GetCommonDirectory(folders) ?? parents[0];
    }

    private static string? GetCommonDirectory(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return null;
        }

        var segments = paths
            .Select(path => path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .ToArray();

        var commonCount = 0;
        var first = segments[0];

        for (var i = 0; i < first.Length; i++)
        {
            if (segments.Any(parts => parts.Length <= i || !parts[i].Equals(first[i], StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            commonCount++;
        }

        if (commonCount == 0)
        {
            return null;
        }

        var prefix = string.Join(Path.DirectorySeparatorChar, first.Take(commonCount));
        if (prefix.Length == 2 && prefix[1] == ':')
        {
            return prefix + Path.DirectorySeparatorChar;
        }

        return prefix;
    }
}
