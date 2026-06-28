using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class SldGoldenFileTests
{
    private static readonly string GoldenSldPath =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures", "golden-test.sld"));

    [Fact]
    public void Read_GoldenTestSld_ParsesShowInfoAndInfoTemplate()
    {
        Assert.True(File.Exists(GoldenSldPath), $"Golden file missing: {GoldenSldPath}");

        var playlist = new SldReaderV2().Read(GoldenSldPath);

        Assert.True(playlist.Options.ShowInfo);
        Assert.Equal("{Folder name} - {Filename}", playlist.Options.Info);
        Assert.Equal(0, playlist.Options.TextPosition);
        Assert.Equal("Top left", SldTextPosition.GetLabel(playlist.Options.TextPosition));
        Assert.True(playlist.Options.FullScreen);
        Assert.Equal(15, playlist.Options.Timer);
    }

    [Fact]
    public void RoundTrip_GoldenTestSld_PreservesAllTwentyTwoOptionLines()
    {
        Assert.True(File.Exists(GoldenSldPath), $"Golden file missing: {GoldenSldPath}");

        var reader = new SldReaderV2();
        var writer = new SldWriterV2();
        var playlist = reader.Read(GoldenSldPath);
        var paths = playlist.Entries
            .Select(entry => entry.StoredPath ?? entry.AbsolutePath)
            .ToList();

        var rebuiltLines = writer.BuildContent(playlist.Options, paths)
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Take(22)
            .ToArray();

        var originalLines = File.ReadAllLines(GoldenSldPath).Take(22).ToArray();
        Assert.Equal(22, originalLines.Length);
        Assert.Equal(22, rebuiltLines.Length);

        for (var i = 0; i < 22; i++)
        {
            Assert.Equal(originalLines[i], rebuiltLines[i]);
        }
    }

    [Fact]
    public void TextPosition_ValuesMatchXnViewReferenceOrder()
    {
        Assert.Equal("Top left", SldTextPosition.GetLabel(0));
        Assert.Equal("Top center", SldTextPosition.GetLabel(1));
        Assert.Equal("Top right", SldTextPosition.GetLabel(2));
        Assert.Equal("Left center", SldTextPosition.GetLabel(3));
        Assert.Equal(0, SldTextPosition.Default);
        Assert.Equal(0, SldTextPosition.Normalize("0"));
    }
}
