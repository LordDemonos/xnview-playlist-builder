namespace XnViewPlaylistBuilder.Core.Services;

using XnViewPlaylistBuilder.Core.Models;

public enum MediaFileHealthIssue
{
    Missing,
    Empty,
    InvalidImageHeader
}

public sealed class MediaFileHealthReport
{
    public static MediaFileHealthReport Empty { get; } = new();

    public int TotalChecked { get; init; }
    public IReadOnlyList<MediaFileHealthFinding> Findings { get; init; } = [];

    public int HealthyCount => TotalChecked - Findings.Count;

    public bool HasIssues => Findings.Count > 0;

    public int MissingCount => CountIssue(MediaFileHealthIssue.Missing);

    public int EmptyCount => CountIssue(MediaFileHealthIssue.Empty);

    public int InvalidImageCount => CountIssue(MediaFileHealthIssue.InvalidImageHeader);

    public int UnplayableCount => EmptyCount + InvalidImageCount;

    public string FormatSummary() =>
        HasIssues
            ? $"{UnplayableCount:N0} unplayable ({EmptyCount:N0} empty, {InvalidImageCount:N0} invalid)" +
              (MissingCount > 0 ? $", {MissingCount:N0} missing" : string.Empty)
            : $"{TotalChecked:N0} files look readable";

    public string FormatDetail(int maxExamples = 8)
    {
        if (!HasIssues)
        {
            return string.Empty;
        }

        var lines = new List<string>
        {
            $"{Findings.Count:N0} of {TotalChecked:N0} playlist file(s) may not display in XnView MP.",
            "Empty or invalid image files are skipped silently during slideshow playback."
        };

        AppendIssueSection(lines, MediaFileHealthIssue.Empty, "Empty files (0 bytes)", maxExamples);
        AppendIssueSection(lines, MediaFileHealthIssue.InvalidImageHeader, "Invalid image headers", maxExamples);
        AppendIssueSection(lines, MediaFileHealthIssue.Missing, "Missing files", maxExamples);

        lines.Add(string.Empty);
        lines.Add("Use Check files to review the list, delete empty files, or export a report.");

        return string.Join(Environment.NewLine, lines);
    }

    private void AppendIssueSection(
        List<string> lines,
        MediaFileHealthIssue issue,
        string title,
        int maxExamples)
    {
        var matches = Findings.Where(finding => finding.Issue == issue).ToList();
        if (matches.Count == 0)
        {
            return;
        }

        lines.Add(string.Empty);
        lines.Add($"{title}: {matches.Count:N0}");
        foreach (var finding in matches.Take(maxExamples))
        {
            lines.Add(finding.Path);
        }

        var remaining = matches.Count - maxExamples;
        if (remaining > 0)
        {
            lines.Add($"… and {remaining:N0} more");
        }
    }

    private int CountIssue(MediaFileHealthIssue issue) =>
        Findings.Count(finding => finding.Issue == issue);
}

public sealed record MediaFileHealthFinding(
    string Path,
    MediaFileHealthIssue Issue,
    long? SizeBytes,
    string Detail);

public sealed class EmptyFileDeleteResult
{
    public int DeletedCount { get; init; }
    public IReadOnlyList<string> FailedPaths { get; init; } = [];
}

