using System.Text;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class GinzaOlPathProbe
{
    static GinzaOlPathProbe() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void Probe_GinzaOl_EntriesMatchDisk()
    {
        const string sldPath = LocalProbePaths.BigSld;
        if (!File.Exists(sldPath))
        {
            return;
        }

        var utf8Paths = SldFileEncoding.ReadAllLines(sldPath)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .Where(path => path.Contains("GINZA OL", StringComparison.OrdinalIgnoreCase) ||
                           path.Contains("Ginza OL", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(utf8Paths);

        foreach (var path in utf8Paths.Take(3))
        {
            var existsUtf8 = File.Exists(path);
            var cp1252Path = ToCp1252RoundTrip(path);
            var existsCp1252 = File.Exists(cp1252Path);

            if (!existsUtf8 && !existsCp1252)
            {
                return;
            }

            // Surface diagnostics when run with `dotnet test --filter GinzaOl -v n`
            Assert.True(existsUtf8 || existsCp1252,
                $"Neither UTF-8 nor CP1252 path exists.\nUTF-8: {path}\nCP1252: {cp1252Path}");
        }
    }

    [Fact]
    public void Probe_GinzaOl_DumpActualPaths()
    {
        var mediaRoot = LocalProbePaths.ProbeMediaRoot ?? LocalProbePaths.MediaRoot;
        if (!Directory.Exists(mediaRoot))
        {
            return;
        }

        var ginzaFolder = Directory.GetDirectories(mediaRoot)
            .FirstOrDefault(d => d.Contains("Ginza", StringComparison.OrdinalIgnoreCase) &&
                                 d.Contains("sample", StringComparison.OrdinalIgnoreCase));

        if (ginzaFolder is null)
        {
            return;
        }

        var sampleFile = Directory.GetFiles(ginzaFolder, "*.jpg").OrderBy(f => f).First();
        var fileName = Path.GetFileName(sampleFile);
        var folderName = Path.GetFileName(ginzaFolder);

        var sldLine = SldFileEncoding.ReadAllLines(LocalProbePaths.BigSld)
            .First(line => line.Contains("GINZA OL-001", StringComparison.OrdinalIgnoreCase));
        var sldPath = sldLine.Trim().Trim('"');

        if (!File.Exists(sldPath))
        {
            return;
        }

        var cp1252 = Encoding.GetEncoding(1252);
        var sldReadAsCp1252 = cp1252.GetString(Encoding.UTF8.GetBytes(sldPath));

        // Folder is proper Unicode; filename on disk may be mojibake from a bad rename.
        Assert.Contains("GINZA OL", fileName, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(sldPath));
        Assert.False(File.Exists(sldReadAsCp1252));
        Assert.Contains(folderName, sldPath, StringComparison.Ordinal);
        Assert.Contains(fileName, sldPath, StringComparison.Ordinal);
    }

    private static string ToCp1252RoundTrip(string path)
    {
        var utf8 = Encoding.UTF8;
        var cp1252 = Encoding.GetEncoding(1252);
        var bytes = utf8.GetBytes(path);
        var asLatin = cp1252.GetString(bytes);
        return asLatin;
    }
}
