using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class EntryMerge
{
    public static IReadOnlyList<MediaEntry> Merge(
        IReadOnlyList<MediaEntry> existing,
        IReadOnlyList<MediaEntry> added,
        bool allowDuplicates = false)
    {
        if (allowDuplicates)
        {
            var combined = new List<MediaEntry>(existing.Count + added.Count);
            combined.AddRange(existing);
            combined.AddRange(added);
            return combined;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<MediaEntry>(existing.Count + added.Count);

        foreach (var entry in existing.Concat(added))
        {
            var key = EntryKey(entry);
            if (!seen.Add(key))
            {
                continue;
            }

            merged.Add(entry);
        }

        return merged;
    }

    public static string EntryKey(MediaEntry entry) =>
        PathKeyNormalizer.Normalize(entry.AbsolutePath);
}
