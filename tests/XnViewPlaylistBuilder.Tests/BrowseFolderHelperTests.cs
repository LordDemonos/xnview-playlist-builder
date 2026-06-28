using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class BrowseFolderHelperTests
{
    [Fact]
    public void GetNextBrowseDirectory_UsesParentOfAddedFolder()
    {
        var next = BrowseFolderHelper.GetNextBrowseDirectory(
        [
            @"D:\media\[Publisher] Album Vol.19 - Theme"
        ]);

        Assert.Equal(@"D:\media", next);
    }

    [Fact]
    public void GetNextBrowseDirectory_UsesSharedParentForSiblings()
    {
        var next = BrowseFolderHelper.GetNextBrowseDirectory(
        [
            @"D:\media\FolderA",
            @"D:\media\FolderB"
        ]);

        Assert.Equal(@"D:\media", next);
    }
}
