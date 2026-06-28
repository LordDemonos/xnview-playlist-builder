using System.Globalization;
using System.Text.RegularExpressions;

namespace XnViewPlaylistBuilder.Core.Logging;

public static partial class RenameLogParser
{
    private const string RenamedPrefix = "Renamed: ";
    private const string ArrowSeparator = " -> ";

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3}) \[[A-Z]+\] ", RegexOptions.CultureInvariant)]
    private static partial Regex LogLineHeaderRegex();

    public static IReadOnlyList<RenameLogEntry> ParseFile(string logFilePath)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            throw new ArgumentException("Log file path is required.", nameof(logFilePath));
        }

        if (!File.Exists(logFilePath))
        {
            throw new FileNotFoundException("Log file not found.", logFilePath);
        }

        var entries = new List<RenameLogEntry>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(logFilePath))
        {
            lineNumber++;
            var entry = TryParseLine(line, lineNumber);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    public static RenameLogEntry? TryParseLine(string line, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var message = ExtractMessage(line, out var timestamp);
        if (message is null ||
            !message.StartsWith(RenamedPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var payload = message[RenamedPrefix.Length..];
        var arrowIndex = payload.IndexOf(ArrowSeparator, StringComparison.Ordinal);
        if (arrowIndex <= 0)
        {
            return null;
        }

        var sourcePath = payload[..arrowIndex].Trim();
        var targetPath = payload[(arrowIndex + ArrowSeparator.Length)..].Trim();
        if (sourcePath.Length == 0 || targetPath.Length == 0)
        {
            return null;
        }

        return new RenameLogEntry(lineNumber, timestamp, sourcePath, targetPath);
    }

    private static string? ExtractMessage(string line, out DateTime? timestamp)
    {
        timestamp = null;
        var match = LogLineHeaderRegex().Match(line);
        if (!match.Success)
        {
            return line.Contains(RenamedPrefix, StringComparison.Ordinal) ? line : null;
        }

        if (DateTime.TryParseExact(
                match.Groups[1].Value,
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            timestamp = parsed;
        }

        return line[match.Length..];
    }
}
