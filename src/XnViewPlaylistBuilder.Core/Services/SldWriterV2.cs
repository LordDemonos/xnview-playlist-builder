using System.Text;
using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed class SldWriterV2
{
    public const string HeaderLine = "# Slide Show Sequence v2";

    private static readonly string[] OptionKeyOrder =
    [
        "UseTimer", "Timer", "Loop", "FullScreen", "WinWidth", "WinHeight", "Stretch", "RandomOrder",
        "ShowInfo", "Info", "TitleBar", "OnTop", "CursorAutoHide", "BackgroundColor", "TextColor",
        "UseTextBackColor", "TextPosition", "TextBackColor", "Opacity", "Font", "EffectDuration", "Effects"
    ];

    public void Write(
        string outputPath,
        SldOptionsV2 options,
        IReadOnlyList<string> serializedPaths)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path is required.", nameof(outputPath));
        }

        if (serializedPaths.Count == 0)
        {
            throw new InvalidOperationException("At least one media path is required.");
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var content = BuildContent(options, serializedPaths);
        SldFileEncoding.WriteAllText(outputPath, content);
        AppLog.Info($"Wrote playlist: {outputPath} ({serializedPaths.Count} entries, {content.Length} bytes, UTF-8)");
    }

    public string BuildContent(SldOptionsV2 options, IReadOnlyList<string> serializedPaths)
    {
        var builder = new StringBuilder();
        builder.AppendLine(HeaderLine);

        foreach (var line in BuildOptionLines(options))
        {
            builder.AppendLine(line);
        }

        foreach (var path in serializedPaths)
        {
            builder.AppendLine(PathFormatter.QuotePath(path));
        }

        return builder.ToString();
    }

    public IReadOnlyList<string> BuildOptionLines(SldOptionsV2 options)
    {
        options.NormalizeForWrite();
        var map = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["UseTimer"] = Bool(options.UseTimer),
            ["Timer"] = options.Timer.ToString(),
            ["Loop"] = Bool(options.Loop),
            ["FullScreen"] = Bool(options.FullScreen),
            ["WinWidth"] = options.WinWidth.ToString(),
            ["WinHeight"] = options.WinHeight.ToString(),
            ["Stretch"] = Bool(options.Stretch),
            ["RandomOrder"] = Bool(options.RandomOrder),
            ["ShowInfo"] = Bool(options.ShowInfo),
            ["Info"] = options.Info,
            ["TitleBar"] = Bool(options.TitleBar),
            ["OnTop"] = Bool(options.OnTop),
            ["CursorAutoHide"] = Bool(options.CursorAutoHide),
            ["BackgroundColor"] = options.BackgroundColor.ToSldValue(),
            ["TextColor"] = options.TextColor.ToSldValue(),
            ["UseTextBackColor"] = Bool(options.UseTextBackColor),
            ["TextPosition"] = options.TextPosition.ToString(),
            ["TextBackColor"] = options.TextBackColor.ToSldValue(),
            ["Opacity"] = options.Opacity.ToString(),
            ["Font"] = options.Font,
            ["EffectDuration"] = options.EffectDuration.ToString(),
            ["Effects"] = string.Join(" ", options.Effects) + " "
        };

        return OptionKeyOrder.Select(key => $"{key} = {map[key]}").ToArray();
    }

    public IReadOnlyList<string> SerializePaths(
        IReadOnlyList<MediaEntry> entries,
        PathPolicy policy,
        string outputPath,
        string? anchorPath,
        bool useXnViewRelativePathsForUnicode = false)
    {
        var paths = new List<string>(entries.Count);

        foreach (var entry in entries)
        {
            if (!string.IsNullOrWhiteSpace(entry.StoredPath) &&
                PathKeyNormalizer.AreEquivalent(entry.StoredPath, entry.AbsolutePath))
            {
                paths.Add(entry.StoredPath);
                continue;
            }

            var formatted = PathFormatter.FormatForSld(entry.AbsolutePath, policy, outputPath, anchorPath);
            paths.Add(SldPathSerializer.ToStoredPath(formatted, outputPath, policy, useXnViewRelativePathsForUnicode));
        }

        if (paths.Count == 0)
        {
            throw new InvalidOperationException("No media paths available to write.");
        }

        return paths;
    }

    private static string Bool(bool value) => value ? "1" : "0";
}
