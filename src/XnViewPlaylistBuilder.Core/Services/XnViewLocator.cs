namespace XnViewPlaylistBuilder.Core.Services;

public static class XnViewLocator
{
    private static readonly string[] CandidatePaths =
    [
        @"C:\Program Files\XnViewMP\xnviewmp.exe",
        @"C:\Program Files (x86)\XnViewMP\xnviewmp.exe",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "XnViewMP", "xnviewmp.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "XnViewMP", "xnviewmp.exe")
    ];

    public static string? DetectInstallPath()
    {
        foreach (var path in CandidatePaths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
