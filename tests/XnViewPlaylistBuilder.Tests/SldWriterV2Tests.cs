using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class SldWriterV2Tests
{
    [Fact]
    public void BuildContent_StartsWithV2HeaderAndOptionKeys()
    {
        var writer = new SldWriterV2();
        var options = SldOptionsV2.CreateDefaults();
        options.Timer = 15;
        options.Loop = true;
        options.RandomOrder = true;

        var content = writer.BuildContent(options, [@"D:\media\one.jpg"]);

        var lines = content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(SldWriterV2.HeaderLine, lines[0].TrimEnd('\r'));
        Assert.Equal("UseTimer = 1", lines[1]);
        Assert.Equal("Timer = 15", lines[2]);
        Assert.Contains("Effects = ", lines[22]);
        Assert.Equal(@"""D:\media\one.jpg""", lines[23]);
    }

    [Fact]
    public void BuildOptionLines_ContainsAllTwentyTwoKeysInOrder()
    {
        var writer = new SldWriterV2();
        var lines = writer.BuildOptionLines(SldOptionsV2.CreateDefaults());

        Assert.Equal(22, lines.Count);
        Assert.StartsWith("UseTimer =", lines[0]);
        Assert.StartsWith("Effects =", lines[21]);
    }

    [Fact]
    public void Write_CreatesFileWithQuotedPaths()
    {
        var writer = new SldWriterV2();
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-writer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var output = Path.Combine(tempDir, "out.sld");
        var image = Path.Combine(tempDir, "a.jpg");
        File.WriteAllText(image, "x");

        try
        {
            writer.Write(output, SldOptionsV2.CreateDefaults(), [Path.GetFullPath(image)]);

            Assert.True(File.Exists(output));
            var bytes = File.ReadAllBytes(output);
            Assert.Equal((byte)'#', bytes[0]);
            var text = SldFileEncoding.ReadAllText(output);
            Assert.StartsWith(SldWriterV2.HeaderLine, text);
            Assert.Contains($"\"{Path.GetFullPath(image)}\"", text);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SerializePaths_UsesStoredPathWhenFileMissing()
    {
        var writer = new SldWriterV2();
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-serialize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var existing = Path.Combine(tempDir, "exists.jpg");
        File.WriteAllText(existing, "x");

        try
        {
            var paths = writer.SerializePaths(
            [
                new MediaEntry { AbsolutePath = existing, SourceRootIndex = 0 },
                new MediaEntry
                {
                    AbsolutePath = @"missing\folder\photo.jpg",
                    StoredPath = @"missing\folder\photo.jpg",
                    SourceRootIndex = 0
                }
            ],
            PathPolicy.AbsoluteLocal,
            Path.Combine(tempDir, "out.sld"),
            anchorPath: null);

            Assert.Equal(2, paths.Count);
            Assert.Equal(Path.GetFullPath(existing), paths[0]);
            Assert.Equal(@"missing\folder\photo.jpg", paths[1]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SerializePaths_WritesAbsolutePathWhenStoredPathIsStale()
    {
        var writer = new SldWriterV2();
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-serialize-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var output = Path.Combine(tempDir, "out.sld");
        var updatedPath = Path.Combine(tempDir, "renamed", "photo.jpg");
        Directory.CreateDirectory(Path.GetDirectoryName(updatedPath)!);
        File.WriteAllText(updatedPath, "x");

        try
        {
            var paths = writer.SerializePaths(
            [
                new MediaEntry
                {
                    AbsolutePath = updatedPath,
                    StoredPath = Path.Combine(tempDir, "old", "photo.jpg"),
                    SourceRootIndex = 0
                }
            ],
            PathPolicy.AbsoluteLocal,
            output,
            anchorPath: null);

            Assert.Single(paths);
            Assert.Equal(Path.GetFullPath(updatedPath), paths[0]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
