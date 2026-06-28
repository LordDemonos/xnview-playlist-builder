using System.IO;
using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder.Cli;

public static class CliRunner
{
    public static int Run(string[] args)
    {
        AppLog.Info($"CLI started with args: {string.Join(' ', args)}");

        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintHelp();
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            var parsed = ParseArgs(args);
            if (parsed.Folders.Count == 0)
            {
                AppLog.Error("No folders provided. Use --add.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(parsed.OutputPath))
            {
                AppLog.Error("Output path required. Use --out.");
                return 1;
            }

            var settingsStore = new SettingsStore();
            var settings = settingsStore.Load();
            var scanner = new FolderScanner(settings.ImageExtensions);
            var service = new PlaylistService(scanner);

            var sources = FolderSourceCollapser.Collapse(
                    parsed.Folders
                        .Select(folder => new FolderSource
                        {
                            AbsolutePath = folder,
                            IncludeSubfolders = parsed.Recursive
                        }))
                .Roots;

            var scan = service.ScanFolders(sources);
            service.SavePlaylist(
                parsed.OutputPath,
                settings.DefaultOptions,
                scan.Entries,
                settings.DefaultPathPolicy);

            settings.LastBrowseFolder = BrowseFolderHelper.GetNextBrowseDirectory(parsed.Folders);
            settings.LastSaveFolder = Path.GetDirectoryName(Path.GetFullPath(parsed.OutputPath));
            settingsStore.Save(settings);

            Console.WriteLine($"Wrote {parsed.OutputPath} ({scan.Entries.Count} entries)");
            Console.WriteLine($"Log: {AppLog.CurrentLogFile}");
            return 0;
        }
        catch (Exception ex)
        {
            AppLog.Error("CLI failed.", ex);
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine($"See log: {AppLog.CurrentLogFile}");
            return 1;
        }
    }

    private static ParsedArgs ParseArgs(string[] args)
    {
        var parsed = new ParsedArgs();
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--add":
                    i = ReadValues(args, i + 1, parsed.Folders);
                    break;
                case "--out":
                    parsed.OutputPath = ReadValue(args, ref i);
                    break;
                case "--recursive":
                    parsed.Recursive = true;
                    break;
                case "--no-recursive":
                    parsed.Recursive = false;
                    break;
                default:
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }
        }

        return parsed;
    }

    private static int ReadValues(string[] args, int start, List<string> target)
    {
        var i = start;
        while (i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal))
        {
            target.Add(args[i]);
            i++;
        }

        return i - 1;
    }

    private static string ReadValue(string[] args, ref int index)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"Missing value for {args[index]}");
        }

        index++;
        return args[index];
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
            XnView Playlist Builder (CLI)

            Usage:
              XnViewPlaylistBuilder.exe --add "D:\folder1" "D:\folder2" --out "D:\out.sld" [--recursive]

            Options:
              --add       One or more folder paths
              --out       Output .sld file path
              --recursive Include subfolders (default: true)
              --no-recursive
              --help      Show this help
            """);
    }

    private sealed class ParsedArgs
    {
        public List<string> Folders { get; } = [];
        public string? OutputPath { get; set; }
        public bool Recursive { get; set; } = true;
    }
}
