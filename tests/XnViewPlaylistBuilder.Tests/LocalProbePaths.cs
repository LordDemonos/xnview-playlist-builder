namespace XnViewPlaylistBuilder.Tests;

/// <summary>
/// Optional local paths for machine-specific probe tests. Probes skip when files are missing.
/// Override with environment variables on a developer machine if needed.
/// </summary>
internal static class LocalProbePaths
{
    internal const string MediaRoot = @"D:\media";
    internal const string UncShareRoot = @"\\server\share";
    internal const string BigSld = @"D:\testdata\xnview\big.sld";

    internal static string? ProbeMediaRoot =>
        Environment.GetEnvironmentVariable("XNPB_PROBE_MEDIA_ROOT");
}
