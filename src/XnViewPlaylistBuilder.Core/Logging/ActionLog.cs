namespace XnViewPlaylistBuilder.Core.Logging;

public enum ActionLogCategory
{
    Removals,
    FixNames,
    Rename
}

public static class ActionLog
{
    private static readonly object Gate = new();

    public static string GetCategoryDirectory(ActionLogCategory category)
    {
        var name = category switch
        {
            ActionLogCategory.Removals => "removals",
            ActionLogCategory.FixNames => "fix-names",
            ActionLogCategory.Rename => "rename",
            _ => "other"
        };

        var directory = Path.Combine(AppLog.LogDirectory, name);
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string BeginBatch(ActionLogCategory category, string header)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var prefix = category switch
        {
            ActionLogCategory.Removals => "removals",
            ActionLogCategory.FixNames => "fix-names",
            ActionLogCategory.Rename => "rename",
            _ => "action"
        };

        var filePath = Path.Combine(GetCategoryDirectory(category), $"{prefix}-{timestamp}.log");
        WriteLines(filePath, [
            $"# {header}",
            $"# Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            string.Empty
        ]);
        return filePath;
    }

    public static string GetDailyFile(ActionLogCategory category)
    {
        var prefix = category switch
        {
            ActionLogCategory.Removals => "removals",
            ActionLogCategory.FixNames => "fix-names",
            ActionLogCategory.Rename => "rename",
            _ => "action"
        };

        return Path.Combine(GetCategoryDirectory(category), $"{prefix}-{DateTime.Now:yyyyMMdd}.log");
    }

    public static void AppendBatch(string batchFilePath, IEnumerable<string> lines)
    {
        WriteLines(batchFilePath, lines);
    }

    public static void AppendBatchLine(string batchFilePath, string line) =>
        WriteLines(batchFilePath, [line]);

    public static void Info(ActionLogCategory category, string message) =>
        WriteCategory(category, "INFO", message);

    public static void Warning(ActionLogCategory category, string message) =>
        WriteCategory(category, "WARN", message);

    private static void WriteCategory(ActionLogCategory category, string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        WriteLines(GetDailyFile(category), [line]);
        System.Diagnostics.Trace.WriteLine(line);
    }

    private static void WriteLines(string filePath, IEnumerable<string> lines)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.AppendAllLines(filePath, lines);
            }
            catch
            {
                // Avoid recursive logging failures.
            }
        }
    }
}
