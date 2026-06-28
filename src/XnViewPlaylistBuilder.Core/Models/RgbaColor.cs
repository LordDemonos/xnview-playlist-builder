namespace XnViewPlaylistBuilder.Core.Models;

public readonly record struct RgbaColor(byte R, byte G, byte B, byte A)
{
    public static RgbaColor Black => new(0, 0, 0, 255);
    public static RgbaColor White => new(255, 255, 255, 255);
    public static RgbaColor Gray128 => new(128, 128, 128, 255);

    public string ToSldValue() => $"{R} {G} {B} {A}";

    public static bool TryParse(string? value, out RgbaColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        if (!byte.TryParse(parts[0], out var r) ||
            !byte.TryParse(parts[1], out var g) ||
            !byte.TryParse(parts[2], out var b) ||
            !byte.TryParse(parts[3], out var a))
        {
            return false;
        }

        color = new RgbaColor(r, g, b, a);
        return true;
    }

    public static RgbaColor Parse(string value) =>
        TryParse(value, out var color)
            ? color
            : throw new FormatException($"Invalid RGBA value: {value}");
}
