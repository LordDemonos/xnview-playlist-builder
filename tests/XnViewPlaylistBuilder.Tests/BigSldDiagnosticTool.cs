using System.Text;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

/// <summary>
/// Run with: dotnet test --filter "Dump_BigSld_Diagnostics" -v n
/// </summary>
public class BigSldDiagnosticTool
{
    static BigSldDiagnosticTool() =>
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    [Fact]
    public void Dump_BigSld_Diagnostics()
    {
        const string sldPath = LocalProbePaths.BigSld;
        if (!File.Exists(sldPath))
        {
            return;
        }

        var bytes = File.ReadAllBytes(sldPath);
        var hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        var utf8Paths = SldFileEncoding.ReadAllLines(sldPath)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();

        var cp1252 = Encoding.GetEncoding(1252);
        var rawUtf8Lines = File.ReadAllLines(sldPath, new UTF8Encoding(false))
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();
        var ansiLines = File.ReadAllLines(sldPath, Encoding.Default)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();

        var asciiOnly = utf8Paths.Count(p => p.All(ch => ch <= 127));
        var nonAscii = utf8Paths.Count - asciiOnly;
        var shortPaths = utf8Paths.Count(p => p.Contains('~', StringComparison.Ordinal));
        var utf8Exists = utf8Paths.Count(File.Exists);
        var ansiExists = ansiLines.Count(File.Exists);
        var cp1252SimExists = utf8Paths.Count(p => File.Exists(SimulateAnsiMisread(p, cp1252)));

        var reportPath = Path.Combine(Path.GetTempPath(), "big-sld-diagnostic.txt");
        using var writer = new StreamWriter(reportPath, false, Encoding.UTF8);
        writer.WriteLine($"File: {sldPath}");
        writer.WriteLine($"Has UTF-8 BOM: {hasBom}");
        writer.WriteLine($"Path entries: {utf8Paths.Count}");
        writer.WriteLine($"ASCII-only paths: {asciiOnly}");
        writer.WriteLine($"Non-ASCII paths: {nonAscii}");
        writer.WriteLine($"Short (8.3) paths: {shortPaths}");
        writer.WriteLine($"Exist when read UTF-8: {utf8Exists}/{utf8Paths.Count}");
        writer.WriteLine($"Exist when read Encoding.Default: {ansiExists}/{ansiLines.Count}");
        writer.WriteLine($"Exist if UTF-8 path misread as CP1252: {cp1252SimExists}/{utf8Paths.Count}");

        var cp1252WholeFile = cp1252.GetString(bytes);
        var cp1252WholePaths = cp1252WholeFile
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.StartsWith('"'))
            .Select(line => line.Trim().Trim('"'))
            .ToList();
        writer.WriteLine($"Exist when entire BOM file decoded as CP1252: {cp1252WholePaths.Count(File.Exists)}/{cp1252WholePaths.Count}");
        writer.WriteLine($"Header if whole file is CP1252: {cp1252WholeFile.Split('\n')[0]}");
        writer.WriteLine();
        writer.WriteLine("=== Sample: UTF-8 ok, ANSI read fails ===");
        foreach (var path in utf8Paths.Where(File.Exists).Take(500))
        {
            var ansiPath = ansiLines[utf8Paths.IndexOf(path)];
            if (File.Exists(ansiPath))
            {
                continue;
            }

            writer.WriteLine($"UTF-8: {path}");
            writer.WriteLine($"ANSI:  {ansiPath}");
            writer.WriteLine();
        }

        writer.WriteLine("=== Short path samples ===");
        foreach (var path in utf8Paths.Where(p => p.Contains('~')).Take(10))
        {
            writer.WriteLine(path);
        }

        Assert.True(File.Exists(reportPath));
    }

    private static string SimulateAnsiMisread(string utf8Path, Encoding cp1252)
    {
        var utf8Bytes = Encoding.UTF8.GetBytes(utf8Path);
        return cp1252.GetString(utf8Bytes);
    }
}
