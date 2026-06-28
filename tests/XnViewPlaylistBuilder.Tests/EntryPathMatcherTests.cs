using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class EntryPathMatcherTests
{
    [Fact]
    public void IsUnderFolder_MatchesNestedFilePath()
    {
        var entry = new MediaEntry
        {
            AbsolutePath = @"E:\Photos\Vacation\img.jpg",
            SourceRootIndex = 0
        };

        Assert.True(EntryPathMatcher.IsUnderFolder(entry, @"E:\Photos\Vacation"));
        Assert.False(EntryPathMatcher.IsUnderFolder(entry, @"E:\Photos\Other"));
    }

    [Fact]
    public void IsUnderFolder_MatchesWildcardLineForSameFolder()
    {
        var entry = new MediaEntry
        {
            AbsolutePath = @"E:\Photos\Vacation\*.*",
            StoredPath = @"E:\Photos\Vacation\*.*",
            SourceRootIndex = 0
        };

        Assert.True(EntryPathMatcher.IsUnderFolder(entry, @"E:\Photos\Vacation"));
    }

    [Fact]
    public void IsUnderFolder_MatchesPathWhenFileMissingOnDisk()
    {
        var entry = new MediaEntry
        {
            AbsolutePath = @"\\server\share\u\Astro_bunnies\photo.jpg",
            SourceRootIndex = 0
        };

        Assert.True(EntryPathMatcher.IsUnderFolder(entry, @"\\server\share\u\Astro_bunnies"));
        Assert.False(EntryPathMatcher.IsUnderFolder(entry, @"\\server\share\u\Other"));
    }

    [Fact]
    public void CollectUnderFolders_SinglePassMatchesAllFolders()
    {
        var entries = new[]
        {
            new MediaEntry { AbsolutePath = @"E:\Photos\A\1.jpg", SourceRootIndex = 0 },
            new MediaEntry { AbsolutePath = @"E:\Photos\B\2.jpg", SourceRootIndex = 0 },
            new MediaEntry { AbsolutePath = @"E:\Photos\A\Sub\3.jpg", SourceRootIndex = 0 }
        };

        var filtered = EntryPathMatcher.CollectUnderFolders(entries, [@"E:\Photos\A"]);
        Assert.Equal(2, filtered.Count);
    }
}
