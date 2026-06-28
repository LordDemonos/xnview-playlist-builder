using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class AsciiPathNormalizerTests
{
    [Fact]
    public void ToAsciiSegment_PreservesAsciiSpacesInFolderNames()
    {
        Assert.Equal("My Games", AsciiPathNormalizer.ToAsciiSegment("My Games"));
        Assert.Equal("Kato Best", AsciiPathNormalizer.ToAsciiSegment("Kato Best"));
        Assert.False(AsciiPathNormalizer.NeedsNormalization(@"\\server\share\My Games\file.jpg"));
    }

    [Fact]
    public void ToAsciiSegment_PreservesAsciiUnderscoresInFolderNames()
    {
        Assert.Equal("OF_Patreon", AsciiPathNormalizer.ToAsciiSegment("OF_Patreon"));
        Assert.Equal("reddit_user_rsjto", AsciiPathNormalizer.ToAsciiSegment("reddit_user_rsjto"));
        Assert.Equal("Astro_bunnies", AsciiPathNormalizer.ToAsciiSegment("Astro_bunnies"));
    }

    [Fact]
    public void ToAsciiPath_PreservesAsciiSpacesInFolderNames()
    {
        var source = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "My Games", "photo.jpg"));
        var target = AsciiPathNormalizer.ToAsciiPath(source);
        Assert.Equal(source, target);
    }

    [Fact]
    public void ToAsciiSegment_PreservesAsciiParenthesesInNames()
    {
        Assert.Equal(
            "QQueen - Sirius (Azur Lane)",
            AsciiPathNormalizer.ToAsciiSegment("QQueen - Sirius (Azur Lane)"));
        Assert.Equal("28 Set (both)", AsciiPathNormalizer.ToAsciiSegment("28 Set (both)"));
        Assert.Equal("Pinup Set (HOT)", AsciiPathNormalizer.ToAsciiSegment("Pinup Set (HOT)"));
        Assert.Equal("(1).jpg", AsciiPathNormalizer.ToAsciiSegment("(1).jpg"));
    }

    [Theory]
    [InlineData("サンプル (sample) - Navy Style", "sample-Navy-Style")]
    [InlineData("サンプル (sample) – Ginza OL", "sample-Ginza-OL")]
    [InlineData("[Set] サンプル (sample) - Red Theme", "Set-sample-Red-Theme")]
    public void ToAsciiSegment_UsesParentheticalHint(string input, string expectedStart)
    {
        var result = AsciiPathNormalizer.ToAsciiSegment(input);
        Assert.StartsWith(expectedStart, result, StringComparison.OrdinalIgnoreCase);
        Assert.True(AsciiPathNormalizer.IsAsciiOnly(result));
    }

    [Fact]
    public void ToAsciiSegment_FixesMojibakeFilename()
    {
        var mediaRoot = LocalProbePaths.ProbeMediaRoot ?? LocalProbePaths.MediaRoot;
        if (!Directory.Exists(mediaRoot))
        {
            return;
        }

        var ginzaFolder = Directory.GetDirectories(mediaRoot)
            .FirstOrDefault(path => path.Contains("Ginza", StringComparison.OrdinalIgnoreCase));
        if (ginzaFolder is null)
        {
            return;
        }

        var sample = Directory.GetFiles(ginzaFolder, "*.jpg").FirstOrDefault();
        if (sample is null)
        {
            return;
        }

        var result = AsciiPathNormalizer.ToAsciiSegment(Path.GetFileName(sample));

        Assert.Contains("sample", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GINZA-OL", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(AsciiPathNormalizer.IsAsciiOnly(result));
    }

    [Fact]
    public void BuildPlan_UnicodeEntry_ProducesAsciiTargets()
    {
        const string sample =
            @"D:\media\サンプル (sample) - Navy Style\サンプル (sample) - Navy Style\Images\(1).jpg";

        if (!File.Exists(sample))
        {
            return;
        }

        var plan = new MediaPathRenameService().BuildPlan([sample]);
        Assert.True(plan.AffectedEntryCount > 0);
        Assert.True(plan.Operations.Count(op => op.IsDirectory) >= 2);
        Assert.All(plan.FilePathMap.Values, path => Assert.True(AsciiPathNormalizer.IsAsciiOnly(path), path));
        Assert.True(File.Exists(plan.FilePathMap[sample]) || File.Exists(sample));
    }

    [Fact]
    public void ApplyFilePathMap_UpdatesAfterRename_NotBefore()
    {
        var before = new MediaEntry
        {
            AbsolutePath = @"E:\before\file.jpg",
            StoredPath = "legacy",
            SourceRootIndex = 0
        };

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [before.AbsolutePath] = @"E:\after\file.jpg"
        };

        var after = MediaPathRenameService.ApplyFilePathMap([before], map).Single();
        Assert.Equal(@"E:\after\file.jpg", after.AbsolutePath);
        Assert.Null(after.StoredPath);
    }
}