public static class MediaFileHealthChecker
{
    public static MediaFileHealthReport Analyze(
        IEnumerable<string> absolutePaths,
        IEnumerable<string>? imageExtensions = null,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var extensions = BuildExtensionSet(imageExtensions);
        var paths = absolutePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var findings = new List<MediaFileHealthFinding>();
        const int reportInterval = 250;
        for (var index = 0; index < paths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = paths[index];
            if (index == 0 || (index + 1) % reportInterval == 0 || index == paths.Count - 1)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Checking file health…",
                    Detail = paths.Count > 0 ? $"{index + 1:N0} of {paths.Count:N0}" : null,
                    PercentComplete = paths.Count > 0 ? (index + 1) * 100.0 / paths.Count : null
                });
            }

            var finding = InspectPath(path, extensions);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        return new MediaFileHealthReport
        {
            TotalChecked = paths.Count,
            Findings = findings
                .OrderBy(finding => finding.Issue)
                .ThenBy(finding => finding.Path, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    public static bool IsEmptyFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }

        return new FileInfo(path).Length == 0;
    }

    public static EmptyFileDeleteResult DeleteEmptyFiles(
        IEnumerable<string> paths,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var pathList = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var deleted = 0;
        var failed = new List<string>();
        const int reportInterval = 100;

        for (var index = 0; index < pathList.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = pathList[index];

            if (index == 0 || (index + 1) % reportInterval == 0 || index == pathList.Count - 1)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Deleting empty files…",
                    Detail = pathList.Count > 0 ? $"{index + 1:N0} of {pathList.Count:N0}" : null,
                    PercentComplete = pathList.Count > 0 ? (index + 1) * 100.0 / pathList.Count : null
                });
            }

            if (!File.Exists(path))
            {
                deleted++;
                continue;
            }

            if (new FileInfo(path).Length != 0)
            {
                failed.Add(path);
                continue;
            }

            try
            {
                File.Delete(path);
                deleted++;
            }
            catch
            {
                failed.Add(path);
            }
        }

        return new EmptyFileDeleteResult
        {
            DeletedCount = deleted,
            FailedPaths = failed
        };
    }

    public static string FormatPathExamples(IEnumerable<string> paths, int maxExamples = 8)
    {
        var examples = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (examples.Count == 0)
        {
            return string.Empty;
        }

        var lines = examples.Take(maxExamples).ToList();
        var detail = string.Join(Environment.NewLine, lines);
        var remaining = examples.Count - maxExamples;
        if (remaining > 0)
        {
            detail += $"{Environment.NewLine}… and {remaining:N0} more";
        }

        return detail;
    }

    internal static MediaFileHealthFinding? InspectPath(string path, IReadOnlySet<string> extensions)
    {
        if (!File.Exists(path))
        {
            return new MediaFileHealthFinding(path, MediaFileHealthIssue.Missing, null, "File not found");
        }

        var info = new FileInfo(path);
        if (info.Length == 0)
        {
            return new MediaFileHealthFinding(path, MediaFileHealthIssue.Empty, 0, "0 bytes");
        }

        var extension = Path.GetExtension(path);
        if (!extensions.Contains(extension))
        {
            return null;
        }

        if (!HasValidImageHeader(path, extension))
        {
            return new MediaFileHealthFinding(
                path,
                MediaFileHealthIssue.InvalidImageHeader,
                info.Length,
                "File does not start with a recognized image signature");
        }

        return null;
    }

    internal static bool HasValidImageHeader(string path, string extension)
    {
        Span<byte> header = stackalloc byte[12];
        var read = ReadHeaderBytes(path, header);
        if (read == 0)
        {
            return false;
        }

        header = header[..read];
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => header.Length >= 3 &&
                                 header[0] == 0xFF &&
                                 header[1] == 0xD8 &&
                                 header[2] == 0xFF,
            ".png" => header.Length >= 8 &&
                      header[0] == 0x89 &&
                      header[1] == 0x50 &&
                      header[2] == 0x4E &&
                      header[3] == 0x47 &&
                      header[4] == 0x0D &&
                      header[5] == 0x0A &&
                      header[6] == 0x1A &&
                      header[7] == 0x0A,
            ".gif" => header.Length >= 6 &&
                      header[0] == (byte)'G' &&
                      header[1] == (byte)'I' &&
                      header[2] == (byte)'F' &&
                      header[3] == (byte)'8' &&
                      (header[4] == (byte)'7' || header[4] == (byte)'9') &&
                      header[5] == (byte)'a',
            ".webp" => header.Length >= 12 &&
                       header[0] == (byte)'R' &&
                       header[1] == (byte)'I' &&
                       header[2] == (byte)'F' &&
                       header[3] == (byte)'F' &&
                       header[8] == (byte)'W' &&
                       header[9] == (byte)'E' &&
                       header[10] == (byte)'B' &&
                       header[11] == (byte)'P',
            ".bmp" => header.Length >= 2 &&
                      header[0] == (byte)'B' &&
                      header[1] == (byte)'M',
            ".tif" or ".tiff" => header.Length >= 4 &&
                                 ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
                                  (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)),
            _ => true
        };
    }

    private static int ReadHeaderBytes(string path, Span<byte> buffer)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        return stream.Read(buffer);
    }

    private static HashSet<string> BuildExtensionSet(IEnumerable<string>? imageExtensions) =>
        new(
            imageExtensions ?? FolderScanner.DefaultExtensions,
            StringComparer.OrdinalIgnoreCase);
}
