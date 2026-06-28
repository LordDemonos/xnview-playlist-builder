using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Win32;
using XnViewPlaylistBuilder.Collections;
using XnViewPlaylistBuilder.Core.Logging;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder;

public partial class RenameUndoWindow : Window
{
    private readonly BatchObservableCollection<RenameLogRowViewModel> _rows = [];
    private readonly ObservableCollection<PathSegmentSubstitutionRow> _substitutions = [];
    private readonly List<RenameLogEntry> _allEntries = [];
    private readonly DispatcherTimer _refreshDebounceTimer;
    private CancellationTokenSource? _loadCancellationSource;
    private bool _suppressLogSelectionChanged;
    private bool _isUiReady;
    private int _loadGeneration;

    public PathRenameExecutionResult? UndoResult { get; private set; }

    public RenameUndoWindow()
    {
        InitializeComponent();
        _isUiReady = true;
        OperationsDataGrid.ItemsSource = _rows;
        SubstitutionsDataGrid.ItemsSource = _substitutions;
        OperationsDataGrid.SelectionChanged += (_, _) => UpdateSelectionSummary();
        _refreshDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _refreshDebounceTimer.Tick += (_, _) =>
        {
            _refreshDebounceTimer.Stop();
            _ = RefreshGridAsync(_loadGeneration, CancellationToken.None);
        };
        Loaded += (_, _) => _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        PopulateLogFileList();
        if (LogFileComboBox.SelectedItem is LogFileOption selected)
        {
            await LoadOperationsFromLogAsync(selected.FullPath);
        }
    }

    private void PopulateLogFileList()
    {
        _suppressLogSelectionChanged = true;
        try
        {
            LogFileComboBox.ItemsSource = null;

            if (!Directory.Exists(AppLog.LogDirectory))
            {
                return;
            }

            var logFiles = Directory.EnumerateFiles(AppLog.LogDirectory, "*.log", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Select(path => new LogFileOption(path))
                .ToList();

            LogFileComboBox.ItemsSource = logFiles;

            var current = logFiles.FirstOrDefault(option =>
                string.Equals(option.FullPath, AppLog.CurrentLogFile, StringComparison.OrdinalIgnoreCase));
            LogFileComboBox.SelectedItem = current ?? logFiles.FirstOrDefault();
        }
        finally
        {
            _suppressLogSelectionChanged = false;
        }
    }

    private void LogFileComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLogSelectionChanged ||
            LogFileComboBox.SelectedItem is not LogFileOption selected)
        {
            return;
        }

