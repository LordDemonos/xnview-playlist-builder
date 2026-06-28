using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class RemovedPathScanRootSuggesterTests
{
    [Fact]
    public void Suggest_GroupsBySubjectFolder_AndPreselectsImmediateParent()
    {
        var removed = new[]
        {
            @"\\server\share\OF_Patreon\Artist Alpha\a.jpg",
            @"\\server\share\OF_Patreon\Artist Alpha\b.jpg",
            @"\\server\share\OF_Patreon\Another Artist\c.jpg"
        };

        var suggestions = RemovedPathScanRootSuggester.Suggest(removed);

        var subjectFolders = suggestions
            .Where(suggestion => suggestion.Kind == ScanRootSuggestionKind.SubjectFolder)
            .ToList();

        Assert.Equal(2, subjectFolders.Count);
        Assert.All(subjectFolders, suggestion => Assert.True(suggestion.IsSelectedByDefault));
        Assert.Contains(
            subjectFolders,
            suggestion => suggestion.FolderPath.EndsWith(@"Artist Alpha", StringComparison.OrdinalIgnoreCase) &&
                          suggestion.RemovedFileCount == 2);
    }

    [Fact]
    public void Suggest_IncludesBroaderFolder_UncheckedByDefault()
    {
        var removed = new[]
        {
            @"\\server\share\OF_Patreon\Artist Alpha\a.jpg",
            @"\\server\share\OF_Patreon\Artist Alpha\b.jpg"
        };

        var suggestions = RemovedPathScanRootSuggester.Suggest(removed);
        var broader = suggestions
            .Where(suggestion => suggestion.Kind == ScanRootSuggestionKind.BroaderFolder)
            .ToList();

        Assert.Contains(
            broader,
            suggestion => suggestion.FolderPath.EndsWith("OF_Patreon", StringComparison.OrdinalIgnoreCase) &&
                          suggestion.RemovedFileCount == 2);
        Assert.All(broader, suggestion => Assert.False(suggestion.IsSelectedByDefault));
    }

    [Fact]
    public void PathRenamePreview_FlagsMissingSources()
    {
        var plan = new PathRenamePlan
        {
            Operations =
            [
                new PathRenameOperation(
                    @"\\missing\share\emoji_🖤.png",
                    @"\\missing\share\emoji-.png",
                    IsDirectory: false)
            ],
            FilePathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            AffectedEntryCount = 1
        };

        var preview = PathRenamePreview.FromPlan(plan);

        Assert.Equal(0, preview.ReadyCount);
        Assert.Equal(1, preview.MissingCount);
        Assert.Empty(preview.CreateExecutablePlan().Operations);
    }
}
