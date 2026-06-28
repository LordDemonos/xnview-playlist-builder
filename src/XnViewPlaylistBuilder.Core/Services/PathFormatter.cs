using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class PathFormatter
{
    public static string FormatForSld(
        string absolutePath,
        PathPolicy policy,
        string? outputSldPath,
        string? anchorPath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("Path is required.", nameof(absolutePath));
        }

        var normalized = NormalizeSeparators(absolutePath);

        return policy switch
        {
            PathPolicy.AbsoluteLocal => FormatAbsoluteLocal(normalized),
            PathPolicy.AbsoluteUnc => FormatAbsoluteUnc(normalized),
            PathPolicy.RelativeToSld => FormatRelative(normalized, RequireDirectory(outputSldPath, nameof(outputSldPath))),
            PathPolicy.RelativeToAnchor => FormatRelative(normalized, RequireDirectory(anchorPath, nameof(anchorPath))),
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unknown path policy.")
        };
    }

    public static string NormalizeSeparators(string path) =>
        path.Replace('/', '\\');

    public static string QuotePath(string path)
    {
        if (path.Contains('"', StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path contains unsupported quote character: {path}");
        }

        return $"\"{path}\"";
    }

    private static string FormatAbsoluteLocal(string path)
    {
        if (IsUnc(path))
        {
            return path;
        }

        return Path.GetFullPath(path);
    }

    private static string FormatAbsoluteUnc(string path)
    {
        if (IsUnc(path))
        {
            return path;
        }

        return Path.GetFullPath(path);
    }

    private static string FormatRelative(string absolutePath, string baseDirectory)
    {
        var fullBase = Path.GetFullPath(baseDirectory);
        var fullPath = Path.GetFullPath(absolutePath);
        var relative = Path.GetRelativePath(fullBase, fullPath);
        return NormalizeSeparators(relative);
    }

    private static string RequireDirectory(string? path, string paramName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{paramName} is required for this path policy.");
        }

        var directory = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found for {paramName}: {path}");
        }

        return directory;
    }

    private static bool IsUnc(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal);
}
