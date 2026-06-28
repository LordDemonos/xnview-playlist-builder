using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed class SldReaderV2
{
    public SldPlaylist Read(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Playlist file not found.", filePath);
        }

        var lines = SldFileEncoding.ReadAllLines(filePath);
        if (lines.Length == 0)
        {
            throw new InvalidDataException("Playlist file is empty.");
        }

        if (!lines[0].StartsWith("# Slide Show Sequence", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported playlist header: {lines[0]}");
        }

        var options = new SldOptionsV2();
        var paths = new List<string>();
        var optionIndex = 1;

        for (; optionIndex < lines.Length; optionIndex++)
        {
            var line = lines[optionIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith('"'))
            {
                break;
            }

            ParseOptionLine(line, options);
        }

        for (var i = optionIndex; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            paths.Add(UnquotePath(line));
        }

        if (paths.Count == 0)
        {
            throw new InvalidDataException("Playlist contains no image paths.");
        }

        var sldDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        var entries = paths
            .Select((path, index) => CreateEntry(path, sldDirectory, index))
            .ToList();

        AppLog.Info($"Loaded playlist {filePath}: {entries.Count} entries, {optionIndex - 1} option lines.");

        return new SldPlaylist
        {
            SourcePath = Path.GetFullPath(filePath),
            Options = options,
            Entries = entries
        };
    }

    private static MediaEntry CreateEntry(string storedPath, string sldDirectory, int index)
    {
        var normalized = storedPath.Replace('/', '\\');
        var resolved = ResolvePath(normalized, sldDirectory);

        return new MediaEntry
        {
            AbsolutePath = resolved,
            StoredPath = normalized,
            SourceRootIndex = 0
        };
    }

    internal static string ResolvePath(string storedPath, string sldDirectory)
    {
        if (Path.IsPathRooted(storedPath))
        {
            try
            {
                return Path.GetFullPath(storedPath);
            }
            catch
            {
                return storedPath;
            }
        }

        var combined = Path.Combine(sldDirectory, storedPath);
        if (File.Exists(combined))
        {
            return Path.GetFullPath(combined);
        }

        return storedPath;
    }

    internal static void ParseOptionLine(string line, SldOptionsV2 options)
    {
        var separatorIndex = line.IndexOf(" = ", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            throw new InvalidDataException($"Invalid option line: {line}");
        }

        var key = line[..separatorIndex].Trim();
        var value = line[(separatorIndex + 3)..].Trim();

        switch (key)
        {
            case "UseTimer": options.UseTimer = ParseBool(value); break;
            case "Timer": options.Timer = ParseInt(value); break;
            case "Loop": options.Loop = ParseBool(value); break;
            case "FullScreen": options.FullScreen = ParseBool(value); break;
            case "WinWidth": options.WinWidth = ParseInt(value); break;
            case "WinHeight": options.WinHeight = ParseInt(value); break;
            case "Stretch": options.Stretch = ParseBool(value); break;
            case "RandomOrder": options.RandomOrder = ParseBool(value); break;
            case "ShowInfo": options.ShowInfo = ParseBool(value); break;
            case "Info": options.Info = SldInfoTokens.NormalizeTemplate(value); break;
            case "TitleBar": options.TitleBar = ParseBool(value); break;
            case "OnTop": options.OnTop = ParseBool(value); break;
            case "CursorAutoHide": options.CursorAutoHide = ParseBool(value); break;
            case "BackgroundColor": options.BackgroundColor = ParseRgba(value); break;
            case "TextColor": options.TextColor = ParseRgba(value); break;
            case "UseTextBackColor": options.UseTextBackColor = ParseBool(value); break;
            case "TextPosition": options.TextPosition = SldTextPosition.Normalize(ParseInt(value)); break;
            case "TextBackColor": options.TextBackColor = ParseRgba(value); break;
            case "Opacity": options.Opacity = ParseInt(value); break;
            case "Font": options.Font = value; break;
            case "EffectDuration": options.EffectDuration = ParseInt(value); break;
            case "Effects": options.Effects = ParseEffects(value); break;
            default:
                AppLog.Warning($"Unknown option key ignored: {key}");
                break;
        }
    }

    public static string UnquotePath(string line)
    {
        if (!line.StartsWith('"') || !line.EndsWith('"') || line.Length < 2)
        {
            throw new InvalidDataException($"Invalid quoted path line: {line}");
        }

        return line[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
    }

    private static bool ParseBool(string value) => value is "1" or "true" or "True";

    private static int ParseInt(string value) =>
        int.TryParse(value, out var number) ? number : throw new InvalidDataException($"Invalid integer: {value}");

    private static RgbaColor ParseRgba(string value)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            throw new InvalidDataException($"Invalid RGBA value: {value}");
        }

        return new RgbaColor(
            byte.Parse(parts[0]),
            byte.Parse(parts[1]),
            byte.Parse(parts[2]),
            byte.Parse(parts[3]));
    }

    private static IReadOnlyList<int> ParseEffects(string value)
    {
        var effects = value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToArray();

        return effects.Length > 0 ? effects : Enumerable.Range(1, 56).ToArray();
    }
}
