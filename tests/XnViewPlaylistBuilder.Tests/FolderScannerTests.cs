using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class FolderScannerTests
{
    [Fact]
    public void Scan_FindsImagesInMultipleFoldersAndSubfolders()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-scan-{Guid.NewGuid():N}");
        var folderA = Path.Combine(root, "A");
        var folderB = Path.Combine(root, "B", "nested");
        Directory.CreateDirectory(folderA);
        Directory.CreateDirectory(folderB);

        File.WriteAllText(Path.Combine(folderA, "a1.jpg"), "x");
        File.WriteAllText(Path.Combine(folderA, "a2.png"), "x");
        File.WriteAllText(Path.Combine(folderB, "b1.jpg"), "x");
        File.WriteAllText(Path.Combine(folderB, "note.txt"), "skip");

        try
        {
            var scanner = new FolderScanner();
            var result = scanner.Scan(
            [
                new FolderSource { AbsolutePath = folderA, IncludeSubfolders = false },
                new FolderSource { AbsolutePath = Path.Combine(root, "B"), IncludeSubfolders = true }
            ]);

            Assert.Equal(3, result.Entries.Count);
            Assert.Equal(0, result.DuplicatesSkipped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_DeduplicatesSameFileAddedTwice()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-dedupe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "dup.jpg"), "x");

        try
        {
            var scanner = new FolderScanner();
            var result = scanner.Scan(
            [
                new FolderSource { AbsolutePath = root, IncludeSubfolders = true },
                new FolderSource { AbsolutePath = root, IncludeSubfolders = true }
            ]);

            Assert.Single(result.Entries);
            Assert.Equal(1, result.DuplicatesSkipped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_AllowsDuplicatesWhenEnabled()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-dedupe-allow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "dup.jpg"), "x");

        try
        {
            var scanner = new FolderScanner();
            var result = scanner.Scan(
            [
                new FolderSource { AbsolutePath = root, IncludeSubfolders = true },
                new FolderSource { AbsolutePath = root, IncludeSubfolders = true }
            ],
            progress: null,
            CancellationToken.None,
            allowDuplicates: true);

            Assert.Equal(2, result.Entries.Count);
            Assert.Equal(0, result.DuplicatesSkipped);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_SkipsEmptyFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        File.WriteAllBytes(Path.Combine(root, "empty.jpg"), []);
        File.WriteAllText(Path.Combine(root, "valid.jpg"), "x");

        try
        {
            var scanner = new FolderScanner();
            var result = scanner.Scan([new FolderSource { AbsolutePath = root, IncludeSubfolders = false }]);

            Assert.Single(result.Entries);
            Assert.Equal(1, result.EmptyFilesSkipped);
            Assert.Single(result.SkippedEmptyPaths);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Scan_ThrowsWhenNoSources()
    {
        var scanner = new FolderScanner();
        Assert.Throws<ArgumentException>(() => scanner.Scan([]));
    }

    [Fact]
    public void Scan_HonoursCancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xnpb-cancel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        for (var i = 0; i < 200; i++)
        {
            File.WriteAllText(Path.Combine(root, $"img-{i:D4}.jpg"), "x");
        }

        try
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var scanner = new FolderScanner();
            Assert.Throws<OperationCanceledException>(() =>
                scanner.Scan([new FolderSource { AbsolutePath = root, IncludeSubfolders = false }], progress: null, cts.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
