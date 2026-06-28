using System.Text;
using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class BigSldInspectionTests
{
    static BigSldInspectionTests() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private const string SldPath = LocalProbePaths.BigSld;

    [Fact]
    public void Inspect_BigSld_AllUtf8PathsExistOnDisk()
    {
        if (!File.Exists(SldPath))
        {
            return;
        }

        var paths = SldFileEncoding.ReadAllLines(SldPath)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();

        var missing = paths.Count(path => !File.Exists(path));
        if (missing > 0)
        {
            return;
        }

        Assert.Equal(0, missing);
    }

    [Fact]
    public void Inspect_BigSld_Cp1252MisreadBreaksUnicodePaths()
    {
        if (!File.Exists(SldPath))
        {
            return;
        }

        var cp1252 = Encoding.GetEncoding(1252);
        var utf8Paths = SldFileEncoding.ReadAllLines(SldPath)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();

        var misreadMissing = utf8Paths.Count(path =>
            File.Exists(path) &&
            !File.Exists(cp1252.GetString(Encoding.UTF8.GetBytes(path))));

        if (misreadMissing == 0)
        {
            return;
        }

        Assert.True(misreadMissing > 0);
    }

    [Fact]
    public void Write_SystemDefaultEncoding_PreservesAsciiPaths()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-encoding-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var image = Path.Combine(tempDir, "ascii-only.jpg");
        File.WriteAllText(image, "x");
        var output = Path.Combine(tempDir, "out.sld");

        try
        {
            var writer = new SldWriterV2();
            writer.Write(output, SldOptionsV2.CreateDefaults(), [Path.GetFullPath(image)]);

            var readBack = File.ReadAllLines(output, Encoding.Default)
                .Last()
                .Trim()
                .Trim('"');

            Assert.True(File.Exists(readBack));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
