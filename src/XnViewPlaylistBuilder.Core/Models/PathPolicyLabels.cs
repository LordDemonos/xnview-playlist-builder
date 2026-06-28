namespace XnViewPlaylistBuilder.Core.Models;

public sealed record PathPolicyChoice(PathPolicy Policy, string Label);

public static class PathPolicyLabels
{
    public static IReadOnlyList<PathPolicyChoice> Choices { get; } =
    [
        new(PathPolicy.AbsoluteLocal, "Absolute — local drive letters"),
        new(PathPolicy.AbsoluteUnc, "Absolute — preserve UNC paths"),
        new(PathPolicy.RelativeToSld, "Relative to .sld file location (experimental)")
    ];

    public static string GetLabel(PathPolicy policy) =>
        Choices.FirstOrDefault(choice => choice.Policy == policy)?.Label ?? policy.ToString();
}