        _ = LoadOperationsFromLogAsync(selected.FullPath);
    }

    private void BrowseLogButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select log file",
            Filter = "Log files (*.log)|*.log|All files (*.*)|*.*",
            InitialDirectory = AppLog.LogDirectory,
            FileName = (LogFileComboBox.SelectedItem as LogFileOption)?.FileName
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FileName))
        {
            return;
        }

        var option = new LogFileOption(dialog.FileName);
        var items = LogFileComboBox.ItemsSource as IList<LogFileOption> ?? new List<LogFileOption>();
        if (!items.Any(item => string.Equals(item.FullPath, option.FullPath, StringComparison.OrdinalIgnoreCase)))
        {
            var merged = items.Concat([option])
                .OrderByDescending(item => item.LastWriteUtc)
                .ToList();
            LogFileComboBox.ItemsSource = merged;
        }

        LogFileComboBox.SelectedItem =
            (LogFileComboBox.ItemsSource as IEnumerable<LogFileOption>)?
            .FirstOrDefault(item => string.Equals(item.FullPath, option.FullPath, StringComparison.OrdinalIgnoreCase))
            ?? option;
    }

    private void BrowseAnchorButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select anchor folder on disk",
            InitialDirectory = GetInitialAnchorDirectory()
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        AnchorPathTextBox.Text = dialog.FolderName;
    }

    private string? GetInitialAnchorDirectory()
    {
        if (!string.IsNullOrWhiteSpace(AnchorPathTextBox.Text) && Directory.Exists(AnchorPathTextBox.Text))
        {
            return AnchorPathTextBox.Text;
        }

        return Directory.Exists(AppLog.LogDirectory) ? AppLog.LogDirectory : null;
    }

    private void AddSubstitutionButton_Click(object sender, RoutedEventArgs e)
    {
        _substitutions.Add(new PathSegmentSubstitutionRow());
        ScheduleRefreshGrid();
    }

    private void SubstitutionsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) =>
        ScheduleRefreshGrid();

    private void AnchorOrFilter_Changed(object sender, RoutedEventArgs e)
    {
        if (!_isUiReady)
        {
            return;
        }

        ScheduleRefreshGrid();
    }

    private void ScheduleRefreshGrid()
    {
        _refreshDebounceTimer.Stop();
        _refreshDebounceTimer.Start();
    }

    private async Task LoadOperationsFromLogAsync(string logFilePath)
    {
        var generation = ++_loadGeneration;
        SetLoadingState(true, "Reading log file…");

        _loadCancellationSource?.Cancel();
        _loadCancellationSource?.Dispose();
        _loadCancellationSource = new CancellationTokenSource();
        var token = _loadCancellationSource.Token;

        try
        {
            var entries = await Task.Run(() => RenameLogParser.ParseFile(logFilePath), token);
            if (generation != _loadGeneration || token.IsCancellationRequested)
            {
                return;
            }

            _allEntries.Clear();
            _allEntries.AddRange(entries);
            await RefreshGridAsync(generation, token, "Filtering rename lines…");
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer load.
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not read log file.{Environment.NewLine}{Environment.NewLine}{ex.Message}",
                "Undo renames",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (generation == _loadGeneration)
            {
                SetLoadingState(false);
            }
        }
    }

    private RenameUndoOptions BuildOptions() =>
        new()
        {
            AnchorPath = string.IsNullOrWhiteSpace(AnchorPathTextBox?.Text)
                ? null
                : AnchorPathTextBox.Text.Trim(),
            Substitutions = _substitutions
                .Where(row => !string.IsNullOrWhiteSpace(row.LogSegment))
                .Select(row => new PathSegmentSubstitution(row.LogSegment.Trim(), row.DiskSegment.Trim()))
                .ToList(),
            FilterToAnchorChildren = FilterToAnchorChildrenCheckBox?.IsChecked != false,
            HideParentCorrectedRows = HideParentCorrectedCheckBox?.IsChecked != false
        };

    private async Task RefreshGridAsync(
        int generation,
        CancellationToken cancellationToken,
        string? loadingMessage = null)
    {
        if (!_isUiReady)
        {
            return;
        }

        if (loadingMessage is not null)
        {
            SetLoadingState(true, loadingMessage);
        }

        var options = BuildOptions();
        var entrySnapshot = _allEntries.ToList();

        try
        {
            var visibleRows = await Task.Run(
                () => RenameUndoResolver.BuildVisibleRows(entrySnapshot, options, cancellationToken),
                cancellationToken);

            if (generation != _loadGeneration || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _rows.ReplaceAll(visibleRows
                .Select(resolution => RenameLogRowViewModel.From(resolution.Entry, resolution))
                .ToList());
            UpdateSelectionSummary();
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer refresh.
        }
        finally
        {
            if (loadingMessage is not null && generation == _loadGeneration)
            {
                SetLoadingState(false);
            }
        }
    }

    private void SetLoadingState(bool isLoading, string? message = null)
    {
        LoadingTextBlock.Text = message ?? "Loading log…";
        LoadingTextBlock.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        OperationsDataGrid.IsEnabled = !isLoading;
        UndoButton.IsEnabled = !isLoading && OperationsDataGrid.SelectedItems.Count > 0;
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e) =>
        OperationsDataGrid.SelectAll();

    private void SelectReadyButton_Click(object sender, RoutedEventArgs e)
    {
        OperationsDataGrid.UnselectAll();
        foreach (var row in _rows.Where(row => row.CanUndo))
        {
            OperationsDataGrid.SelectedItems.Add(row);
        }

        UpdateSelectionSummary();
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e) =>
        OperationsDataGrid.UnselectAll();

    private void UpdateSelectionSummary()
    {
        var selectedCount = OperationsDataGrid.SelectedItems.Count;
        var readyCount = _rows.Count(row => row.CanUndo);
        var totalEntries = _allEntries.Count;
        var summaryPrefix = totalEntries > 0 && _rows.Count != totalEntries
            ? $"{_rows.Count:N0} shown of {totalEntries:N0} log lines. "
            : string.Empty;

        SelectionSummaryTextBlock.Text =
            _rows.Count == 0 && totalEntries == 0
                ? "No matching rename lines."
                : $"{summaryPrefix}{selectedCount:N0} of {_rows.Count:N0} selected ({readyCount:N0} ready)";
        UndoButton.IsEnabled = selectedCount > 0 && LoadingTextBlock.Visibility != Visibility.Visible;
    }

    private async void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedRows = OperationsDataGrid.SelectedItems
            .Cast<RenameLogRowViewModel>()
            .ToList();

        if (selectedRows.Count == 0)
        {
            return;
        }

        var readyCount = selectedRows.Count(row => row.CanUndo);
        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Undo renames on disk",
            Message = $"Undo {selectedRows.Count:N0} selected rename(s)?",
            Detail =
                readyCount == selectedRows.Count
                    ? "Each selected row moves the resolved path back to its original name. Missing paths are skipped and logged."
                    : $"{readyCount:N0} of {selectedRows.Count:N0} selected rows are ready; others will be skipped.",
            ConfirmText = "Undo",
            CancelText = "Cancel"
        });

        if (!confirm.Confirmed)
        {
            return;
        }

        var entries = selectedRows.Select(row => row.Entry).ToList();
        var options = BuildOptions();
        var renameService = new MediaPathRenameService();
        var progressWindow = new WorkProgressWindow { Owner = this };
        progressWindow.Show();

        try
        {
            UndoResult = await progressWindow.RunAsync(
                "Undoing renames…",
                (progress, token) => Task.Run(
                    () => renameService.UndoRenames(entries, options, progress, token),
                    token));
        }
        catch (OperationCanceledException)
        {
            UndoResult = null;
            return;
        }
        finally
        {
            progressWindow.Close();
        }

        var result = UndoResult!;
        var message =
            $"Undid {result.CompletedCount:N0} rename(s)." +
            (result.SkippedCount > 0 ? $" Skipped {result.SkippedCount:N0} (see log)." : string.Empty);

        MessageBox.Show(this, message, "Undo renames", MessageBoxButton.OK, MessageBoxImage.Information);
        DialogResult = result.CompletedCount > 0;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _loadCancellationSource?.Cancel();
        _loadCancellationSource?.Dispose();
        _loadCancellationSource = null;
        base.OnClosed(e);
    }

    private sealed class LogFileOption
    {
        public LogFileOption(string fullPath)
        {
            FullPath = Path.GetFullPath(fullPath);
            FileName = Path.GetFileName(FullPath);
            LastWriteUtc = File.Exists(FullPath) ? File.GetLastWriteTimeUtc(FullPath) : DateTime.MinValue;
            DisplayName = $"{FileName}  ({LastWriteUtc.ToLocalTime():g})";
        }

        public string FullPath { get; }
        public string FileName { get; }
        public DateTime LastWriteUtc { get; }
        public string DisplayName { get; }
    }

    private sealed class PathSegmentSubstitutionRow
    {
        public string LogSegment { get; set; } = string.Empty;
        public string DiskSegment { get; set; } = string.Empty;
    }

    private sealed class RenameLogRowViewModel
    {
        public required RenameLogEntry Entry { get; init; }
        public required RenameUndoResolution Resolution { get; init; }
        public int LineNumber => Entry.LineNumber;
        public string SourcePath => Entry.SourcePath;
        public string TargetPath => Entry.TargetPath;
        public string StatusLabel => Resolution.StatusLabel;
        public string ResolvedPathLabel => Resolution.ResolvedPathLabel;
        public bool CanUndo => Resolution.CanUndo;

        public static RenameLogRowViewModel From(RenameLogEntry entry, RenameUndoResolution resolution) =>
            new() { Entry = entry, Resolution = resolution };
    }
}
