using System.Text.Json;
using System.Text.Json.Serialization;
using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed class AppSettings
{
    public string? LastBrowseFolder { get; set; }
    public string? LastSaveFolder { get; set; }
    public string? LastPresetName { get; set; }
    public string? XnViewMpPath { get; set; }
    public PathPolicy DefaultPathPolicy { get; set; } = PathPolicy.AbsoluteLocal;
    public List<string> ImageExtensions { get; set; } = FolderScanner.DefaultExtensions.ToList();
    public bool DefaultIncludeSubfolders { get; set; } = true;
    public bool AllowDuplicates { get; set; }
    public bool UseXnViewRelativePathsForUnicode { get; set; }
    public bool WriteActionLogs { get; set; } = true;
    public SldOptionsV2 DefaultOptions { get; set; } = SldOptionsV2.CreateDefaults();
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string SettingsDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "XnViewPlaylistBuilder");

    public string SettingsFilePath => Path.Combine(SettingsDirectory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
            {
                AppLog.Info("Settings file not found; using defaults.");
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            AppLog.Info($"Loaded settings from {SettingsFilePath}");
            return settings;
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to load settings; using defaults.", ex);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsFilePath, json);
            AppLog.Info($"Saved settings to {SettingsFilePath}");
        }
        catch (Exception ex)
        {
            AppLog.Error("Failed to save settings.", ex);
            throw;
        }
    }
}
