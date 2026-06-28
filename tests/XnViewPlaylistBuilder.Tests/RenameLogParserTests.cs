using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class RenameLogParserTests
{
    [Fact]
    public void TryParseLine_ParsesStandardRenamedEntry()
    {
        const string line = "2026-05-23 14:30:01.123 [INFO] Renamed: E:\\old\\folder\\file.jpg -> E:\\new\\folder\\file.jpg";

        var entry = RenameLogParser.TryParseLine(line, 42);

        Assert.NotNull(entry);
        Assert.Equal(42, entry.LineNumber);
        Assert.Equal("E:\\old\\folder\\file.jpg", entry.SourcePath);
        Assert.Equal("E:\\new\\folder\\file.jpg", entry.TargetPath);
        Assert.NotNull(entry.Timestamp);
    }

    [Fact]
    public void TryParseLine_IgnoresNonRenameLines()
    {
        Assert.Null(RenameLogParser.TryParseLine("2026-05-23 14:30:01.123 [INFO] Application starting.", 1));
        Assert.Null(RenameLogParser.TryParseLine("2026-05-23 14:30:01.123 [WARN] Rename skipped (source missing): E:\\missing", 2));
    }

    [Fact]
    public void ParseFile_ReturnsOnlyRenamedLines()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "xnpb-log-" + Guid.NewGuid().ToString("N") + ".log");

        try
        {
            File.WriteAllLines(tempFile,
            [
                "2026-05-23 14:30:00.000 [INFO] Application starting.",
                "2026-05-23 14:30:01.123 [INFO] Renamed: C:\\a\\old -> C:\\a\\new",
                "2026-05-23 14:30:02.456 [INFO] Renamed: C:\\a\\new\\one.jpg -> C:\\a\\new\\1.jpg"
            ]);

            var entries = RenameLogParser.ParseFile(tempFile);

            Assert.Equal(2, entries.Count);
            Assert.Equal("C:\\a\\old", entries[0].SourcePath);
            Assert.Equal("C:\\a\\new\\1.jpg", entries[1].TargetPath);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}

public class MediaPathRenameUndoTests
{
    [Fact]
    public void ApplySegmentSubstitutions_ReplacesMatchingSegments()
    {
        var substitutions = new[]
        {
            new PathSegmentSubstitution("My-Games", "My Games"),
            new PathSegmentSubstitution("OF-Patreon", "OF_Patreon")
        };

        var adjusted = RenameUndoResolver.ApplySegmentSubstitutions(
            @"\\server\share\My-Games\Emulators\OF-Patreon\Austin-White",
            substitutions);

        Assert.Equal(@"\\server\share\My Games\Emulators\OF_Patreon\Austin-White", adjusted);
    }

    [Fact]
    public void ResolveRelativeToAnchor_FindsLeafUnderCorrectedParent()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-undo-" + Guid.NewGuid().ToString("N"));
        var anchor = Path.Combine(root, "OF_Patreon");
        var renamedChild = Path.Combine(anchor, "Austin-White");

        try
        {
            Directory.CreateDirectory(renamedChild);

            var entry = new RenameLogEntry(
                1,
                null,
                Path.Combine(anchor, "Austin White"),
                Path.Combine(root, "My-Games", "OF-Patreon", "Austin-White"));

            var resolution = RenameUndoResolver.Resolve(
                entry,
                new RenameUndoOptions { AnchorPath = anchor });

            Assert.Equal(RenameUndoResolveStatus.Ready, resolution.Status);
            Assert.True(string.Equals(renamedChild, resolution.CurrentPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(string.Equals(
                Path.Combine(anchor, "Austin White"),
                resolution.RestorePath,
                StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ShouldShowInGrid_HidesParentRowsWhenFilteringToAnchorChildren()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-grid-" + Guid.NewGuid().ToString("N"));
        var anchor = Path.Combine(root, "OF_Patreon");

        try
        {
            Directory.CreateDirectory(anchor);

            var parentEntry = new RenameLogEntry(
                1,
                null,
                Path.Combine(root, "My Games"),
                Path.Combine(root, "My-Games"));
            var childEntry = new RenameLogEntry(
                2,
                null,
                Path.Combine(anchor, "Austin White"),
                Path.Combine(root, "My-Games", "OF-Patreon", "Austin-White"));

            var options = new RenameUndoOptions
            {
                AnchorPath = anchor,
                FilterToAnchorChildren = true,
                HideParentCorrectedRows = true
            };

            var parentResolution = RenameUndoResolver.Resolve(parentEntry, options);
            var childResolution = RenameUndoResolver.Resolve(childEntry, options);

            Assert.False(RenameUndoResolver.ShouldShowInGrid(parentResolution, options));
            Assert.Equal(RenameUndoResolveStatus.NotUnderAnchor, parentResolution.Status);
            Assert.True(RenameUndoResolver.ShouldShowInGrid(childResolution, options));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UndoRenames_RestoresRenamedFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), "xnpb-undo-" + Guid.NewGuid().ToString("N"));
        var renamedDir = Path.Combine(root, "ascii-folder");
        var renamedFile = Path.Combine(renamedDir, "photo.jpg");
        var originalDir = Path.Combine(root, "unicode-モ");

        try
        {
            Directory.CreateDirectory(renamedDir);
            File.WriteAllText(renamedFile, "test");

            var service = new MediaPathRenameService();
            var entries = new[]
            {
                new RenameLogEntry(10, DateTime.UtcNow, originalDir, renamedDir)
            };

            var result = service.UndoRenames(entries, new RenameUndoOptions());

            Assert.Equal(1, result.CompletedCount);
            Assert.Equal(0, result.SkippedCount);
            Assert.True(Directory.Exists(originalDir));
            Assert.True(File.Exists(Path.Combine(originalDir, "photo.jpg")));
            Assert.False(Directory.Exists(renamedDir));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void UndoRenames_SkipsMissingCurrentPath()
    {
        var service = new MediaPathRenameService();
        var entries = new[]
        {
            new RenameLogEntry(1, null, "C:\\missing\\old.jpg", "C:\\missing\\new.jpg")
        };

        var result = service.UndoRenames(entries);

        Assert.Equal(0, result.CompletedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Contains("not found", result.SkippedOperations[0].Reason, StringComparison.OrdinalIgnoreCase);
    }
}
