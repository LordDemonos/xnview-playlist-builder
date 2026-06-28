using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class WildcardPathFormatterTests
{
    [Fact]
    public void IsWildcardPath_DetectsAsteriskSuffix()
    {
        Assert.True(WildcardPathFormatter.IsWildcardPath(@"E:\Photos\*.*"));
        Assert.False(WildcardPathFormatter.IsWildcardPath(@"E:\Photos\img.jpg"));
    }

    [Fact]
    public void ToWildcardLine_AppendsSuffixForAbsolutePolicy()
    {
        var line = WildcardPathFormatter.ToWildcardLine(
            @"E:\Photos\Vacation",
            PathPolicy.AbsoluteLocal,
            @"E:\out.sld",
            anchorPath: null);

        Assert.Equal(@"E:\Photos\Vacation\*.*", line);
    }
}
