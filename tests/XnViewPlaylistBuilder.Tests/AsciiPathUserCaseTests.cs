using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class AsciiPathUserCaseTests
{
    private const string MixedSeparatorUncSource =
        @"\\server\share\My Games\Emulators\BIN\OF_Patreon\u/Astro_bunnies\photo.jpg";

    private const string MixedSeparatorUncTarget =
        @"\\server\share\My Games\Emulators\BIN\OF_Patreon\u\Astro_bunnies\photo.jpg";

    [Fact]
    public void ToAsciiPath_PreservesMixedSeparatorFolderStructure()
    {
        var target = AsciiPathNormalizer.ToAsciiPath(MixedSeparatorUncSource);

        Assert.Equal(MixedSeparatorUncTarget, target);
        Assert.False(AsciiPathNormalizer.NeedsNormalization(MixedSeparatorUncSource));
    }

    [Fact]
    public void BuildPlan_IgnoresMixedSeparatorAsciiFolderStructure()
    {
        var plan = new MediaPathRenameService().BuildPlan([MixedSeparatorUncSource]);

        Assert.Equal(0, plan.AffectedEntryCount);
        Assert.Empty(plan.Operations);
    }

    [Fact]
    public void ToAsciiPath_PreservesSingleLetterFolderSegments()
    {
        const string source = @"\\server\share\My Games\Emulators\BIN\OF_Patreon\u\Astro_bunnies";

        var target = AsciiPathNormalizer.ToAsciiPath(source);

        Assert.Equal(source, target);
    }

    [Fact]
    public void ToAsciiPath_OnlyNormalizesSegmentsThatNeedIt()
    {
        const string source = @"E:\My Games\Collections\サンプル (sample)\photo.jpg";

        var target = AsciiPathNormalizer.ToAsciiPath(source);

        Assert.StartsWith(@"E:\My Games\Collections\", target, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("サ", target, StringComparison.Ordinal);
        Assert.True(AsciiPathNormalizer.IsAsciiOnly(target));
    }

    [Fact]
    public void BuildPlan_PreservesSingleLetterFolderSegments()
    {
        var plan = new MediaPathRenameService().BuildPlan([MixedSeparatorUncTarget]);

        Assert.Equal(0, plan.AffectedEntryCount);
        Assert.Empty(plan.Operations);
        Assert.Equal(MixedSeparatorUncTarget, plan.FilePathMap[MixedSeparatorUncTarget]);
    }
}
