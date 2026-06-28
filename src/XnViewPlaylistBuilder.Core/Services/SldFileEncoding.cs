using System.Text;

namespace XnViewPlaylistBuilder.Core.Services;

/// <summary>
/// XnView MP reads .sld as UTF-8 on some systems, but a UTF-8 BOM corrupts the v2 header
/// when XnView falls back to legacy ANSI decoding. Write UTF-8 without BOM for compatibility.
/// </summary>
public static class SldFileEncoding
{
    public static readonly Encoding WriteEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public static string ReadAllText(string path) =>
        File.ReadAllText(path, DetectEncoding(path));

    public static string[] ReadAllLines(string path) =>
        File.ReadAllLines(path, DetectEncoding(path));

    public static void WriteAllText(string path, string content) =>
        File.WriteAllText(path, content, WriteEncoding);

    public static Encoding DetectEncoding(string path)
    {
        var bytes = File.ReadAllBytes(path);
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return WriteEncoding;
        }

        if (LooksLikeUtf8(bytes))
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        return Encoding.Default;
    }

    internal static bool LooksLikeUtf8(ReadOnlySpan<byte> bytes)
    {
        var index = 0;
        while (index < bytes.Length)
        {
            if (bytes[index] <= 0x7F)
            {
                index++;
                continue;
            }

            var leading = bytes[index];
            int expectedContinuation;
            if ((leading & 0xE0) == 0xC0)
            {
                expectedContinuation = 1;
            }
            else if ((leading & 0xF0) == 0xE0)
            {
                expectedContinuation = 2;
            }
            else if ((leading & 0xF8) == 0xF0)
            {
                expectedContinuation = 3;
            }
            else
            {
                return false;
            }

            if (index + expectedContinuation >= bytes.Length)
            {
                return false;
            }

            for (var i = 1; i <= expectedContinuation; i++)
            {
                if ((bytes[index + i] & 0xC0) != 0x80)
                {
                    return false;
                }
            }

            index += expectedContinuation + 1;
        }

        return true;
    }

    public static bool IsRepresentableInSystemEncoding(string value)
    {
        var encoding = Encoding.Default;
        var bytes = encoding.GetBytes(value);
        var roundTrip = encoding.GetString(bytes);
        return string.Equals(value, roundTrip, StringComparison.Ordinal);
    }

    public static int CountNonAsciiPaths(IEnumerable<string> paths) =>
        paths.Count(path => path.Any(ch => ch > 127));
}
