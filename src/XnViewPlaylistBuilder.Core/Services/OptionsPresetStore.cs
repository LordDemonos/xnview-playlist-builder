using System.Text.Json;
using System.Text.Json.Serialization;
using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;

namespace XnViewPlaylistBuilder.Core.Services;

public sealed class OptionsPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly SettingsStore _settingsStore;
    private readonly string? _presetsDirectoryOverride;

    public OptionsPresetStore(SettingsStore? settingsStore = null, string? presetsDirectory = null)
    {
        _settingsStore = settingsStore ?? new SettingsStore();
        _presetsDirectoryOverride = presetsDirectory;
    }

    public string PresetsDirectory =>
        _presetsDirectoryOverride ?? Path.Combine(_settingsStore.SettingsDirectory, "presets");

    public IReadOnlyList<string> ListPresetNames()
    {
        if (!Directory.Exists(PresetsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(PresetsDirectory, "*.json")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public SldOptionsV2 Load(string name)
    {
        var path = GetPresetPath(name);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Preset not found: {name}", path);
        }

        var json = File.ReadAllText(path);
        var preset = JsonSerializer.Deserialize<OptionsPresetFile>(json, JsonOptions)
            ?? throw new InvalidDataException($"Preset file is invalid: {path}");

        AppLog.Info($"Loaded options preset: {name}");
        preset.Options.NormalizeForWrite();
        return preset.Options;
    }

    public void Save(string name, SldOptionsV2 options)
    {
        var sanitized = SanitizePresetName(name);
        Directory.CreateDirectory(PresetsDirectory);

        var preset = new OptionsPresetFile
        {
            Name = sanitized,
            Options = CloneOptions(options)
        };

        var path = GetPresetPath(sanitized);
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions));
        AppLog.Info($"Saved options preset: {sanitized}");
    }

    public void Delete(string name)
    {
        var path = GetPresetPath(name);
        if (File.Exists(path))
        {
            File.Delete(path);
            AppLog.Info($"Deleted options preset: {name}");
        }
    }

    private string GetPresetPath(string name) =>
        Path.Combine(PresetsDirectory, $"{SanitizePresetName(name)}.json");

    private static string SanitizePresetName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new ArgumentException("Preset name is required.", nameof(name));
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            trimmed = trimmed.Replace(invalid, '_');
        }

        return trimmed;
    }

    private static SldOptionsV2 CloneOptions(SldOptionsV2 options) =>
        new()
        {
            UseTimer = options.UseTimer,
            Timer = options.Timer,
            Loop = options.Loop,
            FullScreen = options.FullScreen,
            WinWidth = options.WinWidth,
            WinHeight = options.WinHeight,
            Stretch = options.Stretch,
            RandomOrder = options.RandomOrder,
            ShowInfo = options.ShowInfo,
            Info = options.Info,
            TitleBar = options.TitleBar,
            OnTop = options.OnTop,
            CursorAutoHide = options.CursorAutoHide,
            BackgroundColor = options.BackgroundColor,
            TextColor = options.TextColor,
            UseTextBackColor = options.UseTextBackColor,
            TextPosition = options.TextPosition,
            TextBackColor = options.TextBackColor,
            Opacity = options.Opacity,
            Font = options.Font,
            EffectDuration = options.EffectDuration,
            Effects = options.Effects.ToArray()
        };

    private sealed class OptionsPresetFile
    {
        public string Name { get; set; } = string.Empty;
        public SldOptionsV2 Options { get; set; } = SldOptionsV2.CreateDefaults();
    }
}
