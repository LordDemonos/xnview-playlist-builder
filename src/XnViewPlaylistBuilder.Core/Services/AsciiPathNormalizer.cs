using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace XnViewPlaylistBuilder.Core.Services;

public static partial class AsciiPathNormalizer
{
    private static readonly HashSet<char> InvalidFileNameChars = Path.GetInvalidFileNameChars().ToHashSet();

    public static bool NeedsNormalization(string path) =>
        path.Any(static ch => ch > 127) || ContainsMojibake(path);

    public static bool SegmentNeedsNormalization(string segment) =>
        segment.Any(static ch => ch > 127) || ContainsMojibake(segment);

    public static bool IsAsciiOnly(string value) =>
        value.All(static ch => ch <= 127);

    public static bool ContainsMojibake(string value) =>
        value.Contains("ã", StringComparison.Ordinal) ||
        value.Contains('Ã') ||
        value.Contains('â', StringComparison.Ordinal);

    public static string? TryDecodeMojibake(string value)
    {
        if (!ContainsMojibake(value))
        {
            return null;
        }

        try
        {
            var bytes = Encoding.GetEncoding(1252).GetBytes(value);
            var recovered = Encoding.UTF8.GetString(bytes);
            if (recovered.Any(static ch => ch > 127) && !ContainsMojibake(recovered))
            {
                return recovered;
            }
        }
        catch
        {
            // Fall through to manual ASCII cleanup.
        }

        return null;
    }

    public static string ToAsciiSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return "renamed";
        }

        if (!SegmentNeedsNormalization(segment))
        {
            return segment;
        }

        var working = TryDecodeMojibake(segment) ?? segment;
        working = NormalizeDashes(working);
        working = BracketPrefixPattern().Replace(working, "$1-");
        working = ApplyParentheticalHint(working);
        working = StripToAscii(working);
        working = CollapseSeparators(working, collapseSpaces: true).Trim(' ', '.', '-', '_');

        if (string.IsNullOrWhiteSpace(working))
        {
            return "renamed";
        }

        return working;
    }

    public static string ToAsciiPath(string absolutePath)
    {
        var fullPath = Path.GetFullPath(NormalizeSeparators(absolutePath));
        var root = Path.GetPathRoot(fullPath) ?? string.Empty;
        var relative = fullPath[root.Length..].TrimStart('\\', '/');
        var asciiSegments = SplitPathSegments(relative).Select(ToAsciiSegment).ToArray();
        return Path.Combine([root.TrimEnd('\\'), .. asciiSegments]);
    }

    internal static string NormalizeSeparators(string path) =>
        path.Replace('/', '\\');

    internal static string[] SplitPathSegments(string relativePath)
    {
        var segments = new List<string>();

        foreach (var part in relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains('\\') || part.Contains('/'))
            {
                foreach (var nested in part.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    segments.Add(nested);
                }
            }
            else
            {
                segments.Add(part);
            }
        }

        return segments.ToArray();
    }

    private static string NormalizeDashes(string value) =>
        value.Replace('–', '-').Replace('—', '-').Replace('−', '-');

    private static string ApplyParentheticalHint(string value)
    {
        var match = ParentheticalPattern().Match(value);
        if (!match.Success)
        {
            return value;
        }

        var before = match.Groups[1].Value.TrimEnd();
        var label = match.Groups[2].Value.Trim();
        var after = match.Groups[3].Value;

        if (!label.All(static ch => ch >= 32 && ch <= 126))
        {
            return value;
        }

        var leadMatch = LeadingAsciiTokenPattern().Match(before);
        var lead = leadMatch.Success ? leadMatch.Value : string.Empty;
        var head = string.IsNullOrEmpty(lead) ? label : $"{lead}-{label}";
        return $"{head}{after}";
    }

    private static string StripToAscii(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var ch in normalized)
        {
            if (char.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch <= 127 && !InvalidFileNameChars.Contains(ch))
            {
                builder.Append(ch);
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString();
    }

    private static string CollapseSeparators(string value, bool collapseSpaces)
    {
        if (collapseSpaces)
        {
            var collapsed = Regex.Replace(value, @"[\s\-_]+", "-");
            return Regex.Replace(collapsed, @"-{2,}", "-");
        }

        // Already-ASCII names: keep spaces and underscores; only normalize hyphen runs.
        return Regex.Replace(value, @"-{2,}", "-");
    }

    [GeneratedRegex(@"\[([^\]]+)\]")]
    private static partial Regex BracketPrefixPattern();

    [GeneratedRegex(@"^(.*)\(([^)]+)\)(.*)$")]
    private static partial Regex ParentheticalPattern();

    [GeneratedRegex(@"^[A-Za-z0-9]+")]
    private static partial Regex LeadingAsciiTokenPattern();
}
