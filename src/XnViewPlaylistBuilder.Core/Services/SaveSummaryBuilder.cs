using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public static class SaveSummaryBuilder
{
    private const int MaxInfoTemplateLength = 72;
    private const int FullHealthCheckLimit = 5000;
    private const int MissingCheckReportInterval = 5000;

    public static SaveSummary Build(
        string outputPath,
        IReadOnlyList<MediaEntry> entries,
        PathPolicy pathPolicy,
        SldOptionsV2 options,
        string? anchorPath = null,
        int? totalPathCount = null,
        IReadOnlyList<string>? serializedPaths = null,
        bool useXnViewRelativePathsForUnicode = false,
        IProgress<WorkProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var missingOnDisk = CountMissingOnDisk(entries, progress, cancellationToken);
        var skipHealthCheck = entries.Count > FullHealthCheckLimit;
        var health = skipHealthCheck
            ? new MediaFileHealthReport { TotalChecked = entries.Count }
            : MediaFileHealthChecker.Analyze(entries.Select(entry => entry.AbsolutePath));
        var normalized = CloneOptions(options);
        normalized.NormalizeForWrite();
        var pathsForEncodingCheck = serializedPaths
            ?? entries.Select(entry => entry.StoredPath ?? entry.AbsolutePath).ToArray();
        var nonAsciiPathCount = SldFileEncoding.CountNonAsciiPaths(pathsForEncodingCheck);

        return new SaveSummary
        {
            OutputPath = outputPath,
            EntryCount = totalPathCount ?? entries.Count,
            MissingOnDiskCount = missingOnDisk,
            EmptyFileCount = health.EmptyCount,
            InvalidImageCount = health.InvalidImageCount,
            NonAsciiPathCount = nonAsciiPathCount,
            PathPolicy = pathPolicy,
            PathPolicyLabel = PathPolicyLabels.GetLabel(pathPolicy),
            AnchorPath = anchorPath,
            IsExperimentalPathPolicy = pathPolicy == PathPolicy.RelativeToSld,
            UseXnViewRelativePathsForUnicode = useXnViewRelativePathsForUnicode,
            PlaybackSummary = FormatPlaybackSummary(normalized),
            OverlaySummary = FormatOverlaySummary(normalized),
            EffectsSummary = FormatEffectsSummary(normalized),
            WindowSummary = FormatWindowSummary(normalized),
            FileHealthCheckSkipped = skipHealthCheck
        };
    }

    private static int CountMissingOnDisk(
        IReadOnlyList<MediaEntry> entries,
        IProgress<WorkProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return 0;
        }

        var missing = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(entries[index].AbsolutePath))
            {
                missing++;
            }

            if (progress is not null && index > 0 && index % MissingCheckReportInterval == 0)
            {
                progress.Report(new WorkProgressReport
                {
                    Status = "Checking files on disk…",
                    Detail = $"{index:N0} of {entries.Count:N0}",
                    PercentComplete = index * 100.0 / entries.Count
                });
            }
        }

        return missing;
    }

    internal static string FormatPlaybackSummary(SldOptionsV2 options)
    {
        var parts = new List<string>(4)
        {
            options.UseTimer ? $"{options.Timer} s" : "Manual advance"
        };

        if (options.Loop)
        {
            parts.Add("Loop");
        }

        if (options.FullScreen)
        {
            parts.Add("Full screen");
        }

        if (options.RandomOrder)
        {
            parts.Add("Random order");
        }

        return parts.Count > 0 ? string.Join(" · ", parts) : "Default playback";
    }

    internal static string FormatOverlaySummary(SldOptionsV2 options)
    {
        if (!options.ShowInfo)
        {
            return "Off";
        }

        var template = TruncateInfoTemplate(options.Info);
        var position = SldTextPosition.GetLabel(options.TextPosition);
        return $"Show info — \"{template}\" · {position}";
    }

    internal static string FormatEffectsSummary(SldOptionsV2 options)
    {
        var effectsLabel = SldEffects.IsAllEffects(options.Effects)
            ? $"All ({SldEffects.Count})"
            : $"{options.Effects.Count} selected";

        return $"{effectsLabel} · {options.EffectDuration} ms";
    }

    internal static string? FormatWindowSummary(SldOptionsV2 options)
    {
        if (options.FullScreen)
        {
            return null;
        }

        var size = $"{options.WinWidth}×{options.WinHeight}";
        return options.Stretch ? $"{size} · Stretch" : size;
    }

    private static SldOptionsV2 CloneOptions(SldOptionsV2 options) =>
        new()
        {
            UseTimer = options.UseTimer,
            Timer = options.Timer,
            Loop = options.Loop,
            FullScreen = options.FullScreen,
            WinWidth = options.WinWidth,
            WinHeight = options.WinHeight,
            Stretch = options.Stretch,
            RandomOrder = options.RandomOrder,
            ShowInfo = options.ShowInfo,
            Info = options.Info,
            TitleBar = options.TitleBar,
            OnTop = options.OnTop,
            CursorAutoHide = options.CursorAutoHide,
            BackgroundColor = options.BackgroundColor,
            TextColor = options.TextColor,
            UseTextBackColor = options.UseTextBackColor,
            TextPosition = options.TextPosition,
            TextBackColor = options.TextBackColor,
            Opacity = options.Opacity,
            Font = options.Font,
            EffectDuration = options.EffectDuration,
            Effects = options.Effects.ToArray()
        };

    private static string TruncateInfoTemplate(string template)
    {
        var normalized = SldInfoTokens.NormalizeTemplate(template);
        if (normalized.Length <= MaxInfoTemplateLength)
        {
            return normalized;
        }

        return normalized[..(MaxInfoTemplateLength - 1)] + "…";
    }
}

public sealed class SaveSummary
{
    public required string OutputPath { get; init; }
    public int EntryCount { get; init; }
    public int MissingOnDiskCount { get; init; }
    public int EmptyFileCount { get; init; }
    public int InvalidImageCount { get; init; }
    public int UnplayableFileCount => EmptyFileCount + InvalidImageCount;
    public int NonAsciiPathCount { get; init; }
    public PathPolicy PathPolicy { get; init; }
    public required string PathPolicyLabel { get; init; }
    public string? AnchorPath { get; init; }
    public bool IsExperimentalPathPolicy { get; init; }
    public bool UseXnViewRelativePathsForUnicode { get; init; }
    public required string PlaybackSummary { get; init; }
    public required string OverlaySummary { get; init; }
    public required string EffectsSummary { get; init; }
    public string? WindowSummary { get; init; }
    public bool FileHealthCheckSkipped { get; init; }
}
