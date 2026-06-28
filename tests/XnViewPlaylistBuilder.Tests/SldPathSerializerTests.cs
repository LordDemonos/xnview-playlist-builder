using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class SldPathSerializerTests
{
    [Fact]
    public void ToStoredPath_NonAsciiUnderSldDirectory_WritesRelativePath()
    {
        const string outputSld = @"D:\media\big.sld";
        const string absolute =
            @"D:\media\サンプル (sample) - Navy Style\サンプル (sample) - Navy Style\Images\(1).jpg";

        if (!File.Exists(absolute))
        {
            return;
        }

        var stored = SldPathSerializer.ToStoredPath(absolute, outputSld, PathPolicy.AbsoluteLocal, useXnViewRelativePathsForUnicode: true);

        Assert.False(Path.IsPathRooted(stored));
        Assert.Equal(absolute, Path.GetFullPath(Path.Combine(@"D:\media", stored)));
    }

    [Fact]
    public void ToStoredPath_AsciiUnderSldDirectory_KeepsAbsolutePath()
    {
        const string outputSld = @"D:\media\big.sld";
        const string absolute = @"D:\media\Sample Artist 2022\Sample Artist 2022.10 - Set\001.jpg";

        if (!File.Exists(absolute))
        {
            return;
        }

        var stored = SldPathSerializer.ToStoredPath(absolute, outputSld, PathPolicy.AbsoluteLocal, useXnViewRelativePathsForUnicode: false);

        Assert.True(Path.IsPathRooted(stored));
        Assert.Equal(Path.GetFullPath(absolute), stored);
    }

    [Fact]
    public void ToStoredPath_MatchesOfficialExampleShape()
    {
        var fixturesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));
        var officialSld = Path.Combine(fixturesDir, "official-example.sld");
        if (!File.Exists(officialSld))
        {
            return;
        }

        var officialLines = SldFileEncoding.ReadAllLines(officialSld);
        var officialStored = officialLines
            .FirstOrDefault(line => line.Contains("Sample Album", StringComparison.Ordinal))
            ?.Trim()
            .Trim('"');
        if (officialStored is null)
        {
            return;
        }

        var absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(officialSld)!, officialStored));
        if (!File.Exists(absolute))
        {
            return;
        }

        var stored = SldPathSerializer.ToStoredPath(absolute, officialSld, PathPolicy.AbsoluteLocal, useXnViewRelativePathsForUnicode: true);

        Assert.Equal(officialStored, stored);
    }
}
