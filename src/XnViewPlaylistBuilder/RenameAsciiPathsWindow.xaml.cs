using System.Windows;
using XnViewPlaylistBuilder.Core.Services;

namespace XnViewPlaylistBuilder;

public partial class RenameAsciiPathsWindow : Window
{
    private readonly PathRenamePreview _preview;

    public PathRenamePlan ExecutablePlan { get; private set; } = null!;

    public RenameAsciiPathsWindow(PathRenamePreview preview)
    {
        InitializeComponent();
        _preview = preview;
        SummaryTextBlock.Text = preview.FormatSummary();
        ShowMissingCheckBox.Visibility = preview.MissingCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        RefreshGrid();
        ApplyButton.IsEnabled = preview.ReadyCount > 0;
    }

    private void ShowMissingCheckBox_Changed(object sender, RoutedEventArgs e) =>
        RefreshGrid();

    private void RefreshGrid() =>
        OperationsDataGrid.ItemsSource = _preview.VisibleRows(ShowMissingCheckBox.IsChecked == true);

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ExecutablePlan = _preview.CreateExecutablePlan();
        if (ExecutablePlan.Operations.Count == 0)
        {
            MessageBox.Show(
                this,
                "No paths on disk are ready to rename. Remove missing entries with Check files first.",
                "Fix names",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirm = ConfirmDialogWindow.Show(this, new ConfirmDialogRequest
        {
            Title = "Rename files and folders",
            Message = "Permanently rename the selected paths on disk?",
            Detail =
                $"{ExecutablePlan.DirectoryOperationCount:N0} folder and {ExecutablePlan.FileOperationCount:N0} file rename(s) on disk " +
                $"(updating {_preview.OriginalPlan.AffectedEntryCount:N0} playlist paths). " +
                (_preview.MissingCount > 0
                    ? $"{_preview.MissingCount:N0} missing path(s) will be skipped. "
                    : string.Empty) +
                "If a target name already exists, a (1), (2), … suffix is added automatically.",
            ConfirmText = "Rename",
            CancelText = "Cancel"
        });

        if (!confirm.Confirmed)
        {
            return;
        }

        DialogResult = true;
        Close();
    }
}
