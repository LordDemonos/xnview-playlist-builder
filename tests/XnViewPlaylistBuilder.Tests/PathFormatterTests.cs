using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class PathFormatterTests
{
    [Fact]
    public void FormatAbsoluteLocal_UsesFullPath()
    {
        var temp = Path.Combine(Path.GetTempPath(), "xnpb-path-test");
        Directory.CreateDirectory(temp);
        try
        {
            var file = Path.Combine(temp, "photo.jpg");
            File.WriteAllText(file, "x");

            var formatted = PathFormatter.FormatForSld(
                file,
                PathPolicy.AbsoluteLocal,
                outputSldPath: null,
                anchorPath: null);

            Assert.Equal(Path.GetFullPath(file), formatted);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void QuotePath_WrapsInDoubleQuotes()
    {
        Assert.Equal(@"""D:\media\a.jpg""", PathFormatter.QuotePath(@"D:\media\a.jpg"));
    }

    [Fact]
    public void QuotePath_RejectsEmbeddedQuotes()
    {
        Assert.Throws<InvalidOperationException>(() => PathFormatter.QuotePath(@"D:\bad""name.jpg"));
    }
}
