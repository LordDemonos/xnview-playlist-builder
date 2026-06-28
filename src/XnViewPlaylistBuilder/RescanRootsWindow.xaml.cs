using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using Microsoft.Win32;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder;

public partial class RescanRootsWindow : Window
{
    private readonly ObservableCollection<RescanRootRow> _rows = [];

    public RescanRootsWindow(IReadOnlyList<ScanRootSuggestion> suggestions, int totalRemovedCount)
    {
        InitializeComponent();
        foreach (var suggestion in suggestions)
        {
            _rows.Add(new RescanRootRow(suggestion));
        }

        SuggestionsDataGrid.ItemsSource = _rows;
        SummaryTextBlock.Text =
            $"{totalRemovedCount:N0} playlist entries were removed. " +
            "Pick the specific subject folders you want to scan — broader parents like a shared archive folder are listed but not selected by default.";
    }

    public bool ShouldScanAfterAdd { get; private set; }

    public IReadOnlyList<string> SelectedFolderPaths =>
        _rows.Where(row => row.IsSelected && Directory.Exists(row.FolderPath))
            .Select(row => row.FolderPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (SuggestionsDataGrid.SelectedItem is not RescanRootRow selectedRow)
        {
            MessageBox.Show(
                this,
                "Select a row first, then browse to replace its folder path.",
                "Browse for folder",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Choose scan folder",
            InitialDirectory = Directory.Exists(selectedRow.FolderPath)
                ? selectedRow.FolderPath
                : selectedRow.FolderPath
        };

        if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            return;
        }

        selectedRow.FolderPath = Path.GetFullPath(dialog.FolderName);
        selectedRow.IsSelected = true;
    }

    private void SelectSubjectFoldersButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var row in _rows)
        {
            row.IsSelected = row.Kind == ScanRootSuggestionKind.SubjectFolder;
        }
    }

    private void AddFoldersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryConfirmSelection(scanAfterAdd: false))
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void AddAndScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryConfirmSelection(scanAfterAdd: true))
        {
            return;
        }

        ShouldScanAfterAdd = true;
        DialogResult = true;
        Close();
    }

    private bool TryConfirmSelection(bool scanAfterAdd)
    {
        var selected = SelectedFolderPaths;
        if (selected.Count == 0)
        {
            MessageBox.Show(
                this,
                "Select at least one folder to add to the scan list, or choose Skip.",
                "Re-add scan folders",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        var broaderSelected = _rows
            .Where(row => row.IsSelected && row.Kind == ScanRootSuggestionKind.BroaderFolder)
            .Select(row => row.FolderPath)
            .ToList();

        if (broaderSelected.Count == 0)
        {
            return true;
        }

        var detail = string.Join(Environment.NewLine, broaderSelected.Take(6));
        if (broaderSelected.Count > 6)
        {
            detail += $"{Environment.NewLine}… and {broaderSelected.Count - 6:N0} more";
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Scan broad folder roots?",
            Message =
                $"You selected {broaderSelected.Count:N0} broader parent folder(s). " +
                "Scanning a large shared folder can take a long time.",
            Detail = detail,
            ConfirmText = scanAfterAdd ? "Add and scan" : "Add to scan list",
            CancelText = "Go back"
        });

        return confirm.Confirmed;
    }

    private sealed class RescanRootRow : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _folderPath;

        public RescanRootRow(ScanRootSuggestion suggestion)
        {
            _folderPath = suggestion.FolderPath;
            _isSelected = suggestion.IsSelectedByDefault;
            RemovedFileCount = suggestion.RemovedFileCount;
            Kind = suggestion.Kind;
            KindLabel = suggestion.KindLabel;
        }

        public int RemovedFileCount { get; }

        public ScanRootSuggestionKind Kind { get; }

        public string KindLabel { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                {
                    return;
                }

                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public string FolderPath
        {
            get => _folderPath;
            set
            {
                if (string.Equals(_folderPath, value, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                _folderPath = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
