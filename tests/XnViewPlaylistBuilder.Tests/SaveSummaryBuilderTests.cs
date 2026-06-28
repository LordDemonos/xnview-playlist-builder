using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class SaveSummaryBuilderTests
{
    [Fact]
    public void Build_CountsMissingOnDiskEntries()
    {
        var summary = SaveSummaryBuilder.Build(
            @"D:\out\playlist.sld",
            [
                new MediaEntry { AbsolutePath = @"D:\missing.jpg" },
                new MediaEntry { AbsolutePath = Environment.ProcessPath ?? @"C:\exists.exe" }
            ],
            PathPolicy.AbsoluteLocal,
            SldOptionsV2.CreateDefaults());

        Assert.Equal(2, summary.EntryCount);
        Assert.True(summary.MissingOnDiskCount >= 1);
        Assert.Equal(PathPolicy.AbsoluteLocal, summary.PathPolicy);
    }

    [Fact]
    public void Build_IncludesSlideshowOptionSummaries()
    {
        var options = SldOptionsV2.CreateDefaults();
        options.ShowInfo = true;
        options.Info = "{Filename}";
        options.TextPosition = 0;
        options.Effects = [1, 2, 3];

        var summary = SaveSummaryBuilder.Build(
            @"D:\out\playlist.sld",
            [],
            PathPolicy.AbsoluteLocal,
            options);

        Assert.Contains("15 s", summary.PlaybackSummary);
        Assert.Contains("Loop", summary.PlaybackSummary);
        Assert.Contains("Full screen", summary.PlaybackSummary);
        Assert.Contains("Show info", summary.OverlaySummary);
        Assert.Contains("{Filename}", summary.OverlaySummary);
        Assert.Contains("Top left", summary.OverlaySummary);
        Assert.Equal("3 selected · 1000 ms", summary.EffectsSummary);
        Assert.Null(summary.WindowSummary);
    }

    [Fact]
    public void Build_WhenShowInfoDisabled_OverlaySummaryIsOff()
    {
        var options = SldOptionsV2.CreateDefaults();
        options.ShowInfo = false;

        var summary = SaveSummaryBuilder.Build(@"D:\out\playlist.sld", [], PathPolicy.AbsoluteLocal, options);

        Assert.Equal("Off", summary.OverlaySummary);
    }

    [Fact]
    public void Build_WhenNotFullScreen_IncludesWindowSummary()
    {
        var options = SldOptionsV2.CreateDefaults();
        options.FullScreen = false;
        options.WinWidth = 800;
        options.WinHeight = 600;
        options.Stretch = true;

        var summary = SaveSummaryBuilder.Build(@"D:\out\playlist.sld", [], PathPolicy.AbsoluteLocal, options);

        Assert.Equal("800×600 · Stretch", summary.WindowSummary);
    }

    [Fact]
    public void Build_WhenAllEffectsSelected_UsesAllLabel()
    {
        var options = SldOptionsV2.CreateDefaults();
        options.EffectDuration = 750;

        var summary = SaveSummaryBuilder.Build(@"D:\out\playlist.sld", [], PathPolicy.AbsoluteLocal, options);

        Assert.Equal("All (56) · 750 ms", summary.EffectsSummary);
    }
}
