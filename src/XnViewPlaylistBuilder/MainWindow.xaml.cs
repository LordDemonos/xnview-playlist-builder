using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using XnViewPlaylistBuilder.Collections;
using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Models;
using XnViewPlaylistBuilder.Core.Services;
using XnViewPlaylistBuilder.Services;

namespace XnViewPlaylistBuilder;

public partial class MainWindow : Window
{
    private readonly SettingsStore _settingsStore = new();
    private readonly OptionsPresetStore _presetStore = new();
    private AppSettings _settings;
    private PlaylistService _playlistService;
    private readonly ObservableCollection<FolderSource> _folderSources = [];
    private readonly BatchObservableCollection<MediaEntry> _entries = [];
    private readonly ICollectionView _entryView;
    private readonly CheckBox[] _effectCheckBoxes = new CheckBox[SldEffects.Count];
    private string? _currentPlaylistPath;
    private int _folderListAnchorIndex = -1;
    private bool _scanUpToDate;
    private string? _folderSignatureAtLastScan;
    private bool _fileHealthVerified;
    private bool _fileHealthHasIssues;
    private int _entriesAtFileHealthCheck;
    private bool _isRecollapsingFolders;

    public MainWindow(string[]? startupArgs = null)
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            AppLog.Error("Main window failed to initialize.", ex);
            MessageBox.Show(
                $"Could not open the main window:{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "XnView Playlist Builder",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }

        _settings = _settingsStore.Load();
        if (_settings.DefaultPathPolicy == PathPolicy.RelativeToAnchor)
        {
            _settings.DefaultPathPolicy = PathPolicy.AbsoluteLocal;
        }

        _playlistService = CreatePlaylistService();
        InitializeOptionsUi();

        UpdateToggleSubfoldersButtonText();

        InitializeSlideshowHelp();

        FolderListBox.ItemsSource = _folderSources;
        FolderListBox.SelectionChanged += FolderListBox_SelectionChanged;
        _entryView = CollectionViewSource.GetDefaultView(_entries);
        _entryView.Filter = EntryMatchesFilter;
        EntryListBox.ItemsSource = _entryView;
        LogPathTextBlock.Text = AppLog.CurrentLogFile;
        ApplyOptionsToUi(_settings.DefaultOptions);
        RefreshPresetList();
        TryLoadLastPreset();
        UpdateEntrySectionTitle();
        UpdateWindowTitle();
        UpdatePlayButtonState();

        RefreshWorkflowIndicators();

        if (startupArgs is { Length: > 0 })
        {
            LoadFoldersFromArgs(startupArgs);
        }

        AppLog.Info("Main window initialized.");
    }

    private PlaylistService CreatePlaylistService() =>
        new(new FolderScanner(_settings.ImageExtensions));

    private void InitializeOptionsUi()
    {
        TextPositionComboBox.ItemsSource = SldTextPosition.Choices;
        TextPositionComboBox.SelectedValue = SldTextPosition.Default;

        BackgroundColorEditor.SetColor(RgbaColor.Black);
        TextColorEditor.SetColor(RgbaColor.White);
        TextBackColorEditor.SetColor(RgbaColor.Gray128);

        var panel = new UniformGrid { Columns = 4 };
        for (var i = 0; i < SldEffects.Count; i++)
        {
            var effectId = i + 1;
            var checkBox = new CheckBox
            {
                Content = effectId.ToString(),
                IsChecked = true,
                Margin = new Thickness(0, 0, 8, 4),
                Tag = effectId
            };
            checkBox.Checked += EffectCheckBox_Changed;
            checkBox.Unchecked += EffectCheckBox_Changed;
            _effectCheckBoxes[i] = checkBox;
            panel.Children.Add(checkBox);
        }

        EffectsItemsControl.Items.Add(panel);

        AllEffectsCheckBox.Checked += AllEffectsCheckBox_Changed;
        AllEffectsCheckBox.Unchecked += AllEffectsCheckBox_Changed;
        AllEffectsCheckBox.IsChecked = true;
        SetEffectsPanelEnabled(false);
    }

    private void NewPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        var hasContent = _entries.Count > 0 || _folderSources.Count > 0 || !string.IsNullOrWhiteSpace(_currentPlaylistPath);
        if (hasContent)
        {
            var detailParts = new List<string>();
            if (_entries.Count > 0)
            {
                detailParts.Add($"{_entries.Count} playlist entries");
            }

            if (_folderSources.Count > 0)
            {
                detailParts.Add($"{_folderSources.Count} scan folder(s)");
            }

            if (!string.IsNullOrWhiteSpace(_currentPlaylistPath))
            {
                detailParts.Add(Path.GetFileName(_currentPlaylistPath));
            }

            var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
            {
                Title = "New playlist",
                Message = "Clear the current playlist and all scan folders?",
                Detail = detailParts.Count > 0 ? string.Join(" · ", detailParts) : null,
                ConfirmText = "New",
                CancelText = "Cancel"
            });

