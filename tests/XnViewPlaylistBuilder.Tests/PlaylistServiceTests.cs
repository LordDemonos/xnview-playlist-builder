using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class PlaylistServiceTests
{
    [Fact]
    public void SavePlaylist_WritesValidStructureEndToEnd()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-e2e-{Guid.NewGuid():N}");
        var folderA = Path.Combine(root, "A");
        var folderB = Path.Combine(root, "B");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);
        File.WriteAllText(Path.Combine(folderA, "1.jpg"), "x");
        File.WriteAllText(Path.Combine(folderB, "2.png"), "x");

        var output = Path.Combine(root, "out.sld");

        try
        {
            var service = new PlaylistService();
            var scan = service.ScanFolders(
            [
                new FolderSource { AbsolutePath = folderA, IncludeSubfolders = true },
                new FolderSource { AbsolutePath = folderB, IncludeSubfolders = true }
            ]);

            var options = SldOptionsV2.CreateDefaults();
            options.Timer = 15;
            options.Loop = true;
            options.RandomOrder = true;

            service.SavePlaylist(output, options, scan.Entries, PathPolicy.AbsoluteLocal);

            var lines = File.ReadAllLines(output);
            Assert.Equal(SldWriterV2.HeaderLine, lines[0]);
            Assert.Equal("Timer = 15", lines[2]);
            Assert.Equal(25, lines.Length);
            Assert.All(lines.Skip(23), line => Assert.StartsWith("\"", line));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SavePlaylist_WritesWildcardLineWhenFolderSourceUsesWildcard()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-wildcard-{Guid.NewGuid():N}");
        var folder = Path.Combine(root, "Photos");
        Directory.CreateDirectory(folder);
        var output = Path.Combine(root, "out.sld");

        try
        {
            var service = new PlaylistService();
            var options = SldOptionsV2.CreateDefaults();
            service.SavePlaylist(
                output,
                options,
                [],
                PathPolicy.AbsoluteLocal,
                [new FolderSource { AbsolutePath = folder, IncludeSubfolders = true, UseWildcardLine = true }]);

            var lines = File.ReadAllLines(output);
            Assert.Contains($"\"{folder}\\*.*\"", lines);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
