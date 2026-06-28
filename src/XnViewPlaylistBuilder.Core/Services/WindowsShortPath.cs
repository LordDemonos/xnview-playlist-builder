using System.Runtime.InteropServices;
using System.Text;

namespace XnViewPlaylistBuilder.Core.Services;

public static class WindowsShortPath
{
    public static string? TryGetShortPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(512);
            var length = GetShortPathName(path, buffer, (uint)buffer.Capacity);
            if (length == 0 || length >= buffer.Capacity)
            {
                return null;
            }

            var shortPath = buffer.ToString();
            return string.Equals(shortPath, path, StringComparison.OrdinalIgnoreCase) ? null : shortPath;
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, BestFitMapping = false, SetLastError = true)]
    private static extern uint GetShortPathName(string longPath, StringBuilder shortPath, uint bufferSize);
}
