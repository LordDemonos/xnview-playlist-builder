namespace XnViewPlaylistBuilder.Core.Services;

internal static class PathKeyNormalizer
{
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Trim().Replace('/', '\\').TrimEnd('\\');
    }

    public static bool AreEquivalent(string? left, string? right) =>
        string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
}
