using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class FolderSourceCollapserTests
{
    [Fact]
    public void Collapse_KeepsShallowestRootWhenIncludeSubfoldersEnabled()
    {
        var sources = new[]
        {
            CreateSource(@"D:\media\Artist A\Album 1\Images"),
            CreateSource(@"D:\media\Artist A\Album 1"),
            CreateSource(@"D:\media\Artist A")
        };

        var result = FolderSourceCollapser.Collapse(sources);

        Assert.Equal(2, result.CollapsedCount);
        Assert.Single(result.Roots);
        Assert.Equal(@"D:\media\Artist A", result.Roots[0].AbsolutePath);
        Assert.Equal(2, result.Roots[0].CollapsedSubfolderPaths.Count);
    }

    [Fact]
    public void Collapse_KeepsNestedFolderWhenParentDoesNotIncludeSubfolders()
    {
        var sources = new[]
        {
            CreateSource(@"D:\media\Artist A", includeSubfolders: false),
            CreateSource(@"D:\media\Artist A\Album 1")
        };

        var result = FolderSourceCollapser.Collapse(sources);

        Assert.Equal(0, result.CollapsedCount);
        Assert.Equal(2, result.Roots.Count);
    }

    [Fact]
    public void Collapse_KeepsUnrelatedRoots()
    {
        var sources = new[]
        {
            CreateSource(@"D:\media\Artist A"),
            CreateSource(@"D:\media\Artist B")
        };

        var result = FolderSourceCollapser.Collapse(sources);

        Assert.Equal(0, result.CollapsedCount);
        Assert.Equal(2, result.Roots.Count);
    }

    [Fact]
    public void Collapse_MergesWildcardFlagFromCollapsedChild()
    {
        var sources = new[]
        {
            new FolderSource
            {
                AbsolutePath = @"E:\root\parent",
                IncludeSubfolders = true
            },
            new FolderSource
            {
                AbsolutePath = @"E:\root\parent\child",
                IncludeSubfolders = true,
                UseWildcardLine = true
            }
        };

        var result = FolderSourceCollapser.Collapse(sources);

        Assert.Single(result.Roots);
        Assert.True(result.Roots[0].UseWildcardLine);
    }

    private static FolderSource CreateSource(string path, bool includeSubfolders = true) =>
        new()
        {
            AbsolutePath = path,
            IncludeSubfolders = includeSubfolders
        };
}
