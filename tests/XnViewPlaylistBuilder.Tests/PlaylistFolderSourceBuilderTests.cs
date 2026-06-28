using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class PlaylistFolderSourceBuilderTests
{
    [Fact]
    public void Build_InfersWildcardAndRegularFolders()
    {
        var entries = new List<MediaEntry>
        {
            new()
            {
                AbsolutePath = @"D:\media\album\photo.jpg",
                StoredPath = @"D:\media\album\photo.jpg"
            },
            new()
            {
                AbsolutePath = @"D:\wild\line.jpg",
                StoredPath = @"D:\wild\*.*"
            }
        };

        var sources = PlaylistFolderSourceBuilder.Build(entries, defaultIncludeSubfolders: true);

        Assert.Equal(2, sources.Count);
        Assert.Contains(sources, source => source.AbsolutePath.EndsWith(@"media\album", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sources, source => source.UseWildcardLine && source.AbsolutePath.EndsWith(@"wild", StringComparison.OrdinalIgnoreCase));
    }
}