            if (!confirm.Confirmed)
            {
                return;
            }
        }

        ClearCurrentPlaylist();
        UpdateStatus("New playlist. Add folders, scan, and save.");
    }

    private void ClearCurrentPlaylist()
    {
        _entries.Clear();
        _folderSources.Clear();
        _currentPlaylistPath = null;
        _folderListAnchorIndex = -1;

        if (EntryFilterTextBox is not null)
        {
            EntryFilterTextBox.Text = string.Empty;
        }

        UpdateEntrySectionTitle();
        UpdateWindowTitle();
        UpdatePlayButtonState();
        UpdateToggleSubfoldersButtonText();
        UpdateFolderActionButtons();
        ResetWorkflowIndicators();
    }

    private void OpenPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        _ = OpenPlaylistAsync();
    }

    private async Task OpenPlaylistAsync()
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open XnView slideshow playlist",
                Filter = "Slideshow (*.sld)|*.sld",
                InitialDirectory = GetInitialSaveDirectory()
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            SetBusyUiEnabled(false);
            var progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();

            SldPlaylist playlist;
            try
            {
                playlist = await progressWindow.RunAsync(
                    "Opening playlist…",
                    async (progress, token) =>
                    {
                        var loaded = await Task.Run(
                            () =>
                            {
                                progress.Report(new WorkProgressReport
                                {
                                    Status = "Reading playlist file…",
                                    Detail = Path.GetFileName(dialog.FileName)
                                });
                                token.ThrowIfCancellationRequested();

                                var loadedPlaylist = _playlistService.LoadPlaylist(dialog.FileName);
                                progress.Report(new WorkProgressReport
                                {
                                    Status = "Analyzing folders…",
                                    Detail = $"{loadedPlaylist.Entries.Count:N0} entries"
                                });
                                token.ThrowIfCancellationRequested();

                                var folderSources = PlaylistFolderSourceBuilder.Build(
                                    loadedPlaylist.Entries,
                                    _settings.DefaultIncludeSubfolders);

                                return (loadedPlaylist, folderSources);
                            },
                            token);

                        await ReplaceEntriesAsync(loaded.Item1.Entries, progress, token);
                        await ApplyFolderSourcesAsync(loaded.Item2, progress, token);

                        return loaded.Item1;
                    });
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Open playlist cancelled.");
                return;
            }
            finally
            {
                progressWindow.Close();
                SetBusyUiEnabled(true);
            }

            _currentPlaylistPath = playlist.SourcePath;
            ApplyOptionsToUi(playlist.Options);
            _settings.LastSaveFolder = Path.GetDirectoryName(playlist.SourcePath);
            _settingsStore.Save(_settings);
            UpdateWindowTitle();
            UpdatePlayButtonState();
            MarkScanStale();
            MarkFileHealthStale();

            UpdateStatus(
                $"Opened {Path.GetFileName(playlist.SourcePath)} — {playlist.Entries.Count:N0} entries loaded. " +
                "Edit options, add folders and scan to append, then save.");
        }
        catch (Exception ex)
        {
            ShowError("Failed to open playlist.", ex);
        }
    }

    private void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select image folders (Ctrl+click or Shift+click for multiple)",
                InitialDirectory = GetInitialBrowseDirectory(),
                Multiselect = true
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var selected = dialog.FolderNames is { Length: > 0 }
                ? dialog.FolderNames
                : string.IsNullOrWhiteSpace(dialog.FolderName) ? [] : [dialog.FolderName];

            var (added, skipped) = AddFolders(selected);
            if (selected.Length > 0)
            {
                _settings.LastBrowseFolder = BrowseFolderHelper.GetNextBrowseDirectory(selected);
                _settingsStore.Save(_settings);
            }

            var status = added > 0
                ? $"Added {added} folder(s). {skipped} duplicate(s) skipped. {_folderSources.Count} source(s) in list."
                : skipped > 0
                    ? $"All {skipped} selected folder(s) were already in the list."
                    : "No folders selected.";

            if (added > 0)
            {
                var folderIssueCount = selected
                    .Select(Path.GetFullPath)
                    .Count(AsciiPathNormalizer.NeedsNormalization);
                if (folderIssueCount > 0)
                {
                    status +=
                        $" {folderIssueCount} added folder(s) use non-ASCII names — scan may add files that need Fix names.";
                }
            }

            UpdateStatus(status);
            if (added > 0)
            {
                MarkScanStale();
            }
        }
        catch (Exception ex)
        {
            ShowError("Failed to add folders.", ex);
        }
    }

    private void AddFilesButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select image files (Ctrl+click or Shift+click for multiple)",
                Filter = BuildImageFileFilter(),
                Multiselect = true,
                InitialDirectory = GetInitialBrowseDirectory()
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            var addedEntries = new List<MediaEntry>();
            var skipped = 0;
            var emptySkipped = 0;
            foreach (var filePath in dialog.FileNames)
            {
                if (!IsAllowedImageFile(filePath))
                {
                    skipped++;
                    continue;
                }

                var fullPath = Path.GetFullPath(filePath);
                if (MediaFileHealthChecker.IsEmptyFile(fullPath))
                {
                    emptySkipped++;
                    continue;
                }

                addedEntries.Add(new MediaEntry
                {
                    AbsolutePath = fullPath,
                    SourceRootIndex = -1
                });
            }

            if (addedEntries.Count == 0)
            {
                UpdateStatus(emptySkipped > 0
                    ? $"No files added. {emptySkipped} empty file(s) skipped."
                    : skipped > 0
                        ? "No supported image files selected."
                        : "No files selected.");
                return;
            }

            var before = _entries.Count;
            ReplaceEntries(_playlistService.MergeEntries(_entries, addedEntries, _settings.AllowDuplicates));
            var added = _entries.Count - before;
            var duplicatesSkipped = _settings.AllowDuplicates ? 0 : addedEntries.Count - added;
            var emptyNote = emptySkipped > 0 ? $", {emptySkipped} empty skipped" : string.Empty;
            UpdateStatus(
                $"Added {added} file(s). {skipped + duplicatesSkipped} duplicate(s) or unsupported file(s) skipped{emptyNote}.");
            MarkFileHealthStale();
        }
        catch (Exception ex)
        {
            ShowError("Failed to add files.", ex);
        }
    }

    private void RemoveFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (EntryListBox.SelectedItem is MediaEntry entry)
        {
            _entries.Remove(entry);
            UpdateEntrySectionTitle();
            UpdatePlayButtonState();
            UpdateStatus($"Removed entry. {_entries.Count} file(s) in playlist.");
            return;
        }

        if (FolderListBox.SelectedItems.Count > 0)
        {
            var removed = FolderListBox.SelectedItems.Cast<FolderSource>().ToList();
            foreach (var source in removed)
            {
                _folderSources.Remove(source);
            }

            _folderListAnchorIndex = -1;
            UpdateToggleSubfoldersButtonText();
            UpdateFolderActionButtons();
            MarkScanStale();
            UpdateStatus($"Removed {removed.Count} scan folder(s). {_folderSources.Count} folder(s) queued to scan.");
            return;
        }

        MessageBox.Show(this, "Select a playlist entry or scan folder to remove.", "Remove", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RecollapseFolderSources();

            if (_folderSources.Count == 0)
            {
                if (_entries.Count == 0 && !HasWildcardFolderSources())
                {
                    MessageBox.Show(this, "Add at least one folder before scanning.", "Scan", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                MessageBox.Show(this, "Add folders to append more images, or save the opened playlist as-is.", "Scan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var scannable = _folderSources.Where(source => !source.UseWildcardLine).ToList();
            if (scannable.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "All listed folders use wildcard lines (*.*) and are not scanned. Save the playlist to write wildcard folder lines.",
                    "Scan",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SetBusyUiEnabled(false);
            var progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();

            PostScanWorkResult postScan;
            try
            {
                var existingEntries = _entries.ToList();
                var allowDuplicates = _settings.AllowDuplicates;
                var imageExtensions = _settings.ImageExtensions;

                postScan = await progressWindow.RunAsync(
                    "Scanning folders…",
                    async (progress, token) =>
                    {
                        var scanProgress = new Progress<ScanProgressReport>(report =>
                        {
                            progress.Report(new WorkProgressReport
                            {
                                Status = report.TotalRoots > 0
                                    ? $"Scanning folder {report.RootIndex} of {report.TotalRoots}"
                                    : "Scanning folders…",
                                Detail = string.IsNullOrWhiteSpace(report.CurrentPath)
                                    ? $"{report.FilesFound:N0} files found"
                                    : $"{report.FilesFound:N0} files found — {report.CurrentPath}",
                                PercentComplete = report.TotalRoots > 0
                                    ? Math.Clamp(report.RootIndex * 100.0 / report.TotalRoots, 0, 100)
                                    : null
                            });
                        });

                        var scanResult = await Task.Run(
                            () => _playlistService.ScanFolders(
                                scannable,
                                scanProgress,
                                token,
                                allowDuplicates),
                            token);

                        var processed = await Task.Run(
                            () => PostScanWorkProcessor.Process(
                                scanResult,
                                existingEntries,
                                allowDuplicates,
                                imageExtensions,
                                progress,
                                token),
                            token);

                        await ReplaceEntriesAsync(processed.MergedEntries, progress, token);

                        return processed;
                    });
            }
            catch (OperationCanceledException)
            {
                UpdateStatus("Scan cancelled.");
                return;
            }
            finally
            {
                progressWindow.Close();
                SetBusyUiEnabled(true);
            }

            var result = postScan.ScanResult;
            var duplicateNote = _settings.AllowDuplicates || result.DuplicatesSkipped == 0
                ? string.Empty
                : $", {result.DuplicatesSkipped} duplicates skipped";
            var emptyNote = result.EmptyFilesSkipped == 0
                ? string.Empty
                : $", {result.EmptyFilesSkipped} empty skipped";
            UpdateStatus(
                $"Scan complete: {_entries.Count:N0} total files ({result.Entries.Count:N0} added{duplicateNote}{emptyNote}), " +
                $"{result.DirectoriesScanned} directories in {result.Duration.TotalSeconds:F2}s.");

            MaybePromptEmptyFilesSkippedAfterScan(result);
            MaybePromptFixAsciiPathsAfterScan(postScan.AsciiPathSummary);
            MaybePromptMediaHealthAfterScan(postScan.HealthReport, result.Entries);
            MarkScanComplete();
        }
        catch (Exception ex)
        {
            ShowError("Scan failed.", ex);
        }
        finally
        {
            SetBusyUiEnabled(true);
        }
    }

    private async void RemoveFolderEntriesButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedFolders = GetSelectedFolderSources();
        if (selectedFolders.Count == 0)
        {
            ConfirmDialogWindow.ShowInfo(this, "Remove entries", "Select one or more folders in the list below.");
            return;
        }

        SetBusyUiEnabled(false);
        try
        {
            var entrySnapshot = _entries.ToList();
            var folders = selectedFolders.ToArray();

            List<MediaEntry> toRemove;
            var progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();
            try
            {
                toRemove = await progressWindow.RunAsync(
                    "Finding entries…",
                    async (progress, token) =>
                        await Task.Run(
                            () =>
                            {
                                progress.Report(new WorkProgressReport { Status = "Matching entries to folders…" });
                                return EntryPathMatcher.CollectUnderFolders(
                                    entrySnapshot,
                                    folders.Select(folder => folder.AbsolutePath))
                                    .ToList();
                            },
                            token));
            }
            finally
            {
                progressWindow.Close();
            }

            if (toRemove.Count == 0)
            {
                ConfirmDialogWindow.ShowInfo(this, "Remove entries", "No playlist entries are under the selected folder(s).");
                return;
            }

            var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
            {
                Title = "Remove entries",
                Message = selectedFolders.Count == 1
                    ? $"Remove {toRemove.Count:N0} playlist entries under this folder?"
                    : $"Remove {toRemove.Count:N0} playlist entries under {selectedFolders.Count} folders?",
                Detail = FormatFolderSelectionDetail(folders),
                CheckBoxLabel = selectedFolders.Count == 1
                    ? "Also remove this folder from the scan list"
                    : "Also remove selected folders from the scan list",
                CheckBoxDefault = true
            });

            if (!confirm.Confirmed)
            {
                return;
            }

            List<MediaEntry> remaining;
            progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();
            try
            {
                remaining = await progressWindow.RunAsync(
                    "Removing entries…",
                    async (progress, token) =>
                        await Task.Run(
                            () =>
                            {
                                progress.Report(new WorkProgressReport { Status = "Filtering playlist entries…" });
                                var toRemoveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var entry in toRemove)
                                {
                                    toRemoveKeys.Add(EntryMerge.EntryKey(entry));
                                }

                                var kept = new List<MediaEntry>(entrySnapshot.Count - toRemove.Count);
                                foreach (var entry in entrySnapshot)
                                {
                                    token.ThrowIfCancellationRequested();
                                    if (!toRemoveKeys.Contains(EntryMerge.EntryKey(entry)))
                                    {
                                        kept.Add(entry);
                                    }
                                }

                                return kept;
                            },
                            token));
            }
            finally
            {
                progressWindow.Close();
            }

            progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();
            try
            {
                await progressWindow.RunAsync(
                    "Removing entries…",
                    async (progress, token) =>
                    {
                        await ReplaceEntriesAsync(remaining, progress, token);
                        return true;
                    });
            }
            finally
            {
                progressWindow.Close();
            }

            if (confirm.IsOptionChecked)
            {
                foreach (var source in folders)
                {
                    _folderSources.Remove(source);
                }

                _folderListAnchorIndex = -1;
                UpdateToggleSubfoldersButtonText();
                MarkScanStale();
            }

            UpdateEntrySectionTitle();
            UpdatePlayButtonState();
            UpdateFolderActionButtons();

            UpdateStatus(confirm.IsOptionChecked
                ? $"Removed {toRemove.Count:N0} entries and dropped {folders.Length} folder(s) from scan list. {_entries.Count:N0} entries, {_folderSources.Count} folder(s) remaining."
                : $"Removed {toRemove.Count:N0} entries under {folders.Length} folder(s). {_entries.Count:N0} remaining.");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Remove entries cancelled.");
        }
        catch (Exception ex)
        {
            ShowError("Remove entries failed.", ex);
        }
        finally
        {
            SetBusyUiEnabled(true);
        }
    }

    private void FolderListBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject origin)
        {
            return;
        }

        if (FindAncestor<CheckBox>(origin) is not null)
        {
            return;
        }

        if (FindAncestor<Button>(origin) is not null)
        {
            return;
        }

        var listBoxItem = FindAncestor<ListBoxItem>(origin);
        if (listBoxItem is null)
        {
            return;
        }

        var clickedIndex = FolderListBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        if (clickedIndex < 0)
        {
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && _folderListAnchorIndex >= 0)
        {
            SelectFolderRange(_folderListAnchorIndex, clickedIndex);
            listBoxItem.Focus();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            if (listBoxItem.IsSelected)
            {
                FolderListBox.SelectedItems.Remove(FolderListBox.Items[clickedIndex]);
            }
            else
            {
                FolderListBox.SelectedItems.Add(FolderListBox.Items[clickedIndex]);
            }

            _folderListAnchorIndex = clickedIndex;
            listBoxItem.Focus();
            e.Handled = true;
            return;
        }

        FolderListBox.SelectedItems.Clear();
        FolderListBox.SelectedItems.Add(FolderListBox.Items[clickedIndex]);
        _folderListAnchorIndex = clickedIndex;
        listBoxItem.Focus();
        e.Handled = true;
    }

    private void SelectFolderRange(int anchorIndex, int clickedIndex)
    {
        var start = Math.Min(anchorIndex, clickedIndex);
        var end = Math.Max(anchorIndex, clickedIndex);
        FolderListBox.SelectedItems.Clear();

        for (var i = start; i <= end; i++)
        {
            FolderListBox.SelectedItems.Add(FolderListBox.Items[i]);
        }
    }

    private IReadOnlyList<FolderSource> GetSelectedFolderSources() =>
        FolderListBox.SelectedItems.Cast<FolderSource>().ToArray();

    private void RemoveEntriesByAbsolutePaths(IEnumerable<string> absolutePaths)
    {
        var remaining = FilterOutEntriesByAbsolutePaths(_entries, absolutePaths);
        if (remaining.Count == _entries.Count)
        {
            return;
        }

        ReplaceEntries(remaining);
        UpdateEntrySectionTitle();
        UpdatePlayButtonState();
        MarkFileHealthStale();
    }

    private async Task ApplyPlaylistPathRemovalsAsync(
        IReadOnlyList<string> absolutePaths,
        IReadOnlyList<MediaHealthReportWindow.RemovedPathRecord> removedRecords,
        int deletedEmptyFileCount)
    {
        if (absolutePaths.Count == 0)
        {
            return;
        }

        const int asyncThreshold = 1000;
        var entrySnapshot = _entries.ToList();

        if (absolutePaths.Count >= asyncThreshold || entrySnapshot.Count >= asyncThreshold)
        {
            SetBusyUiEnabled(false);
            var progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();
            try
            {
                await progressWindow.RunAsync(
                    "Updating playlist…",
                    async (progress, token) =>
                    {
                        var remaining = await Task.Run(
                            () => FilterOutEntriesByAbsolutePaths(entrySnapshot, absolutePaths),
                            token);
                        await ReplaceEntriesAsync(remaining, progress, token);
                        return true;
                    });
            }
            finally
            {
                progressWindow.Close();
                SetBusyUiEnabled(true);
            }
        }
        else
        {
            RemoveEntriesByAbsolutePaths(absolutePaths);
        }

        var removedCount = entrySnapshot.Count - _entries.Count;
        if (removedCount <= 0)
        {
            return;
        }

        var logEnabled = _settings.WriteActionLogs;
        string? removalLogPath = null;
        IReadOnlyList<string> addedScanRoots = [];

        using (var removalLogger = new PlaylistRemovalLogger(logEnabled))
        {
            removalLogPath = removalLogger.LogFilePath;
            foreach (var group in removedRecords.GroupBy(record => record.Issue))
            {
                removalLogger.WriteSection(
                    group.Key,
                    group.Select(record => record.Path));
            }

            var suggestions = RemovedPathScanRootSuggester.Suggest(absolutePaths);
            removalLogger.WriteSuggestedRoots(suggestions);

            if (suggestions.Count > 0)
            {
                var rescanDialog = new RescanRootsWindow(suggestions, removedCount) { Owner = this };
                if (rescanDialog.ShowDialog() == true)
                {
                    addedScanRoots = rescanDialog.SelectedFolderPaths;
                    foreach (var folderPath in addedScanRoots)
                    {
                        AddFolder(folderPath, logStatus: false);
                    }

                    if (addedScanRoots.Count > 0)
                    {
                        MarkScanStale();
                        if (rescanDialog.ShouldScanAfterAdd)
                        {
                            ScanButton_Click(this, new RoutedEventArgs());
                        }
                    }
                }
            }

            removalLogger.WriteFooter(removedCount, addedScanRoots);
        }

        if (deletedEmptyFileCount > 0)
        {
            UpdateStatus(
                $"Removed {removedCount:N0} entries from the playlist (deleted {deletedEmptyFileCount:N0} empty file(s) from disk).");
        }
        else if (logEnabled && !string.IsNullOrWhiteSpace(removalLogPath))
        {
            UpdateStatus($"Removed {removedCount:N0} entries from the playlist. Removal log: {removalLogPath}");
            AppLog.Info($"Removal batch logged to {removalLogPath}");
        }
        else
        {
            UpdateStatus($"Removed {removedCount:N0} entries from the playlist.");
        }

        MarkFileHealthStale();
    }

    private static List<MediaEntry> FilterOutEntriesByAbsolutePaths(
        IEnumerable<MediaEntry> entries,
        IEnumerable<string> absolutePaths)
    {
        var removeKeys = absolutePaths
            .Select(path => EntryMerge.EntryKey(new MediaEntry { AbsolutePath = path, SourceRootIndex = 0 }))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (removeKeys.Count == 0)
        {
            return entries.ToList();
        }

        return entries
            .Where(entry => !removeKeys.Contains(EntryMerge.EntryKey(entry)))
            .ToList();
    }

    private static string FormatFolderSelectionDetail(IReadOnlyList<FolderSource> folders)
    {
        if (folders.Count == 1)
        {
            return folders[0].AbsolutePath;
        }

        const int maxLines = 5;
        var lines = folders.Take(maxLines).Select(folder => folder.AbsolutePath).ToArray();
        var detail = string.Join(Environment.NewLine, lines);
        if (folders.Count > maxLines)
        {
            detail += $"{Environment.NewLine}… and {folders.Count - maxLines} more";
        }

        return detail;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private void MoveEntryUpButton_Click(object sender, RoutedEventArgs e) =>
        MoveSelectedEntry(-1);

    private void MoveEntryDownButton_Click(object sender, RoutedEventArgs e) =>
        MoveSelectedEntry(1);

    private void SortEntriesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_entries.Count < 2)
        {
            return;
        }

        var ordered = _entries.OrderBy(entry => entry.DisplayPath, StringComparer.OrdinalIgnoreCase).ToList();
        ReplaceEntries(ordered);
        UpdateStatus($"Sorted {_entries.Count} entries A–Z.");
    }

    private void SortFoldersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_folderSources.Count < 2)
        {
            return;
        }

        var ordered = _folderSources
            .OrderBy(source => source.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _folderSources.Clear();
        foreach (var source in ordered)
        {
            _folderSources.Add(source);
        }

        UpdateStatus($"Sorted {_folderSources.Count} scan folders A–Z.");
    }

    private void CheckFilesButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunCheckFilesAsync(_entries.Select(entry => entry.AbsolutePath));

    private async Task RunCheckFilesAsync(IEnumerable<string> absolutePaths)
    {
        if (_entries.Count == 0)
        {
            MessageBox.Show(this, "Add or scan playlist entries first.", "Check files", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SetBusyUiEnabled(false);
        var progressWindow = new WorkProgressWindow { Owner = this };
        progressWindow.Show();

        try
        {
            var report = await progressWindow.RunAsync(
                "Checking files…",
                (progress, token) => Task.Run(
                    () => MediaFileHealthChecker.Analyze(
                        absolutePaths,
                        _settings.ImageExtensions,
                        progress,
                        token),
                    token));

            if (!report.HasIssues)
            {
                MessageBox.Show(
                    this,
                    $"{report.TotalChecked:N0} playlist files look readable on disk.",
                    "Check files",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                RecordFileHealthCheck(hasIssues: false);
                return;
            }

            await ShowMediaHealthReportAsync(report);
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("File check cancelled.");
        }
        finally
        {
            progressWindow.Close();
            SetBusyUiEnabled(true);
        }
    }

    private void MaybePromptEmptyFilesSkippedAfterScan(ScanResult result)
    {
        if (result.EmptyFilesSkipped == 0)
        {
            return;
        }

        var examples = MediaFileHealthChecker.FormatPathExamples(result.SkippedEmptyPaths);
        var detail =
            "0-byte image files cannot be displayed in XnView MP and were excluded from the playlist." +
            Environment.NewLine +
            Environment.NewLine +
            examples +
            Environment.NewLine +
            Environment.NewLine +
            "You can delete them from disk so future scans stay clean.";

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Empty files skipped",
            Message = $"{result.EmptyFilesSkipped:N0} empty file(s) were not added to the playlist.",
            Detail = detail,
            ConfirmText = "Delete empty files…",
            CancelText = "OK"
        });

        if (confirm.Confirmed)
        {
            _ = TryDeleteEmptyFilesAsync(result.SkippedEmptyPaths, confirm: false);
        }
    }

    private async void MaybePromptMediaHealthAfterScan(MediaFileHealthReport report, IReadOnlyList<MediaEntry> scannedEntries)
    {
        if (scannedEntries.Count == 0 || !report.HasIssues)
        {
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Unplayable files detected",
            Message =
                $"{report.UnplayableCount:N0} scanned file(s) are empty or invalid. XnView MP skips them silently during playback.",
            Detail = report.FormatDetail(),
            ConfirmText = "Review…",
            CancelText = "Later"
        });

        if (confirm.Confirmed)
        {
            await ShowMediaHealthReportAsync(report);
        }
    }

    private async Task ShowMediaHealthReportAsync(MediaFileHealthReport report)
    {
        var dialog = new MediaHealthReportWindow(report) { Owner = this };
        dialog.ShowDialog();

        if (dialog.PathsRemovedFromPlaylist.Count > 0)
        {
            await ApplyPlaylistPathRemovalsAsync(
                dialog.PathsRemovedFromPlaylist,
                dialog.RemovedPathRecords,
                dialog.DeletedEmptyFileCount);
        }

        RecordFileHealthCheck(dialog.HasRemainingIssues);
    }

    private void ShowMediaHealthReport(IEnumerable<string> absolutePaths)
    {
        _ = RunCheckFilesAsync(absolutePaths);
    }

    private void TryDeleteEmptyFiles(IEnumerable<string> paths, bool confirm = true) =>
        _ = TryDeleteEmptyFilesAsync(paths, confirm);

    private async Task TryDeleteEmptyFilesAsync(IEnumerable<string> paths, bool confirm = true)
    {
        var emptyPaths = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(path => !File.Exists(path) || MediaFileHealthChecker.IsEmptyFile(path))
            .ToList();

        if (emptyPaths.Count == 0)
        {
            return;
        }

        if (confirm)
        {
            var deleteConfirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
            {
                Title = "Delete empty files",
                Message = $"Permanently delete {emptyPaths.Count:N0} empty file(s) from disk?",
                Detail = MediaFileHealthChecker.FormatPathExamples(emptyPaths),
                ConfirmText = "Delete",
                CancelText = "Cancel"
            });

            if (!deleteConfirm.Confirmed)
            {
                return;
            }
        }

        SetBusyUiEnabled(false);
        var progressWindow = new WorkProgressWindow { Owner = this };
        progressWindow.Show();

        EmptyFileDeleteResult result;
        try
        {
            result = await progressWindow.RunAsync(
                "Deleting empty files…",
                (progress, token) => Task.Run(
                    () => MediaFileHealthChecker.DeleteEmptyFiles(emptyPaths, progress, token),
                    token));
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Delete empty files cancelled.");
            return;
        }
        finally
        {
            progressWindow.Close();
            SetBusyUiEnabled(true);
        }

        if (result.FailedPaths.Count > 0)
        {
            MessageBox.Show(
                this,
                $"Deleted {result.DeletedCount:N0} file(s). {result.FailedPaths.Count:N0} could not be deleted.",
                "Delete empty files",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        UpdateStatus($"Deleted {result.DeletedCount:N0} empty file(s) from disk.");
    }

    private void FixAsciiPathsButton_Click(object sender, RoutedEventArgs e) =>
        _ = RunFixAsciiPathsFlowAsync();

    private void MaybePromptFixAsciiPathsAfterScan(AsciiPathIssueSummary summary)
    {
        if (!summary.HasIssues)
        {
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Non-ASCII path names detected",
            Message =
                $"{summary.AffectedEntryCount:N0} scanned file(s) in {summary.AffectedDirectoryCount:N0} folder(s) " +
                "use names XnView MP may not play with absolute paths.",
            Detail = summary.FormatDetail(relativePathFallbackEnabled: _settings.UseXnViewRelativePathsForUnicode),
            ConfirmText = "Fix names",
            CancelText = "Later"
        });

        if (confirm.Confirmed)
        {
            _ = RunFixAsciiPathsFlowAsync();
        }
    }

    private async Task RunFixAsciiPathsFlowAsync()
    {
        if (_entries.Count == 0)
        {
            MessageBox.Show(this, "Add or scan playlist entries first.", "Fix names", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var candidates = _entries
            .Select(entry => entry.AbsolutePath)
            .Where(AsciiPathNormalizer.NeedsNormalization)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            MessageBox.Show(
                this,
                "No playlist entries use non-ASCII or mojibake file or folder names.",
                "Fix names",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RefreshWorkflowIndicators();
            return;
        }

        SetBusyUiEnabled(false);
        var renameService = new MediaPathRenameService();
        var progressWindow = new WorkProgressWindow { Owner = this };
        progressWindow.Show();

        PathRenamePlan plan;
        try
        {
            plan = await progressWindow.RunAsync(
                "Preparing rename plan…",
                (progress, token) => Task.Run(
                    () => renameService.BuildPlan(
                        _entries.Select(entry => entry.AbsolutePath),
                        progress,
                        token),
                    token));
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Fix names cancelled.");
            progressWindow.Close();
            SetBusyUiEnabled(true);
            return;
        }

        progressWindow.Close();

        if (plan.Operations.Count == 0)
        {
            MessageBox.Show(
                this,
                "No rename operations were generated for the current entries.",
                "Fix names",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SetBusyUiEnabled(true);
            return;
        }

        var preview = PathRenamePreview.FromPlan(plan);
        if (preview.ReadyCount == 0)
        {
            MessageBox.Show(
                this,
                $"{preview.MissingCount:N0} non-ASCII path(s) were not found on disk. " +
                "Remove them with Check files, then scan the correct subject folder to re-add files.",
                "Fix names",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            SetBusyUiEnabled(true);
            RefreshWorkflowIndicators();
            return;
        }

        var dialog = new RenameAsciiPathsWindow(preview) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            SetBusyUiEnabled(true);
            return;
        }

        var executablePlan = dialog.ExecutablePlan;
        progressWindow = new WorkProgressWindow { Owner = this };
        progressWindow.Show();

        try
        {
            var result = await progressWindow.RunAsync(
                "Renaming paths…",
                async (progress, token) =>
                {
                    var executionResult = await Task.Run(
                        () => renameService.ExecutePlan(executablePlan, progress, token),
                        token);

                    var entrySnapshot = await Dispatcher.InvokeAsync(() => _entries.ToList());
                    var folderSnapshot = await Dispatcher.InvokeAsync(() => _folderSources.ToList());

                    var updatedEntries = await Task.Run(
                        () => MediaPathRenameService.ApplyRenamedPaths(
                            entrySnapshot,
                            executionResult,
                            progress,
                            token),
                        token);

                    var updatedFolders = await Task.Run(
                        () => MediaPathRenameService.ApplyDirectoryMap(
                            folderSnapshot,
                            executionResult.CompletedOperations),
                        token);

                    progress.Report(new WorkProgressReport
                    {
                        Status = "Refreshing entry list…",
                        Detail = $"{updatedEntries.Count:N0} entries",
                        PercentComplete = null
                    });

                    await ReplaceEntriesAsync(updatedEntries, progress, token);
                    await ApplyFolderSourcesAsync(updatedFolders, progress, token);

                    return executionResult;
                });

            ShowRenameResultSummary(result);
            UpdateStatus(BuildRenameStatusMessage(result));
            MarkScanStale();
            RefreshWorkflowIndicators();
            AppLog.Info("Fix names updated existing playlist entries only; no scan was run.");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Fix names cancelled.");
        }
        finally
        {
            progressWindow.Close();
            SetBusyUiEnabled(true);
        }
    }

    private static string BuildRenameStatusMessage(PathRenameExecutionResult result)
    {
        var message = $"Renamed {result.CompletedCount:N0} path(s) to ASCII";
        if (result.ConflictResolvedCount > 0)
        {
            message += $"; {result.ConflictResolvedCount:N0} used a (1), (2), … suffix";
        }

        if (result.SkippedCount > 0)
        {
            message += $"; skipped {result.SkippedCount:N0}";
        }

        return message + ". Re-save the playlist when ready.";
    }

    private void ShowRenameResultSummary(PathRenameExecutionResult result)
    {
        if (result.SkippedCount == 0 && result.ConflictResolvedCount == 0)
        {
            return;
        }

        var detailParts = new List<string>();
        if (result.ConflictResolvedCount > 0)
        {
            var conflictExamples = result.ResolvedConflicts
                .Take(6)
                .Select(conflict => $"{conflict.PreferredTargetPath} -> {conflict.ResolvedTargetPath}")
                .ToList();
            var conflictDetail = string.Join(Environment.NewLine, conflictExamples);
            var remainingConflicts = result.ConflictResolvedCount - conflictExamples.Count;
            if (remainingConflicts > 0)
            {
                conflictDetail += $"{Environment.NewLine}… and {remainingConflicts:N0} more";
            }

            detailParts.Add(conflictDetail);
        }

        if (result.SkippedCount > 0)
        {
            var skipExamples = result.SkippedOperations
                .Take(6)
                .Select(skip => $"{skip.Reason}: {skip.SourcePath}")
                .ToList();
            var skipDetail = string.Join(Environment.NewLine, skipExamples);
            var remainingSkips = result.SkippedCount - skipExamples.Count;
            if (remainingSkips > 0)
            {
                skipDetail += $"{Environment.NewLine}… and {remainingSkips:N0} more";
            }

            detailParts.Add(skipDetail);
        }

        var title = result.SkippedCount > 0
            ? "Rename completed with warnings"
            : "Rename completed with suffixes";

        var message = result.ConflictResolvedCount > 0
            ? $"Renamed {result.CompletedCount:N0} path(s). {result.ConflictResolvedCount:N0} conflict(s) were resolved with (1), (2), … suffixes."
            : $"Renamed {result.CompletedCount:N0} path(s).";

        if (result.SkippedCount > 0)
        {
            message += $" Skipped {result.SkippedCount:N0} due to missing sources.";
        }

        MessageBox.Show(
            this,
            $"{message}{Environment.NewLine}{Environment.NewLine}{string.Join($"{Environment.NewLine}{Environment.NewLine}", detailParts)}",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void EntryFilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _entryView.Refresh();
        UpdateEntrySectionTitle();
    }

    private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not string presetName)
        {
            MessageBox.Show(this, "Select a preset to delete.", "Presets", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Delete preset",
            Message = $"Delete preset \"{presetName}\"?",
            ConfirmText = "Delete",
            CancelText = "Cancel"
        });

        if (!confirm.Confirmed)
        {
            return;
        }

        try
        {
            _presetStore.Delete(presetName);
            if (string.Equals(_settings.LastPresetName, presetName, StringComparison.OrdinalIgnoreCase))
            {
                _settings.LastPresetName = null;
            }

            _settingsStore.Save(_settings);
            RefreshPresetList();
            UpdateStatus($"Deleted preset \"{presetName}\".");
        }
        catch (Exception ex)
        {
            ShowError("Failed to delete preset.", ex);
        }
    }

    private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        UpdateFolderActionButtons();

    private void UpdateFolderActionButtons()
    {
        if (SortFoldersButton is not null)
        {
            SortFoldersButton.IsEnabled = _folderSources.Count >= 2;
        }

        if (RemoveFolderEntriesButton is not null)
        {
            RemoveFolderEntriesButton.IsEnabled = FolderListBox.SelectedItems.Count > 0;
        }
    }

    private void MoveSelectedEntry(int delta)
    {
        if (EntryListBox.SelectedItem is not MediaEntry entry)
        {
            MessageBox.Show(this, "Select a playlist entry to move.", "Reorder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var index = _entries.IndexOf(entry);
        var newIndex = index + delta;
        if (index < 0 || newIndex < 0 || newIndex >= _entries.Count)
        {
            return;
        }

        _entries.Move(index, newIndex);
        EntryListBox.SelectedItem = entry;
        EntryListBox.ScrollIntoView(entry);
    }

    private bool EntryMatchesFilter(object item)
    {
        if (item is not MediaEntry entry)
        {
            return false;
        }

        var filter = EntryFilterTextBox?.Text?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        return entry.DisplayPath.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasWildcardFolderSources() =>
        _folderSources.Any(source => source.UseWildcardLine);

    private string BuildImageFileFilter()
    {
        var patterns = _settings.ImageExtensions
            .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
            .Select(ext => $"*{ext}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (patterns.Length == 0)
        {
            return "Image files|*.*";
        }

        var joined = string.Join(';', patterns);
        return $"Image files ({joined})|{joined}|All files (*.*)|*.*";
    }

    private bool IsAllowedImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return _settings.ImageExtensions.Any(ext =>
            string.Equals(ext.StartsWith('.') ? ext : $".{ext}", extension, StringComparison.OrdinalIgnoreCase));
    }

    private void SetBusyUiEnabled(bool enabled)
    {
        ScanButton.IsEnabled = enabled;
        AddFolderButton.IsEnabled = enabled;
        AddFilesButton.IsEnabled = enabled;
        RemoveFolderButton.IsEnabled = enabled;
        OpenPlaylistButton.IsEnabled = enabled;
        CheckFilesButton.IsEnabled = enabled;
        FixAsciiPathsButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
        SaveAsButton.IsEnabled = enabled;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e) =>
        _ = SavePlaylistAsync(saveAs: false);

    private void SaveAsButton_Click(object sender, RoutedEventArgs e) =>
        _ = SavePlaylistAsync(saveAs: true);

    private async Task SavePlaylistAsync(bool saveAs)
    {
        try
        {
            if (_entries.Count == 0 && !HasWildcardFolderSources())
            {
                MessageBox.Show(this, "Open or scan a playlist before saving.", "Save", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var options = ReadOptionsFromUi();
            options.NormalizeForWrite();
            if (options.Effects.Count == 0)
            {
                MessageBox.Show(this, "Select at least one transition effect.", "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (options.ShowInfo && !SldInfoTokens.TemplateShowsFilename(options.Info))
            {
                var confirmInfo = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
                {
                    Title = "Save",
                    Message = "Show info is enabled but the Info template does not include {Filename} or {Filename With Ext}. " +
                              "XnView may show little or no overlay text.",
                    ConfirmText = "Save anyway",
                    CancelText = "Cancel"
                });

                if (!confirmInfo.Confirmed)
                {
                    return;
                }
            }

            _settings.DefaultOptions = options;

            string outputPath;
            if (!saveAs && !string.IsNullOrWhiteSpace(_currentPlaylistPath))
            {
                outputPath = _currentPlaylistPath;
            }
            else
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Save XnView slideshow playlist",
                    Filter = "Slideshow (*.sld)|*.sld",
                    DefaultExt = ".sld",
                    FileName = string.IsNullOrWhiteSpace(_currentPlaylistPath)
                        ? "playlist.sld"
                        : Path.GetFileName(_currentPlaylistPath),
                    InitialDirectory = GetInitialSaveDirectory()
                };

                if (dialog.ShowDialog() != true)
                {
                    return;
                }

                outputPath = dialog.FileName;
            }

            if (!TryValidatePathPolicy(outputPath, out var policyError))
            {
                MessageBox.Show(this, policyError, "Save", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var folderSources = _folderSources.ToList();
            var entryList = _entries.ToList();
            var pathPolicy = _settings.DefaultPathPolicy;
            var useUnicodeRelative = _settings.UseXnViewRelativePathsForUnicode;

            SetBusyUiEnabled(false);
            SavePlaylistPrep prep;
            var progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();
            try
            {
                prep = await progressWindow.RunAsync(
                    "Preparing save…",
                    async (progress, token) =>
                        await Task.Run(
                            () =>
                            {
                                progress.Report(new WorkProgressReport { Status = "Building playlist paths…" });
                                var savePaths = _playlistService.BuildSavePaths(
                                    entryList,
                                    folderSources,
                                    pathPolicy,
                                    outputPath,
                                    anchorPath: null,
                                    useUnicodeRelative);

                                progress.Report(new WorkProgressReport { Status = "Building save summary…" });
                                var summary = SaveSummaryBuilder.Build(
                                    outputPath,
                                    entryList,
                                    pathPolicy,
                                    options,
                                    anchorPath: null,
                                    totalPathCount: savePaths.Count,
                                    serializedPaths: savePaths,
                                    useXnViewRelativePathsForUnicode: useUnicodeRelative,
                                    progress: progress,
                                    cancellationToken: token);

                                return new SavePlaylistPrep(outputPath, options, savePaths, summary);
                            },
                            token));
            }
            finally
            {
                progressWindow.Close();
            }

            var confirm = new SaveSummaryWindow(prep.Summary) { Owner = this };
            if (confirm.ShowDialog() != true)
            {
                return;
            }

            _settings.LastSaveFolder = Path.GetDirectoryName(prep.OutputPath);
            _settingsStore.Save(_settings);

            progressWindow = new WorkProgressWindow { Owner = this };
            progressWindow.Show();
            try
            {
                await progressWindow.RunAsync(
                    "Saving playlist…",
                    async (progress, token) =>
                    {
                        await Task.Run(
                            () =>
                            {
                                progress.Report(new WorkProgressReport { Status = "Writing .sld file…" });
                                _playlistService.SavePlaylist(prep.OutputPath, prep.Options, prep.SavePaths);
                            },
                            token);
                        return true;
                    });
            }
            finally
            {
                progressWindow.Close();
            }

            _currentPlaylistPath = prep.OutputPath;
            UpdateWindowTitle();
            UpdatePlayButtonState();
            UpdateStatus($"Saved {prep.OutputPath} with {prep.SavePaths.Count:N0} path(s).");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Save cancelled.");
        }
        catch (Exception ex)
        {
            ShowError("Save failed.", ex);
        }
        finally
        {
            SetBusyUiEnabled(true);
        }
    }

    private sealed record SavePlaylistPrep(
        string OutputPath,
        SldOptionsV2 Options,
        IReadOnlyList<string> SavePaths,
        SaveSummary Summary);

    private void LoadPresetButton_Click(object sender, RoutedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not string presetName)
        {
            MessageBox.Show(this, "Select a preset to load.", "Presets", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentPlaylistPath))
        {
            var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
            {
                Title = "Load preset",
                Message = "Loading a preset replaces all slideshow options currently shown in the panel " +
                          "(Show info, Info template, text position, colors, etc.).",
                Detail = "Playlist entries and folders are not changed.",
                ConfirmText = "Load",
                CancelText = "Cancel"
            });

            if (!confirm.Confirmed)
            {
                return;
            }
        }

        try
        {
            var options = _presetStore.Load(presetName);
            ApplyOptionsToUi(options);
            _settings.DefaultOptions = options;
            _settings.LastPresetName = presetName;
            _settingsStore.Save(_settings);
            UpdateStatus($"Loaded preset \"{presetName}\".");
        }
        catch (Exception ex)
        {
            ShowError("Failed to load preset.", ex);
        }
    }

    private void SavePresetButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new PresetNameWindow(PresetComboBox.SelectedItem as string) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var options = ReadOptionsFromUi();
            options.NormalizeForWrite();
            if (options.Effects.Count == 0)
            {
                MessageBox.Show(this, "Select at least one transition effect before saving a preset.", "Presets", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _presetStore.Save(dialog.PresetName, options);
            _settings.LastPresetName = dialog.PresetName;
            _settingsStore.Save(_settings);
            RefreshPresetList(selectName: dialog.PresetName);
            UpdateStatus($"Saved preset \"{dialog.PresetName}\".");
        }
        catch (Exception ex)
        {
            ShowError("Failed to save preset.", ex);
        }
    }

    private void RefreshPresetList(string? selectName = null)
    {
        var names = _presetStore.ListPresetNames();
        PresetComboBox.ItemsSource = names;
        var target = selectName ?? _settings.LastPresetName;
        PresetComboBox.SelectedItem = names.FirstOrDefault(name => string.Equals(name, target, StringComparison.OrdinalIgnoreCase));
        LoadPresetButton.IsEnabled = names.Count > 0;
    }

    private void TryLoadLastPreset()
    {
        if (string.IsNullOrWhiteSpace(_settings.LastPresetName))
        {
            return;
        }

        if (!_presetStore.ListPresetNames().Any(name =>
                string.Equals(name, _settings.LastPresetName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        try
        {
            ApplyOptionsToUi(_presetStore.Load(_settings.LastPresetName));
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Could not restore last preset \"{_settings.LastPresetName}\": {ex.Message}");
        }
    }

    private bool TryValidatePathPolicy(string outputPath, out string error)
    {
        if (_settings.DefaultPathPolicy == PathPolicy.RelativeToSld &&
            string.IsNullOrWhiteSpace(Path.GetDirectoryName(outputPath)))
        {
            error = "Relative-to-.sld policy requires saving to a folder path.";
            return false;
        }

        if (_settings.DefaultPathPolicy == PathPolicy.RelativeToAnchor)
        {
            error = "Relative-to-anchor is no longer supported. Choose another path policy in Settings.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_settings) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _settings = dialog.ResultSettings;
        _settingsStore.Save(_settings);
        _playlistService = CreatePlaylistService();
        UpdateStatus($"Settings saved. Path policy: {PathPolicyLabels.GetLabel(_settings.DefaultPathPolicy)}.");
        UpdatePlayButtonState();
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_currentPlaylistPath) || !File.Exists(_currentPlaylistPath))
            {
                MessageBox.Show(this, "Save the playlist before playing it in XnView.", "Play", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var xnViewPath = ResolveXnViewPath();
            if (xnViewPath is null)
            {
                MessageBox.Show(this, "Set the XnView MP path in Settings (or use Auto-detect).", "Play", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = xnViewPath,
                Arguments = $"-slide \"{_currentPlaylistPath}\"",
                UseShellExecute = false
            });

            UpdateStatus($"Launched slideshow: {Path.GetFileName(_currentPlaylistPath)}");
        }
        catch (Exception ex)
        {
            ShowError("Could not launch XnView MP.", ex);
        }
    }

    private string? ResolveXnViewPath()
    {
        if (!string.IsNullOrWhiteSpace(_settings.XnViewMpPath) && File.Exists(_settings.XnViewMpPath))
        {
            return _settings.XnViewMpPath;
        }

        return XnViewLocator.DetectInstallPath();
    }

    private void OpenLogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = AppLog.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError("Could not open log folder.", ex);
        }
    }

    private async void UndoRenamesButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new RenameUndoWindow { Owner = this };
        if (dialog.ShowDialog() != true || dialog.UndoResult is not { CompletedCount: > 0 } result)
        {
            return;
        }

        if (_entries.Count == 0 && _folderSources.Count == 0)
        {
            UpdateStatus($"Undid {result.CompletedCount:N0} rename(s) on disk.");
            return;
        }

        SetBusyUiEnabled(false);
        var progressWindow = new WorkProgressWindow { Owner = this };
        progressWindow.Show();

        try
        {
            await progressWindow.RunAsync(
                "Updating playlist paths…",
                async (progress, token) =>
                {
                    var entrySnapshot = await Dispatcher.InvokeAsync(() => _entries.ToList());
                    var folderSnapshot = await Dispatcher.InvokeAsync(() => _folderSources.ToList());

                    var updatedEntries = await Task.Run(
                        () => MediaPathRenameService.ApplyRenamedPaths(
                            entrySnapshot,
                            result,
                            progress,
                            token),
                        token);

                    var updatedFolders = await Task.Run(
                        () => MediaPathRenameService.ApplyDirectoryMap(
                            folderSnapshot,
                            result.CompletedOperations),
                        token);

                    await ReplaceEntriesAsync(updatedEntries, progress, token);
                    await ApplyFolderSourcesAsync(updatedFolders, progress, token);
                    return true;
                });

            MarkScanStale();
            RefreshWorkflowIndicators();
            UpdateStatus($"Undid {result.CompletedCount:N0} rename(s) on disk and updated playlist paths.");
        }
        catch (OperationCanceledException)
        {
            UpdateStatus("Undo renames cancelled while updating playlist.");
        }
        finally
        {
            progressWindow.Close();
            SetBusyUiEnabled(true);
        }
    }

    private void InitializeSlideshowHelp()
    {
        var infoTemplateHelp = new TextBlock
        {
            Text = SldInfoTokens.HelpText,
            MaxWidth = 420,
            TextWrapping = TextWrapping.Wrap
        };
        InfoTextBox.ToolTip = infoTemplateHelp;
        InfoTemplateLabelTextBlock.ToolTip = infoTemplateHelp;
        FontTextBox.ToolTip =
            "XnView font string (Qt format). Click Choose… to pick a font with the Windows font dialog.";
        TimerTextBox.ToolTip = "Seconds each image is shown when Use timer is enabled.";
        EffectDurationTextBox.ToolTip = "Transition effect duration in milliseconds.";
        OpacityTextBox.ToolTip = "Overlay text opacity (0–100).";
        FullScreenCheckBox.ToolTip = "When enabled, XnView MP runs fullscreen and ignores Win width / Win height.";
        WinWidthTextBox.ToolTip = "Used only when Full screen is off.";
        WinHeightTextBox.ToolTip = "Used only when Full screen is off.";
        ShowInfoCheckBox.ToolTip = "Must be enabled for the Info template (filename, folder name, etc.) to appear during the slideshow.";
    }

    private void ChooseFontButton_Click(object sender, RoutedEventArgs e)
    {
        if (!Win32DialogService.TryPickFont(this, FontTextBox.Text, out var fontValue))
        {
            return;
        }

        FontTextBox.Text = fontValue;
    }

    private void InsertInfoTokenButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        var isFirstGroup = true;
        foreach (var group in SldInfoTokens.InsertMenuGroups)
        {
            if (!isFirstGroup)
            {
                menu.Items.Add(new Separator());
            }

            isFirstGroup = false;
            menu.Items.Add(new MenuItem { Header = group.Label, IsEnabled = false });

            foreach (var (token, description) in group.Tokens)
            {
                var item = new MenuItem { Header = $"{token}  —  {description}" };
                var captured = token;
                item.Click += (_, _) => InsertIntoInfoTemplate(captured);
                menu.Items.Add(item);
            }
        }

        menu.PlacementTarget = InsertInfoTokenButton;
        menu.IsOpen = true;
    }

    private void InsertIntoInfoTemplate(string token)
    {
        var text = InfoTextBox.Text ?? string.Empty;
        if (text.Length > 0 && !text.EndsWith(' ') && !text.EndsWith('-'))
        {
            text += ' ';
        }

        InfoTextBox.Text = text + token;
        InfoTextBox.Focus();
        InfoTextBox.CaretIndex = InfoTextBox.Text.Length;
    }

    private void UseTextBackColorCheckBox_Changed(object sender, RoutedEventArgs e) =>
        TextBackColorEditor.IsEnabled = UseTextBackColorCheckBox.IsChecked == true;

    private void AllEffectsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_effectCheckBoxes[0] is null)
        {
            return;
        }

        if (AllEffectsCheckBox.IsChecked == true)
        {
            SetAllEffectsChecked(true);
        }

        SetEffectsPanelEnabled(AllEffectsCheckBox.IsChecked != true);
        SyncAllEffectsCheckBox();
    }

    private void EffectCheckBox_Changed(object sender, RoutedEventArgs e) =>
        SyncAllEffectsCheckBox();

    private void SetEffectsPanelEnabled(bool enabled)
    {
        foreach (var checkBox in _effectCheckBoxes)
        {
            checkBox.IsEnabled = enabled;
        }
    }

    private void SetAllEffectsChecked(bool isChecked)
    {
        foreach (var checkBox in _effectCheckBoxes)
        {
            if (checkBox is null)
            {
                continue;
            }

            checkBox.IsChecked = isChecked;
        }
    }

    private void SyncAllEffectsCheckBox()
    {
        if (_effectCheckBoxes.Any(box => box.IsChecked != true))
        {
            AllEffectsCheckBox.IsChecked = false;
            SetEffectsPanelEnabled(true);
            return;
        }

        AllEffectsCheckBox.IsChecked = true;
        SetEffectsPanelEnabled(false);
    }

    private (int Added, int Skipped) AddFolders(IEnumerable<string> paths)
    {
        var added = 0;
        var skipped = 0;

        foreach (var path in paths)
        {
            if (AddFolder(path, logStatus: false))
            {
                added++;
            }
            else
            {
                skipped++;
            }
        }

        if (added > 0)
        {
            AppLog.Info($"Added {added} folder source(s), skipped {skipped} duplicate(s).");
            RecollapseFolderSources();
        }

        return (added, skipped);
    }

    private bool AddFolder(string path, bool logStatus = true)
    {
        var fullPath = Path.GetFullPath(path);
        if (!_settings.AllowDuplicates &&
            _folderSources.Any(source => string.Equals(source.AbsolutePath, fullPath, StringComparison.OrdinalIgnoreCase)))
        {
            if (logStatus)
            {
                UpdateStatus($"Folder already in list: {fullPath}");
            }

            return false;
        }

        _folderSources.Add(new FolderSource
        {
            AbsolutePath = fullPath,
            IncludeSubfolders = _settings.DefaultIncludeSubfolders
        });
        WireFolderSource(_folderSources[^1]);
        UpdateToggleSubfoldersButtonText();

        if (logStatus)
        {
            UpdateStatus($"Added folder. {_folderSources.Count} source(s) in list.");
        }

        AppLog.Info($"Added folder source: {fullPath}");
        return true;
    }

    private void LoadFoldersFromArgs(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] != "--add")
            {
                continue;
            }

            for (i++; i < args.Length && !args[i].StartsWith("--", StringComparison.Ordinal); i++)
            {
                if (Directory.Exists(args[i]))
                {
                    AddFolder(Path.GetFullPath(args[i]));
                }
                else
                {
                    AppLog.Warning($"Startup folder not found: {args[i]}");
                }
            }

            i--;
        }
    }

    private string GetInitialBrowseDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastBrowseFolder) && Directory.Exists(_settings.LastBrowseFolder))
        {
            return _settings.LastBrowseFolder;
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    }

    private string GetInitialSaveDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.LastSaveFolder) && Directory.Exists(_settings.LastSaveFolder))
        {
            return _settings.LastSaveFolder;
        }

        return GetInitialBrowseDirectory();
    }

    private SldOptionsV2 ReadOptionsFromUi()
    {
        return new SldOptionsV2
        {
            UseTimer = UseTimerCheckBox.IsChecked == true,
            Timer = ParseInt(TimerTextBox.Text, 15),
            Loop = LoopCheckBox.IsChecked == true,
            FullScreen = FullScreenCheckBox.IsChecked == true,
            WinWidth = ParseInt(WinWidthTextBox.Text, 640),
            WinHeight = ParseInt(WinHeightTextBox.Text, 480),
            Stretch = StretchCheckBox.IsChecked == true,
            RandomOrder = RandomOrderCheckBox.IsChecked == true,
            ShowInfo = ShowInfoCheckBox.IsChecked == true,
            Info = SldInfoTokens.NormalizeTemplate(InfoTextBox.Text),
            TitleBar = TitleBarCheckBox.IsChecked == true,
            OnTop = OnTopCheckBox.IsChecked == true,
            CursorAutoHide = CursorAutoHideCheckBox.IsChecked == true,
            BackgroundColor = BackgroundColorEditor.Color,
            TextColor = TextColorEditor.Color,
            UseTextBackColor = UseTextBackColorCheckBox.IsChecked == true,
            TextPosition = ReadTextPositionFromUi(),
            TextBackColor = TextBackColorEditor.Color,
            Font = FontTextBox.Text.Trim(),
            EffectDuration = ParseInt(EffectDurationTextBox.Text, 1000),
            Opacity = ParseInt(OpacityTextBox.Text, 100),
            Effects = ReadEffectsFromUi()
        };
    }

    private IReadOnlyList<int> ReadEffectsFromUi()
    {
        if (AllEffectsCheckBox.IsChecked == true)
        {
            return SldEffects.All;
        }

        return _effectCheckBoxes
            .Where(box => box.IsChecked == true)
            .Select(box => (int)box.Tag!)
            .OrderBy(id => id)
            .ToArray();
    }

    private void ApplyOptionsToUi(SldOptionsV2 options)
    {
        UseTimerCheckBox.IsChecked = options.UseTimer;
        TimerTextBox.Text = options.Timer.ToString();
        LoopCheckBox.IsChecked = options.Loop;
        FullScreenCheckBox.IsChecked = options.FullScreen;
        WinWidthTextBox.Text = options.WinWidth.ToString();
        WinHeightTextBox.Text = options.WinHeight.ToString();
        StretchCheckBox.IsChecked = options.Stretch;
        RandomOrderCheckBox.IsChecked = options.RandomOrder;
        ShowInfoCheckBox.IsChecked = options.ShowInfo;
        InfoTextBox.Text = options.Info;
        TitleBarCheckBox.IsChecked = options.TitleBar;
        OnTopCheckBox.IsChecked = options.OnTop;
        CursorAutoHideCheckBox.IsChecked = options.CursorAutoHide;
        BackgroundColorEditor.SetColor(options.BackgroundColor);
        TextColorEditor.SetColor(options.TextColor);
        UseTextBackColorCheckBox.IsChecked = options.UseTextBackColor;
        TextBackColorEditor.SetColor(options.TextBackColor);
        TextBackColorEditor.IsEnabled = options.UseTextBackColor;
        ApplyTextPositionToUi(options.TextPosition);
        FontTextBox.Text = options.Font;
        EffectDurationTextBox.Text = options.EffectDuration.ToString();
        OpacityTextBox.Text = options.Opacity.ToString();
        ApplyEffectsToUi(options.Effects);
    }

    private void ApplyEffectsToUi(IReadOnlyList<int> effects)
    {
        var useAll = SldEffects.IsAllEffects(effects);
        AllEffectsCheckBox.IsChecked = useAll;
        SetEffectsPanelEnabled(!useAll);

        var selected = new HashSet<int>(effects);
        foreach (var checkBox in _effectCheckBoxes)
        {
            checkBox.IsChecked = selected.Contains((int)checkBox.Tag!);
        }
    }

    private int ReadTextPositionFromUi() =>
        SldTextPosition.Normalize(
            TextPositionComboBox.SelectedItem ?? TextPositionComboBox.SelectedValue,
            SldTextPosition.Default);

    private void ApplyTextPositionToUi(int textPosition)
    {
        var normalized = SldTextPosition.Normalize(textPosition);
        TextPositionComboBox.SelectedValue = normalized;
        if (TextPositionComboBox.SelectedItem is null)
        {
            TextPositionComboBox.SelectedItem = SldTextPosition.Choices.First(choice => choice.Value == normalized);
        }
    }

    private void ReplaceEntries(IReadOnlyList<MediaEntry> entries)
    {
        _entries.Clear();
        foreach (var entry in entries)
        {
            _entries.Add(entry);
        }

        UpdateEntrySectionTitle();
        UpdatePlayButtonState();
        MarkFileHealthStale();
    }

    private async Task ReplaceEntriesAsync(
        IReadOnlyList<MediaEntry> entries,
        IProgress<WorkProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        if (!Dispatcher.CheckAccess())
        {
            await Dispatcher.InvokeAsync(async () =>
                await ReplaceEntriesAsync(entries, progress, cancellationToken));
            return;
        }

        const int batchSize = 250;
        const int yieldThreshold = 1000;
        const int suspendBindingThreshold = 1000;
        const int bulkReplaceThreshold = 1000;
        var suspendBinding = entries.Count >= suspendBindingThreshold;

        if (suspendBinding)
        {
            EntryListBox.ItemsSource = null;
        }

        if (entries.Count >= bulkReplaceThreshold)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(new WorkProgressReport
            {
                Status = "Refreshing entry list…",
                Detail = $"{entries.Count:N0} entries"
            });

            _entries.ReplaceAll(entries);

            if (suspendBinding)
            {
                EntryListBox.ItemsSource = _entryView;
            }

            UpdateEntrySectionTitle();
            UpdatePlayButtonState();
            MarkFileHealthStale();
            return;
        }

        _entries.Clear();

        if (entries.Count == 0)
        {
            if (suspendBinding)
            {
                EntryListBox.ItemsSource = _entryView;
            }

            UpdateEntrySectionTitle();
            UpdatePlayButtonState();
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _entries.Add(entries[index]);

            if (entries.Count >= yieldThreshold && (index + 1) % batchSize == 0)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Refreshing entry list…",
                    Detail = $"{index + 1:N0} of {entries.Count:N0}",
                    PercentComplete = (index + 1) * 100.0 / entries.Count
                });

                if (suspendBinding)
                {
                    await Dispatcher.InvokeAsync(static () => { }, System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        if (suspendBinding)
        {
            EntryListBox.ItemsSource = _entryView;
        }

        UpdateEntrySectionTitle();
        UpdatePlayButtonState();
        MarkFileHealthStale();
    }

    private async Task ApplyFolderSourcesAsync(
        IReadOnlyList<FolderSource> folderSources,
        IProgress<WorkProgressReport>? progress,
        CancellationToken cancellationToken)
    {
        var expandedVisibility = _folderSources.ToDictionary(
            source => source.AbsolutePath,
            source => source.ShowCollapsedSubfolders,
            StringComparer.OrdinalIgnoreCase);

        var collapseResult = FolderSourceCollapser.Collapse(folderSources);
        if (collapseResult.CollapsedCount > 0)
        {
            AppLog.Info(
                $"Collapsed {collapseResult.CollapsedCount:N0} nested folder(s) into {collapseResult.Roots.Count:N0} scan root(s).");
        }

        _folderSources.Clear();

        if (collapseResult.Roots.Count == 0)
        {
            UpdateToggleSubfoldersButtonText();
            UpdateFolderActionButtons();
            return;
        }

        const int batchSize = 100;
        for (var index = 0; index < collapseResult.Roots.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var source = collapseResult.Roots[index];
            if (expandedVisibility.TryGetValue(source.AbsolutePath, out var showCollapsed))
            {
                source.ShowCollapsedSubfolders = showCollapsed;
            }

            _folderSources.Add(source);
            WireFolderSource(source);

            if (collapseResult.Roots.Count >= batchSize && (index + 1) % batchSize == 0)
            {
                progress?.Report(new WorkProgressReport
                {
                    Status = "Updating folder list…",
                    Detail = $"{index + 1:N0} of {collapseResult.Roots.Count:N0}",
                    PercentComplete = (index + 1) * 100.0 / collapseResult.Roots.Count
                });
                await Dispatcher.InvokeAsync(static () => { }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        UpdateToggleSubfoldersButtonText();
        UpdateFolderActionButtons();
        MarkScanStale();
    }

    private void RecollapseFolderSources()
    {
        if (_folderSources.Count == 0 || _isRecollapsingFolders)
        {
            return;
        }

        _isRecollapsingFolders = true;
        try
        {
            var expandedVisibility = _folderSources.ToDictionary(
                source => source.AbsolutePath,
                source => source.ShowCollapsedSubfolders,
                StringComparer.OrdinalIgnoreCase);

            var collapseResult = FolderSourceCollapser.Collapse(_folderSources);
            if (collapseResult.CollapsedCount == 0 &&
                collapseResult.Roots.Count == _folderSources.Count)
            {
                return;
            }

            _folderSources.Clear();
            foreach (var source in collapseResult.Roots)
            {
                if (expandedVisibility.TryGetValue(source.AbsolutePath, out var showCollapsed))
                {
                    source.ShowCollapsedSubfolders = showCollapsed;
                }

                _folderSources.Add(source);
                WireFolderSource(source);
            }

            if (collapseResult.CollapsedCount > 0)
            {
                AppLog.Info(
                    $"Collapsed {collapseResult.CollapsedCount:N0} nested folder(s) into {collapseResult.Roots.Count:N0} scan root(s).");
            }

            UpdateToggleSubfoldersButtonText();
            UpdateFolderActionButtons();
            MarkScanStale();
        }
        finally
        {
            _isRecollapsingFolders = false;
        }
    }

    private void CollapsedSubfoldersButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not FolderSource source)
        {
            return;
        }

        source.ShowCollapsedSubfolders = !source.ShowCollapsedSubfolders;
        e.Handled = true;
    }

    private void ToggleAllSubfoldersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_folderSources.Count == 0)
        {
            return;
        }

        var enableAll = _folderSources.Any(source => !source.IncludeSubfolders);
        _isRecollapsingFolders = true;
        try
        {
            foreach (var source in _folderSources.ToList())
            {
                source.IncludeSubfolders = enableAll;
            }
        }
        finally
        {
            _isRecollapsingFolders = false;
        }

        UpdateToggleSubfoldersButtonText();
        UpdateStatus(enableAll
            ? $"Include subfolders enabled for all {_folderSources.Count} folder(s)."
            : $"Include subfolders disabled for all {_folderSources.Count} folder(s).");
        RecollapseFolderSources();
    }

    private void WireFolderSource(FolderSource source)
    {
        source.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(FolderSource.IncludeSubfolders) or nameof(FolderSource.UseWildcardLine))
            {
                MarkScanStale();
            }

            if (args.PropertyName == nameof(FolderSource.IncludeSubfolders))
            {
                UpdateToggleSubfoldersButtonText();
                if (!_isRecollapsingFolders)
                {
                    RecollapseFolderSources();
                }
            }
        };
    }

    private void UpdateToggleSubfoldersButtonText()
    {
        if (ToggleAllSubfoldersButton is null)
        {
            return;
        }

        if (_folderSources.Count == 0)
        {
            ToggleAllSubfoldersButton.IsEnabled = false;
            ToggleAllSubfoldersButton.Content = "Check all";
            return;
        }

        ToggleAllSubfoldersButton.IsEnabled = true;
        ToggleAllSubfoldersButton.Content = _folderSources.All(source => source.IncludeSubfolders)
            ? "Uncheck all"
            : "Check all";
    }

    private void UpdateEntrySectionTitle()
    {
        var total = _entries.Count;
        if (total == 0)
        {
            EntrySectionTitleTextBlock.Text = "Playlist entries";
            return;
        }

        var filter = EntryFilterTextBox?.Text?.Trim();
        if (string.IsNullOrEmpty(filter))
        {
            EntrySectionTitleTextBlock.Text = $"Playlist entries ({total})";
            return;
        }

        var visible = _entryView.Cast<object>().Count();
        EntrySectionTitleTextBlock.Text = $"Playlist entries ({visible} of {total})";
    }

    private void UpdateWindowTitle()
    {
        if (string.IsNullOrWhiteSpace(_currentPlaylistPath))
        {
            Title = "XnView Playlist Builder";
            CurrentPlaylistSeparatorTextBlock.Visibility = Visibility.Collapsed;
            CurrentPlaylistFileNameTextBlock.Visibility = Visibility.Collapsed;
            CurrentPlaylistFileNameTextBlock.ToolTip = null;
            return;
        }

        var fileName = Path.GetFileName(_currentPlaylistPath);
        Title = $"XnView Playlist Builder — {fileName}";
        CurrentPlaylistFileNameTextBlock.Text = fileName;
        CurrentPlaylistFileNameTextBlock.ToolTip = _currentPlaylistPath;
        CurrentPlaylistSeparatorTextBlock.Visibility = Visibility.Visible;
        CurrentPlaylistFileNameTextBlock.Visibility = Visibility.Visible;
    }

    private void UpdatePlayButtonState()
    {
        PlayButton.IsEnabled =
            !string.IsNullOrWhiteSpace(_currentPlaylistPath) &&
            File.Exists(_currentPlaylistPath) &&
            ResolveXnViewPath() is not null;
    }

    private static int ParseInt(string? text, int fallback) =>
        int.TryParse(text, out var value) ? value : fallback;

    private void UpdateStatus(string message)
    {
        StatusTextBlock.Text = message;
        AppLog.Info(message);
    }

    private void ShowError(string message, Exception ex)
    {
        AppLog.Error(message, ex);
        MessageBox.Show(this, $"{message}\n\n{ex.Message}\n\nLog: {AppLog.CurrentLogFile}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private void ResetWorkflowIndicators()
    {
        _scanUpToDate = false;
        _folderSignatureAtLastScan = null;
        _fileHealthVerified = false;
        _fileHealthHasIssues = false;
        _entriesAtFileHealthCheck = 0;
        RefreshWorkflowIndicators();
    }

    private void MarkScanStale()
    {
        _scanUpToDate = false;
        RefreshWorkflowIndicators();
    }

    private void MarkScanComplete()
    {
        _folderSignatureAtLastScan = ComputeFolderSignature();
        _scanUpToDate = _folderSources.Any(source => !source.UseWildcardLine);
        MarkFileHealthStale();
        RefreshWorkflowIndicators();
    }

    private void MarkFileHealthStale()
    {
        _fileHealthVerified = false;
        _fileHealthHasIssues = false;
        RefreshWorkflowIndicators();
    }

    private void RecordFileHealthCheck(bool hasIssues)
    {
        _fileHealthVerified = true;
        _fileHealthHasIssues = hasIssues;
        _entriesAtFileHealthCheck = _entries.Count;
        RefreshWorkflowIndicators();
    }

    private static string ComputeFolderSignature(IEnumerable<FolderSource> folderSources) =>
        string.Join('\n', folderSources
            .OrderBy(source => source.AbsolutePath, StringComparer.OrdinalIgnoreCase)
            .Select(source => $"{source.AbsolutePath}|{source.IncludeSubfolders}|{source.UseWildcardLine}"));

    private string ComputeFolderSignature() => ComputeFolderSignature(_folderSources);

    private bool EntriesHaveAsciiPathIssues() =>
        _entries.Any(entry => AsciiPathNormalizer.NeedsNormalization(entry.AbsolutePath));

    private void RefreshWorkflowIndicators()
    {
        if (ScanButton is null || FixAsciiPathsButton is null || CheckFilesButton is null)
        {
            return;
        }

        var scannableFolderCount = _folderSources.Count(source => !source.UseWildcardLine);
        var folderSignature = ComputeFolderSignature();
        var scanMatchesLastRun = _scanUpToDate &&
                                 scannableFolderCount > 0 &&
                                 string.Equals(folderSignature, _folderSignatureAtLastScan, StringComparison.Ordinal);
        var asciiNeedsFix = _entries.Count > 0 && EntriesHaveAsciiPathIssues();
        var fileHealthCurrent = _fileHealthVerified &&
                                !_fileHealthHasIssues &&
                                _entries.Count == _entriesAtFileHealthCheck;

        ApplyWorkflowIndicator(
            ScanButton,
            "Button.Accent",
            scannableFolderCount == 0
                ? WorkflowIndicatorState.Neutral
                : scanMatchesLastRun
                    ? WorkflowIndicatorState.Ok
                    : WorkflowIndicatorState.Attention,
            FormatWorkflowToolTip(
                step: 1,
                action: "Scan folders",
                workflow:
                    "Read files from the folder list below and merge them into the playlist. " +
                    "After Fix names, scan again so paths match disk.",
                status: scannableFolderCount == 0
                    ? "Add folders to scan first."
                    : scanMatchesLastRun
                        ? "Up to date — folders match the last scan."
                        : "Rescan needed — folders changed, or Fix names ran since the last scan."));

        ApplyWorkflowIndicator(
            CheckFilesButton,
            "Button.Secondary",
            _entries.Count == 0
                ? WorkflowIndicatorState.Neutral
                : fileHealthCurrent
                    ? WorkflowIndicatorState.Ok
                    : WorkflowIndicatorState.Attention,
            FormatWorkflowToolTip(
                step: 2,
                action: "Check files",
                workflow:
                    "Verify playlist files exist and are readable. Remove missing entries before Fix names. " +
                    "Export the report if you may need to re-add folders later.",
                status: _entries.Count == 0
                    ? "Scan or open a playlist first."
                    : fileHealthCurrent
                        ? "All checked files look readable on disk."
                        : _fileHealthHasIssues
                            ? "Last check found problems — review the report or recheck after changes."
                            : "Not checked yet, or the playlist changed since the last check."));

        ApplyWorkflowIndicator(
            FixAsciiPathsButton,
            "Button.Secondary",
            _entries.Count == 0
                ? WorkflowIndicatorState.Neutral
                : asciiNeedsFix
                    ? WorkflowIndicatorState.Attention
                    : WorkflowIndicatorState.Ok,
            FormatWorkflowToolTip(
                step: 3,
                action: "Fix names",
                workflow:
                    "Rename non-ASCII files and folders on disk so absolute paths work in XnView MP. " +
                    "Only run when Check files shows paths exist; then scan again (step 1).",
                status: _entries.Count == 0
                    ? "Scan or open a playlist first."
                    : asciiNeedsFix
                        ? "Some paths still use non-ASCII or mojibake names."
                        : "All playlist paths use ASCII-safe names."));
    }

    private static string FormatWorkflowToolTip(int step, string action, string workflow, string status) =>
        $"Step {step} — {action}{Environment.NewLine}{workflow}{Environment.NewLine}{Environment.NewLine}Status: {status}";

    private void ApplyWorkflowIndicator(
        Button button,
        string baseStyleKey,
        WorkflowIndicatorState state,
        string toolTip)
    {
        var styleKey = state switch
        {
            WorkflowIndicatorState.Ok => $"{baseStyleKey}.StatusOk",
            WorkflowIndicatorState.Attention => $"{baseStyleKey}.StatusAttention",
            _ => baseStyleKey
        };

        button.Style = (Style)FindResource(styleKey);
        button.ToolTip = toolTip;
    }
}
