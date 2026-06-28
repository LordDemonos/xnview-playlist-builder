namespace XnViewPlaylistBuilder.Core.Services;

public enum ScanRootSuggestionKind
{
    SubjectFolder,
    BroaderFolder
}

public sealed record ScanRootSuggestion(
    string FolderPath,
    int RemovedFileCount,
    ScanRootSuggestionKind Kind)
{
    public bool IsSelectedByDefault => Kind == ScanRootSuggestionKind.SubjectFolder;

    public string KindLabel => Kind switch
    {
        ScanRootSuggestionKind.SubjectFolder => "Subject folder (immediate parent)",
        ScanRootSuggestionKind.BroaderFolder => "Broader parent folder",
        _ => Kind.ToString()
    };
}

public static class RemovedPathScanRootSuggester
{
    public static IReadOnlyList<ScanRootSuggestion> Suggest(
        IEnumerable<string> removedFilePaths,
        int maxSuggestions = 40)
    {
        var filePaths = removedFilePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (filePaths.Count == 0)
        {
            return [];
        }

        var subjectCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ancestorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in filePaths)
        {
            var parent = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(parent))
            {
                continue;
            }

            parent = Path.GetFullPath(parent);
            subjectCounts[parent] = subjectCounts.GetValueOrDefault(parent) + 1;

            var ancestor = Path.GetDirectoryName(parent);
            while (!string.IsNullOrWhiteSpace(ancestor))
            {
                ancestor = Path.GetFullPath(ancestor);
                var root = Path.GetPathRoot(ancestor);
                if (string.IsNullOrWhiteSpace(root) ||
                    string.Equals(ancestor.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                ancestorCounts[ancestor] = ancestorCounts.GetValueOrDefault(ancestor) + 1;
                ancestor = Path.GetDirectoryName(ancestor);
            }
        }

        var suggestions = new List<ScanRootSuggestion>();

        suggestions.AddRange(subjectCounts
            .Select(pair => new ScanRootSuggestion(pair.Key, pair.Value, ScanRootSuggestionKind.SubjectFolder))
            .OrderByDescending(suggestion => suggestion.RemovedFileCount)
            .ThenBy(suggestion => suggestion.FolderPath, StringComparer.OrdinalIgnoreCase));

        suggestions.AddRange(ancestorCounts
            .Where(pair => !subjectCounts.ContainsKey(pair.Key))
            .Select(pair => new ScanRootSuggestion(pair.Key, pair.Value, ScanRootSuggestionKind.BroaderFolder))
            .OrderByDescending(suggestion => GetDepth(suggestion.FolderPath))
            .ThenByDescending(suggestion => suggestion.RemovedFileCount)
            .ThenBy(suggestion => suggestion.FolderPath, StringComparer.OrdinalIgnoreCase));

        return suggestions
            .Take(maxSuggestions)
            .ToList();
    }

    internal static int GetDepth(string folderPath)
    {
        var root = Path.GetPathRoot(folderPath) ?? string.Empty;
        var relative = folderPath[root.Length..].Trim('\\', '/');
        if (string.IsNullOrEmpty(relative))
        {
            return 0;
        }

        return relative.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
