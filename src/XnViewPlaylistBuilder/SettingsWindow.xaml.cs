using System.IO;
using System.Windows;
using Microsoft.Win32;
using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        LoadFromSettings();
    }

    public AppSettings ResultSettings { get; private set; } = null!;

    private void LoadFromSettings()
    {
        PathPolicyComboBox.ItemsSource = PathPolicyLabels.Choices;

        var policy = _settings.DefaultPathPolicy == PathPolicy.RelativeToAnchor
            ? PathPolicy.AbsoluteLocal
            : _settings.DefaultPathPolicy;

        PathPolicyComboBox.SelectedItem = PathPolicyLabels.Choices
            .FirstOrDefault(choice => choice.Policy == policy)
            ?? PathPolicyLabels.Choices[0];

        XnViewPathTextBox.Text = _settings.XnViewMpPath ?? string.Empty;
        ExtensionsTextBox.Text = string.Join(", ", _settings.ImageExtensions);
        DefaultIncludeSubfoldersCheckBox.IsChecked = _settings.DefaultIncludeSubfolders;
        AllowDuplicatesCheckBox.IsChecked = _settings.AllowDuplicates;
        WriteActionLogsCheckBox.IsChecked = _settings.WriteActionLogs;
        UseXnViewRelativePathsCheckBox.IsChecked = _settings.UseXnViewRelativePathsForUnicode;
        UpdatePolicyUi();
        PathPolicyComboBox.SelectionChanged += (_, _) => UpdatePolicyUi();
    }

    private void UpdatePolicyUi()
    {
        var policy = PathPolicyComboBox.SelectedItem is PathPolicyChoice choice
            ? choice.Policy
            : PathPolicy.AbsoluteLocal;

        RelativePolicyWarningTextBlock.Visibility =
            policy == PathPolicy.RelativeToSld ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BrowseXnViewButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select XnView MP executable",
            Filter = "Executable (xnviewmp.exe)|xnviewmp.exe|All files (*.*)|*.*",
            FileName = "xnviewmp.exe",
            InitialDirectory = GetExistingDirectory(Path.GetDirectoryName(XnViewPathTextBox.Text))
        };

        if (dialog.ShowDialog() == true)
        {
            XnViewPathTextBox.Text = dialog.FileName;
        }
    }

    private void DetectXnViewButton_Click(object sender, RoutedEventArgs e)
    {
        var path = XnViewLocator.DetectInstallPath();
        if (path is null)
        {
            MessageBox.Show(this, "Could not find xnviewmp.exe in Program Files.", "Auto-detect", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        XnViewPathTextBox.Text = path;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (PathPolicyComboBox.SelectedItem is not PathPolicyChoice selectedPolicy)
        {
            MessageBox.Show(this, "Select a path policy.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var extensions = ParseExtensions(ExtensionsTextBox.Text);
        if (extensions.Count == 0)
        {
            MessageBox.Show(this, "Enter at least one image extension (e.g. .jpg, .png).", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var xnViewPath = XnViewPathTextBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(xnViewPath) && !File.Exists(xnViewPath))
        {
            MessageBox.Show(this, "XnView MP path does not exist. Clear the field or choose a valid executable.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ResultSettings = new AppSettings
        {
            LastBrowseFolder = _settings.LastBrowseFolder,
            LastSaveFolder = _settings.LastSaveFolder,
            LastPresetName = _settings.LastPresetName,
            XnViewMpPath = string.IsNullOrWhiteSpace(xnViewPath) ? null : Path.GetFullPath(xnViewPath),
            DefaultPathPolicy = selectedPolicy.Policy,
            ImageExtensions = extensions,
            DefaultIncludeSubfolders = DefaultIncludeSubfoldersCheckBox.IsChecked == true,
            AllowDuplicates = AllowDuplicatesCheckBox.IsChecked == true,
            WriteActionLogs = WriteActionLogsCheckBox.IsChecked == true,
            UseXnViewRelativePathsForUnicode = UseXnViewRelativePathsCheckBox.IsChecked == true,
            DefaultOptions = _settings.DefaultOptions
        };

        DialogResult = true;
        Close();
    }

    private static List<string> ParseExtensions(string text)
    {
        return text
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Directory.Exists(path) ? path : null;
    }
}
