using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class FolderScannerDeepScanTests
{
    [Fact]
    public void Scan_WithSubfolders_FindsImagesInNestedImagesSubfolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-deep-{Guid.NewGuid():N}");
        var nestedImage = Path.Combine(root, "inner-album", "Images", "set-b", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedImage)!);
        File.WriteAllText(nestedImage, "jpeg-bytes");

        try
        {
            var scanner = new FolderScanner();
            var result = scanner.Scan(
            [
                new FolderSource { AbsolutePath = root, IncludeSubfolders = true }
            ]);

            Assert.Single(result.Entries);
            Assert.Equal(Path.GetFullPath(nestedImage), result.Entries[0].AbsolutePath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_WithoutSubfolders_MissesNestedImages()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-shallow-{Guid.NewGuid():N}");
        var nestedImage = Path.Combine(root, "inner-album", "Images", "1.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(nestedImage)!);
        File.WriteAllText(nestedImage, "jpeg-bytes");

        try
        {
            var scanner = new FolderScanner();
            var result = scanner.Scan(
            [
                new FolderSource { AbsolutePath = root, IncludeSubfolders = false }
            ]);

            Assert.Empty(result.Entries);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
