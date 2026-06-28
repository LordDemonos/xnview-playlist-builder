using System.Drawing;
using System.Globalization;

namespace XnViewPlaylistBuilder.Services;

public static class XnViewFontFormat
{
    public static string FromDrawingFont(Font font)
    {
        var weight = font.Bold ? 75 : 50;
        var size = font.SizeInPoints.ToString(CultureInfo.InvariantCulture);
        return $"{font.FontFamily.Name},{size},-1,5,{weight},0,0,0,0,0";
    }

    public static bool TryParse(string? value, out Font? font)
    {
        font = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(',');
        if (parts.Length < 2)
        {
            return false;
        }

        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var size))
        {
            size = 8.25f;
        }

        var family = parts[0].Trim();
        var bold = parts.Length > 4 && parts[4].Trim() is "75" or "87";

        try
        {
            font = new Font(family, size, bold ? FontStyle.Bold : FontStyle.Regular);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
