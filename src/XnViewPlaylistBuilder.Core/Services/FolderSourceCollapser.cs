using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class FolderSourceCollapser
{
    public sealed record Result(IReadOnlyList<FolderSource> Roots, int CollapsedCount);

    public static Result Collapse(IEnumerable<FolderSource> sources)
    {
        var items = sources.ToList();
        if (items.Count == 0)
        {
            return new Result([], 0);
        }

        var ordered = items
            .Select(source => (Source: source, Path: NormalizePath(source.AbsolutePath)))
            .OrderBy(item => item.Path.Length)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roots = new List<RootAccumulator>();

        foreach (var item in ordered)
        {
            var ancestor = roots.FirstOrDefault(root =>
                root.Source.IncludeSubfolders &&
                IsStrictAncestor(root.Path, item.Path));

            if (ancestor is not null)
            {
                ancestor.AddCollapsed(item.Source);
                continue;
            }

            roots.Add(new RootAccumulator(item.Source, item.Path));
        }

        var collapsedCount = items.Count - roots.Count;
        return new Result(roots.Select(root => root.ToFolderSource()).ToList(), collapsedCount);
    }

    internal static bool IsStrictAncestor(string ancestorPath, string descendantPath)
    {
        var ancestor = NormalizePath(ancestorPath);
        var descendant = NormalizePath(descendantPath);
        if (string.Equals(ancestor, descendant, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return descendant.StartsWith(ancestor + "\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return PathKeyNormalizer.Normalize(Path.GetFullPath(path));
        }
        catch
        {
            return PathKeyNormalizer.Normalize(path);
        }
    }

    private sealed class RootAccumulator
    {
        private bool _wildcardFromCollapsed;

        public RootAccumulator(FolderSource source, string path)
        {
            Source = source;
            Path = path;
        }

        public FolderSource Source { get; }
        public string Path { get; }
        public List<string> CollapsedPaths { get; } = [];

        public void AddCollapsed(FolderSource nested)
        {
            CollapsedPaths.Add(nested.AbsolutePath);
            if (nested.CollapsedSubfolderPaths.Count > 0)
            {
                CollapsedPaths.AddRange(nested.CollapsedSubfolderPaths);
            }

            if (nested.UseWildcardLine)
            {
                _wildcardFromCollapsed = true;
            }
        }

        public FolderSource ToFolderSource()
        {
            var distinctCollapsed = CollapsedPaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new FolderSource
            {
                AbsolutePath = Source.AbsolutePath,
                IncludeSubfolders = Source.IncludeSubfolders,
                UseWildcardLine = Source.UseWildcardLine || _wildcardFromCollapsed,
                CollapsedSubfolderPaths = distinctCollapsed
            };
        }
    }
}
