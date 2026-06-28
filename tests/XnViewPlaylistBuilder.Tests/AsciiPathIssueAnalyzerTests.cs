using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class AsciiPathIssueAnalyzerTests
{
    [Fact]
    public void Analyze_NoProblematicPaths_ReturnsEmpty()
    {
        var summary = AsciiPathIssueAnalyzer.Analyze(
        [
            @"D:\media\sample\Images\1.jpg",
            @"D:\media\sample\Images\2.jpg"
        ]);

        Assert.False(summary.HasIssues);
        Assert.Equal(0, summary.AffectedEntryCount);
    }

    [Fact]
    public void Analyze_NonAsciiPaths_GroupsAffectedFolders()
    {
        var summary = AsciiPathIssueAnalyzer.Analyze(
        [
            @"D:\media\サンプル (sample) - Style\Images\1.jpg",
            @"D:\media\サンプル (sample) - Style\Images\2.jpg",
            @"D:\media\サンプル (sample) - Ginza OL\Images\3.jpg"
        ]);

        Assert.True(summary.HasIssues);
        Assert.Equal(3, summary.AffectedEntryCount);
        Assert.Equal(2, summary.AffectedDirectoryCount);
        Assert.Equal(2, summary.ExampleDirectories.Count);
        Assert.All(summary.ExampleDirectories, path => Assert.Contains("sample", path, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FormatDetail_ListsExampleFoldersAndCounts()
    {
        var summary = AsciiPathIssueAnalyzer.Analyze(
        [
            @"D:\media\サンプル (sample) - Style\Images\1.jpg"
        ]);

        var detail = summary.FormatDetail();

        Assert.Contains("1 file(s) under", detail, StringComparison.Ordinal);
        Assert.Contains("Affected folders:", detail, StringComparison.Ordinal);
        Assert.Contains(@"D:\media\", detail, StringComparison.Ordinal);
        Assert.Contains("Fix names", detail, StringComparison.Ordinal);
    }
}
