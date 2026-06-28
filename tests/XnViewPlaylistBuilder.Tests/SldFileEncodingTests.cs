using System.Text;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Tests;

public class SldFileEncodingTests
{
    [Fact]
    public void WriteEncoding_DoesNotEmitUtf8Bom()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-bom-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var file = Path.Combine(tempDir, "sample.sld");

        try
        {
            SldFileEncoding.WriteAllText(file, "# Slide Show Sequence v2\r\n");
            var bytes = File.ReadAllBytes(file);
            Assert.NotEqual(0xEF, bytes[0]);
            Assert.Equal((byte)'#', bytes[0]);
            Assert.Equal(SldFileEncoding.WriteEncoding.WebName, SldFileEncoding.DetectEncoding(file).WebName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void DetectEncoding_RecognizesLegacyUtf8WithoutBom()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"xnpb-detect-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var legacyPath = Path.Combine(tempDir, "legacy.sld");
            File.WriteAllText(legacyPath, "\"E:\\\\test\\\\りな.jpg\"", new UTF8Encoding(false));
            Assert.Equal("utf-8", SldFileEncoding.DetectEncoding(legacyPath).WebName);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
