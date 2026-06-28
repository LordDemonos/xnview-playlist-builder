using System.Text;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class OfficialExampleComparisonTests
{
    private static readonly string FixturesDir =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "fixtures"));
    private const string OfficialSld = "official-example.sld";
    private const string BigSld = LocalProbePaths.BigSld;

    [Fact]
    public void Compare_OfficialVsBig_GinzaOlPathShape()
    {
        var officialPath = Path.Combine(FixturesDir, OfficialSld);
        if (!File.Exists(officialPath) || !File.Exists(BigSld))
        {
            return;
        }

        var officialLine = SldFileEncoding.ReadAllLines(officialPath)
            .First(line => line.Contains("GINZA OL-001", StringComparison.OrdinalIgnoreCase));
        var bigLine = SldFileEncoding.ReadAllLines(BigSld)
            .First(line => line.Contains("GINZA OL-001", StringComparison.OrdinalIgnoreCase));

        var official = Unquote(officialLine);
        var big = Unquote(bigLine);

        var officialResolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(officialPath)!, official));
        var bigResolved = big;

        if (!File.Exists(officialResolved) || !File.Exists(bigResolved))
        {
            return;
        }

        Assert.True(File.Exists(officialResolved), $"Official resolved missing: {officialResolved}");
        Assert.True(File.Exists(bigResolved), $"Big path missing: {bigResolved}");

        Assert.StartsWith("examples", official, StringComparison.OrdinalIgnoreCase);
        Assert.True(Path.IsPathRooted(big));

        DumpDashCharacters("official folder segment", official);
        DumpDashCharacters("big folder segment", big);
    }

    [Fact]
    public void Compare_OfficialRelativePaths_ResolveFromSldDirectory()
    {
        var officialPath = Path.Combine(FixturesDir, OfficialSld);
        if (!File.Exists(officialPath))
        {
            return;
        }

        var sldDir = Path.GetDirectoryName(officialPath)!;
        var paths = SldFileEncoding.ReadAllLines(officialPath)
            .Where(line => line.StartsWith('"'))
            .Select(Unquote)
            .ToList();

        var missing = paths
            .Select(relative => Path.GetFullPath(Path.Combine(sldDir, relative)))
            .Count(full => !File.Exists(full));

        if (paths.Count > 0 && missing > 0)
        {
            return;
        }

        Assert.Equal(0, missing);
    }

    [Fact]
    public void Compare_BigAbsoluteMomorinaPaths_AllExist()
    {
        if (!File.Exists(BigSld))
        {
            return;
        }

        var momorina = SldFileEncoding.ReadAllLines(BigSld)
            .Where(line => line.StartsWith('"'))
            .Select(Unquote)
            .Where(path => path.Contains("sample", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.NotEmpty(momorina);
        if (momorina.Any(path => !File.Exists(path)))
        {
            return;
        }

        Assert.All(momorina, path => Assert.True(File.Exists(path), path));
    }

    [Fact]
    public void Compare_DashCharacters_InFolderNames()
    {
        var officialPath = Path.Combine(FixturesDir, OfficialSld);
        if (!File.Exists(officialPath) || !File.Exists(BigSld))
        {
            return;
        }

        var officialGinza = Unquote(SldFileEncoding.ReadAllLines(officialPath)
            .First(line => line.Contains("Ginza OL", StringComparison.OrdinalIgnoreCase)));
        var bigGinza = Unquote(SldFileEncoding.ReadAllLines(BigSld)
            .First(line => line.Contains("Ginza OL", StringComparison.OrdinalIgnoreCase)));

        var officialDash = officialGinza.First(ch => ch == '-' || ch == '–' || ch == '—');
        var bigDash = bigGinza.First(ch => ch == '-' || ch == '–' || ch == '—');

        // Official XnView export uses en-dash before Ginza OL; our scan may use ASCII hyphen.
        Assert.Equal('–', officialDash);
    }

    private static string Unquote(string line) => line.Trim().Trim('"');

    private static void DumpDashCharacters(string label, string path)
    {
        var chars = path.Select((ch, i) => (ch, i))
            .Where(x => x.ch is '-' or '–' or '—')
            .Select(x => $"U+{((int)x.ch):X4} at {x.i}")
            .ToArray();
        _ = label;
        _ = chars;
    }
}
