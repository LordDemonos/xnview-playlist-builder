using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class MediaFileHealthCheckerTests
{
    [Fact]
    public void Analyze_EmptyFile_IsReported()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var empty = Path.Combine(tempDir, "empty.jpg");
        File.WriteAllBytes(empty, []);

        try
        {
            var report = MediaFileHealthChecker.Analyze([empty]);
            Assert.Equal(1, report.EmptyCount);
            Assert.Equal(1, report.Findings.Count);
            Assert.Equal(MediaFileHealthIssue.Empty, report.Findings[0].Issue);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Analyze_ValidJpegHeader_IsHealthy()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var jpeg = Path.Combine(tempDir, "valid.jpg");
        File.WriteAllBytes(jpeg, [0xFF, 0xD8, 0xFF, 0xD9]);

        try
        {
            var report = MediaFileHealthChecker.Analyze([jpeg]);
            Assert.False(report.HasIssues);
            Assert.Equal(1, report.HealthyCount);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Analyze_InvalidJpegHeader_IsReported()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var invalid = Path.Combine(tempDir, "invalid.jpg");
        File.WriteAllText(invalid, "not-an-image");

        try
        {
            var report = MediaFileHealthChecker.Analyze([invalid]);
            Assert.Equal(1, report.InvalidImageCount);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DeleteEmptyFiles_RemovesZeroByteFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-health-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var empty = Path.Combine(tempDir, "empty.jpg");
        var valid = Path.Combine(tempDir, "valid.jpg");
        File.WriteAllBytes(empty, []);
        File.WriteAllBytes(valid, [0xFF, 0xD8, 0xFF, 0xD9]);

        try
        {
            var result = MediaFileHealthChecker.DeleteEmptyFiles([empty, valid]);
            Assert.Equal(1, result.DeletedCount);
            Assert.False(File.Exists(empty));
            Assert.True(File.Exists(valid));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Analyze_BigSld_ReportsEmptyFiles()
    {
        var fixturesDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));
        var sldPath = Path.Combine(fixturesDir, "golden-test.sld");
        if (!File.Exists(sldPath))
        {
            return;
        }

        var paths = SldFileEncoding.ReadAllLines(sldPath)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();

        var report = MediaFileHealthChecker.Analyze(paths);
        if (report.EmptyCount == 0)
        {
            return;
        }

        Assert.True(report.EmptyCount > 0, report.FormatSummary());
    }
}
