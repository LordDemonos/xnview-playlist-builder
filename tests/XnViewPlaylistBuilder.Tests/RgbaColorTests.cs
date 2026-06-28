using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Tests;

public class RgbaColorTests
{
    [Fact]
    public void TryParse_ValidRgba_ReturnsColor()
    {
        Assert.True(RgbaColor.TryParse("0 0 0 255", out var color));
        Assert.Equal(new RgbaColor(0, 0, 0, 255), color);
    }

    [Fact]
    public void ToSldValue_RoundTrips()
    {
        var color = new RgbaColor(128, 64, 32, 255);
        Assert.Equal("128 64 32 255", color.ToSldValue());
        Assert.True(RgbaColor.TryParse(color.ToSldValue(), out var parsed));
        Assert.Equal(color, parsed);
    }
}
