using System.Globalization;

namespace XnViewPlaylistBuilder.Core.Models;

public sealed record TextPositionChoice(int Value, string Label);

/// <summary>
/// XnView MP v2 TextPosition values (0–8), row-major 3×3 grid:
/// 0 top-left, 1 top-center, 2 top-right,
/// 3 left-center, 4 center, 5 right-center,
/// 6 bottom-left, 7 bottom-center, 8 bottom-right.
/// </summary>
public static class SldTextPosition
{
    public const int Default = 0;

    public static IReadOnlyList<TextPositionChoice> Choices { get; } =
    [
        new(0, "Top left"),
        new(1, "Top center"),
        new(2, "Top right"),
        new(3, "Left center"),
        new(4, "Center"),
        new(5, "Right center"),
        new(6, "Bottom left"),
        new(7, "Bottom center"),
        new(8, "Bottom right")
    ];

    public static int Normalize(object? rawValue, int fallback = Default)
    {
        switch (rawValue)
        {
            case int value when IsValid(value):
                return value;
            case TextPositionChoice choice when IsValid(choice.Value):
                return choice.Value;
            case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && IsValid(parsed):
                return parsed;
            default:
                return fallback;
        }
    }

    public static string GetLabel(int value) =>
        Choices.FirstOrDefault(choice => choice.Value == value)?.Label ?? $"Unknown ({value})";

    public static bool IsValid(int value) => value is >= 0 and <= 8;
}
