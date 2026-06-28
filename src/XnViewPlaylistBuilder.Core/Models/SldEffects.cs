namespace XnViewPlaylistBuilder.Core.Models;

public static class SldEffects
{
    public const int Count = 56;

    public static IReadOnlyList<int> All { get; } = Enumerable.Range(1, Count).ToArray();

    public static bool IsAllEffects(IReadOnlyList<int> effects) =>
        effects.Count == Count && All.All(id => effects.Contains(id));
}
