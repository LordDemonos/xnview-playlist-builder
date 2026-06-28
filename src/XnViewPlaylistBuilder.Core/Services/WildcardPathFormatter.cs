using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class WildcardPathFormatter
{
    public const string WildcardSuffix = "\\*.*";

    public static bool IsWildcardPath(string path) =>
        path.Contains('*', StringComparison.Ordinal);

    public static string ToWildcardLine(
        string folderPath,
        PathPolicy policy,
        string? outputSldPath,
        string? anchorPath)
    {
        var formatted = PathFormatter.FormatForSld(folderPath, policy, outputSldPath, anchorPath);
        return formatted.TrimEnd('\\', '/') + WildcardSuffix;
    }
}
