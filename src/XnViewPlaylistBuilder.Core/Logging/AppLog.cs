namespace XnViewPlaylistBuilder.Core.Logging;

public static class AppLog
{
    private static readonly object Gate = new();
    private static string? _logDirectory;
    private static string? _currentLogFile;

    public static string LogDirectory
    {
        get
        {
            if (_logDirectory is not null)
            {
                return _logDirectory;
            }

            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XnViewPlaylistBuilder",
                "logs");
            Directory.CreateDirectory(_logDirectory);
            return _logDirectory;
        }
    }

    public static string CurrentLogFile
    {
        get
        {
            if (_currentLogFile is not null)
            {
                return _currentLogFile;
            }

            _currentLogFile = Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
            return _currentLogFile;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Warning(string message) => Write("WARN", message);

    public static void Error(string message, Exception? ex = null)
    {
        var text = ex is null ? message : $"{message}{Environment.NewLine}{ex}";
        Write("ERROR", text);
    }

    public static void Debug(string message) => Write("DEBUG", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
        lock (Gate)
        {
            try
            {
                File.AppendAllText(CurrentLogFile, line + Environment.NewLine);
            }
            catch
            {
                // Avoid recursive logging failures.
            }
        }

        System.Diagnostics.Trace.WriteLine(line);
    }
}
