using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class SldPathSerializer
{
    /// <summary>
    /// XnView MP 1.9.x resolves non-ASCII paths reliably when they are relative to the .sld file,
    /// matching its own save format (see officialexample.sld).
    /// </summary>
    public static string ToStoredPath(
        string path,
        string? outputSldPath = null,
        PathPolicy policy = PathPolicy.AbsoluteLocal,
        bool useXnViewRelativePathsForUnicode = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var normalized = path.Replace('/', '\\');
        if (!Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        var fullPath = Path.GetFullPath(normalized);
        if (useXnViewRelativePathsForUnicode &&
            ShouldUseXnViewRelativePath(fullPath, outputSldPath, policy))
        {
            var sldDirectory = Path.GetDirectoryName(Path.GetFullPath(outputSldPath!))!;
            var relative = PathFormatter.NormalizeSeparators(Path.GetRelativePath(sldDirectory, fullPath));
            AppLog.Info($"Using XnView-relative path: {relative}");
            return relative;
        }

        if (SldFileEncoding.IsRepresentableInSystemEncoding(fullPath))
        {
            return fullPath;
        }

        var shortPath = WindowsShortPath.TryGetShortPath(fullPath);
        if (shortPath is not null &&
            SldFileEncoding.IsRepresentableInSystemEncoding(shortPath) &&
            File.Exists(shortPath))
        {
            AppLog.Info($"Using short path for XnView compatibility: {shortPath}");
            return shortPath;
        }

        return fullPath;
    }

    internal static bool ShouldUseXnViewRelativePath(
        string fullPath,
        string? outputSldPath,
        PathPolicy policy)
    {
        if (policy is PathPolicy.RelativeToSld or PathPolicy.RelativeToAnchor)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(outputSldPath) || !fullPath.Any(static ch => ch > 127))
        {
            return false;
        }

        var sldDirectory = Path.GetDirectoryName(Path.GetFullPath(outputSldPath));
        if (string.IsNullOrWhiteSpace(sldDirectory))
        {
            return false;
        }

        var sldRoot = Path.GetFullPath(sldDirectory);
        if (!fullPath.StartsWith(sldRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = Path.GetRelativePath(sldRoot, fullPath);
        return !relative.StartsWith("..", StringComparison.Ordinal);
    }
}
