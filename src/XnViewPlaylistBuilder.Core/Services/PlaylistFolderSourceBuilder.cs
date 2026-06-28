using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class PlaylistFolderSourceBuilder
{
    public static IReadOnlyList<FolderSource> Build(
        IReadOnlyList<MediaEntry> entries,
        bool defaultIncludeSubfolders)
    {
        var wildcardFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var path = entry.StoredPath ?? entry.AbsolutePath;
            if (!WildcardPathFormatter.IsWildcardPath(path))
            {
                continue;
            }

            var directory = PlaylistFolderInference.GetDirectoryPath(entry);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                wildcardFolders.Add(Path.GetFullPath(directory));
            }
        }

        return FolderSourceCollapser.Collapse(
                PlaylistFolderInference.InferUniqueDirectories(entries)
                    .Select(folder =>
                    {
                        try
                        {
                            var fullPath = Path.GetFullPath(folder);
                            return new FolderSource
                            {
                                AbsolutePath = fullPath,
                                IncludeSubfolders = defaultIncludeSubfolders,
                                UseWildcardLine = wildcardFolders.Contains(fullPath)
                            };
                        }
                        catch
                        {
                            return new FolderSource
                            {
                                AbsolutePath = folder,
                                IncludeSubfolders = defaultIncludeSubfolders,
                                UseWildcardLine = wildcardFolders.Contains(folder)
                            };
                        }
                    }))
            .Roots;
    }
}
