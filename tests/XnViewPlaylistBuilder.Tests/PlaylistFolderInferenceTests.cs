using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class PlaylistFolderInferenceTests
{
    [Fact]
    public void InferUniqueDirectories_GroupsEntriesByParentFolder()
    {
        var entries = new List<MediaEntry>
        {
            new() { AbsolutePath = @"D:\media\a\1.jpg", StoredPath = @"D:\media\a\1.jpg", SourceRootIndex = 0 },
            new() { AbsolutePath = @"D:\media\a\2.jpg", StoredPath = @"D:\media\a\2.jpg", SourceRootIndex = 0 },
            new() { AbsolutePath = @"D:\media\b\3.jpg", StoredPath = @"D:\media\b\3.jpg", SourceRootIndex = 0 }
        };

        var folders = PlaylistFolderInference.InferUniqueDirectories(entries);

        Assert.Equal(2, folders.Count);
        Assert.Contains(@"D:\media\a", folders);
        Assert.Contains(@"D:\media\b", folders);
    }
}
